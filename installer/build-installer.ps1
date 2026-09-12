[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PublishRoot,
    [string]$ProductVersion = "5.1.0",
    [string]$DisplayVersion = "5.1.0-beta.1",
    [string]$BundleVersion = "5.1.0-beta.1",
    [string]$OutputDirectory,
    [switch]$SkipApplicationPublish,
    [switch]$RequireSigning,
    # Builds the DualShock 3 hardware-validation installer instead of a normal
    # one. Off unless passed explicitly. The resulting bundle is a separate
    # product with its own name, upgrade code and install folder, so it can
    # never upgrade over or be mistaken for a real PureDS4 installation, and
    # it is refused outright when release signing is requested.
    [switch]$ExperimentalDS3
)

$ErrorActionPreference = "Stop"
if ($ExperimentalDS3 -and $RequireSigning) {
    throw (
        "The DualShock 3 hardware-validation build is not a release artifact " +
        "and must never be produced through the signed release path."
    )
}
$experimentalArgs = @()
if ($ExperimentalDS3) {
    $experimentalArgs = @("-p:PureDS4ExperimentalDS3=true")
    # Switching the DS3 compile symbol changes every emitted assembly, but
    # MSBuild judges CopyToPublishDirectory content as up to date from the
    # previous publish of the same property set and skips it, leaving an
    # extras-less package. The offline-package guard in PureDS4.csproj catches
    # that, so it fails loudly rather than shipping a broken installer; this
    # clean removes the cause on the experimental path.
    foreach ($stale in @(
        (Join-Path $PSScriptRoot "..\PureDS4\obj\x64\Release"),
        (Join-Path $PSScriptRoot "..\PureDS4\bin\x64\Release"))) {
        if (Test-Path -LiteralPath $stale) {
            Remove-Item -LiteralPath $stale -Recurse -Force
        }
    }
    Write-Warning (
        "Building the DualShock 3 HARDWARE-VALIDATION installer. This is not " +
        "a release artifact. It installs as a separate product and must not " +
        "be published or promoted."
    )
    if (-not $DisplayVersion.Contains("ds3")) {
        $DisplayVersion = "$DisplayVersion-ds3experimental"
    }
}
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot ".."))
$publishPath = [IO.Path]::GetFullPath($PublishRoot)
$releaseInputValidator = Join-Path $repoRoot "utils\validate-release-inputs.py"
$releaseInputArguments = @($releaseInputValidator, "--verify-signatures")
if ($RequireSigning) {
    $releaseInputArguments += "--require-release-ready"
}
& python @releaseInputArguments
if ($LASTEXITCODE -ne 0) {
    throw "Release input validation failed."
}
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repoRoot "bin\x64\Release\installer"
}
$outputPath = [IO.Path]::GetFullPath($OutputDirectory)
$signingEnabled = -not [string]::IsNullOrWhiteSpace(
    $env:DS4W_SIGN_CERT_PATH)
if ($RequireSigning -and -not $signingEnabled) {
    throw (
        "Release signing is required, but DS4W_SIGN_CERT_PATH is not set. " +
        "Unsigned public installers are intentionally blocked."
    )
}
$signtool = $null
if ($signingEnabled) {
    $signtool = (Get-Command signtool.exe -ErrorAction Stop).Source
}

function Invoke-SignAndVerify([string]$path) {
    if (-not $signingEnabled) { return }
    $timestampUrl = if ($env:DS4W_SIGN_TIMESTAMP_URL) {
        $env:DS4W_SIGN_TIMESTAMP_URL
    }
    else { "https://timestamp.digicert.com" }
    & $signtool sign /fd SHA256 /f $env:DS4W_SIGN_CERT_PATH `
        /p $env:DS4W_SIGN_CERT_PASSWORD /tr $timestampUrl /td SHA256 $path
    if ($LASTEXITCODE -ne 0) {
        throw "Authenticode signing failed: $path"
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne
            [Management.Automation.SignatureStatus]::Valid) {
        throw "Authenticode verification failed for '$path': $($signature.StatusMessage)"
    }
}

function Assert-ReleaseSignature([string]$path) {
    if (-not $RequireSigning) { return }
    $signature = Get-AuthenticodeSignature -LiteralPath $path
    if ($signature.Status -ne
            [Management.Automation.SignatureStatus]::Valid) {
        throw "Required first-party signature is invalid for '$path': $($signature.StatusMessage)"
    }
}

$buildMutex = [Threading.Mutex]::new($false,
    "Global\PureDS4-Installer-Build")
$buildMutexOwned = $false
try {
    try {
        $buildMutexOwned = $buildMutex.WaitOne(0)
    }
    catch [Threading.AbandonedMutexException] {
        $buildMutexOwned = $true
    }
    if (-not $buildMutexOwned) {
        throw (
            "Another PureDS4 installer composition is already running. " +
            "Wait for that build to finish instead of overlapping WiX/MSI validation."
        )
    }

    # A forcibly terminated shell releases its mutex immediately, while a
    # dotnet/WiX child can survive long enough to keep MSI consistency
    # validation active. Converge only exact build children whose command
    # line points into this repository before starting another composition.
    $repoPattern = [regex]::Escape($repoRoot.TrimEnd('\', '/'))
    $installerProjectPattern =
        '(?i)(PureDS4\.Package|PureDS4\.Bundle|build-installer\.ps1)'
    $orphanedBuilds = @()
    for ($attempt = 0; $attempt -lt 120; $attempt++) {
        $orphanedBuilds = @(Get-CimInstance Win32_Process `
            -ErrorAction SilentlyContinue | Where-Object {
                $_.ProcessId -ne $PID -and $_.CommandLine -and
                $_.CommandLine -match $repoPattern -and
                $_.CommandLine -match $installerProjectPattern
            })
        if ($orphanedBuilds.Count -eq 0) { break }
        if ($attempt -eq 0) {
            Write-Host (
                "Waiting for a previous orphaned WiX/MSI build from this " +
                "repository to exit safely...") -ForegroundColor Yellow
        }
        Start-Sleep -Seconds 1
    }
    if ($orphanedBuilds.Count -gt 0) {
        $owners = ($orphanedBuilds | ForEach-Object {
            "PID=$($_.ProcessId) $($_.Name)"
        }) -join ", "
        throw (
            "A previous installer build from this repository is still " +
            "active after 120 seconds ($owners). No overlapping WiX/MSI " +
            "composition was started."
        )
    }

if ($ProductVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?$') {
    throw "ProductVersion must be a numeric Windows Installer version: $ProductVersion"
}
if (-not $BundleVersion) {
    # Human CI labels (date + commit) belong in filenames, not in the Burn
    # upgrade ordering contract. Release workflows can explicitly pass their
    # monotonic numeric bundle version.
    $BundleVersion = $ProductVersion
}
if ($BundleVersion -notmatch '^\d+\.\d+\.\d+(?:\.\d+)?(?:-[0-9A-Za-z.-]+)?$') {
    throw "BundleVersion must be a numeric or semantic version: $BundleVersion"
}
if ([string]::IsNullOrWhiteSpace($DisplayVersion) -or
    $DisplayVersion -notmatch '^[0-9A-Za-z][0-9A-Za-z._-]{0,79}$') {
    throw (
        "DisplayVersion must be a filename-safe release identifier using " +
        "only letters, numbers, periods, underscores, and hyphens: " +
        $DisplayVersion
    )
}

if (-not $SkipApplicationPublish) {
    & dotnet publish (Join-Path $repoRoot "PureDS4\PureDS4.csproj") `
        -c Release -p:Platform=x64 -r win-x64 --self-contained true `
        -p:RestoreLockedMode=true `
        -p:AssemblyVersion=$ProductVersion -p:FileVersion=$ProductVersion `
        -p:Version=$ProductVersion -p:InformationalVersion=$DisplayVersion `
        @experimentalArgs `
        -o $publishPath
    if ($LASTEXITCODE -ne 0) { throw "PureDS4 publish failed." }
}
if (-not (Test-Path -LiteralPath (Join-Path $publishPath "PureDS4.exe") -PathType Leaf)) {
    throw "PureDS4 publish output is incomplete: $publishPath"
}
Invoke-SignAndVerify (Join-Path $publishPath "PureDS4.exe")
# VIIPER is an immutable upstream release payload. Its compiled-in SHA-256
# and generated package sidecar are validated below; signing it here would
# mutate the executable after PureDS4 has pinned that identity.
$releaseMarker = Join-Path $publishPath "PureDS4.release"
if (-not (Test-Path -LiteralPath $releaseMarker -PathType Leaf)) {
    throw "PureDS4 publish output has no release identity: $releaseMarker"
}
$publishedRelease = (Get-Content -LiteralPath $releaseMarker -Raw).Trim()
if (-not [string]::Equals($publishedRelease, $DisplayVersion,
        [StringComparison]::Ordinal)) {
    throw (
        "DisplayVersion '$DisplayVersion' does not match the completed " +
        "publish identity '$publishedRelease'. Refusing to compose a mixed " +
        "or stale installer."
    )
}

$generatedWix = Join-Path $repoRoot "installer\PureDS4.Package\GeneratedFiles.wxs"
$manifestPath = Join-Path $publishPath "package-manifest.json"
& python (Join-Path $repoRoot "utils\generate-installer-files.py") `
    $publishPath $generatedWix $manifestPath --version $DisplayVersion
if ($LASTEXITCODE -ne 0) { throw "Installer manifest generation failed." }

& dotnet publish (Join-Path $repoRoot "installer\PureDS4.SetupActions\PureDS4.SetupActions.csproj") `
    -c Release -p:Platform=x64 -p:Version=$ProductVersion `
    -p:RestoreLockedMode=true `
    -r win-x64 --self-contained true `
    -o (Join-Path $repoRoot "installer\PureDS4.SetupActions\bin\x64\Release\publish")
if ($LASTEXITCODE -ne 0) { throw "Setup action host build failed." }
$setupActions = Join-Path $repoRoot "installer\PureDS4.SetupActions\bin\x64\Release\publish\PureDS4.SetupActions.exe"
Invoke-SignAndVerify $setupActions

& dotnet publish (Join-Path $repoRoot "installer\PureDS4.Bootstrapper\PureDS4.Bootstrapper.csproj") `
    -c Release -p:Platform=x64 -p:Version=$ProductVersion `
    -p:RestoreLockedMode=true `
    @experimentalArgs `
    -r win-x64 --self-contained true `
    -o (Join-Path $repoRoot "installer\PureDS4.Bootstrapper\bin\x64\Release\publish")
if ($LASTEXITCODE -ne 0) { throw "Bootstrapper UI build failed." }
$baRoot = Join-Path $repoRoot "installer\PureDS4.Bootstrapper\bin\x64\Release\publish"
Invoke-SignAndVerify (Join-Path $baRoot "PureDS4.Bootstrapper.exe")

$packageProject = Join-Path $repoRoot "installer\PureDS4.Package\PureDS4.Package.wixproj"
& dotnet build $packageProject -t:Rebuild -c Release -p:Platform=x64 `
    -p:RestoreLockedMode=true `
    -p:Version=$ProductVersion -p:ProductVersion=$ProductVersion `
    -p:ExperimentalDS3=$($ExperimentalDS3.IsPresent.ToString().ToLower()) `
    -p:PublishRoot=$publishPath
if ($LASTEXITCODE -ne 0) { throw "PureDS4 MSI build failed." }

# The experimental package carries a distinct file name so a DS3 MSI can
# never be mistaken for, or picked up in place of, a release MSI.
$msiSuffix = if ($ExperimentalDS3) { "_DS3Experimental" } else { "" }
$msiPath = Join-Path $repoRoot "installer\PureDS4.Package\bin\x64\Release\PureDS4_${ProductVersion}_x64${msiSuffix}.msi"
Invoke-SignAndVerify $msiPath
$setupActionsHash = (Get-FileHash -LiteralPath $setupActions -Algorithm SHA256).Hash
if ($setupActionsHash -notmatch '^[0-9A-F]{64}$') {
    throw "Could not derive a content-addressed setup-helper cache identity."
}
$extrasRoot = Join-Path $repoRoot "extras"
$bundleProject = Join-Path $repoRoot "installer\PureDS4.Bundle\PureDS4.Bundle.wixproj"
& dotnet build $bundleProject -t:Rebuild -c Release -p:Platform=x64 `
    -p:RestoreLockedMode=true `
    -p:Version=$ProductVersion -p:BundleVersion=$BundleVersion `
    -p:DisplayVersion=$DisplayVersion `
    -p:ExperimentalDS3=$($ExperimentalDS3.IsPresent.ToString().ToLower()) `
    -p:MsiPath=$msiPath -p:BootstrapperRoot=$baRoot `
    -p:SetupActionsPath=$setupActions -p:SetupActionsHash=$setupActionsHash `
    -p:ExtrasRoot=$extrasRoot
if ($LASTEXITCODE -ne 0) { throw "PureDS4 Burn bundle build failed." }

$builtInstaller = Join-Path $repoRoot "installer\PureDS4.Bundle\bin\x64\Release\PureDS4_${DisplayVersion}_Setup_x64.exe"
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$finalInstaller = Join-Path $outputPath "PureDS4_${DisplayVersion}_Setup_x64.exe"
$finalManifest = Join-Path $outputPath "package-manifest.json"
$publishId = [Guid]::NewGuid().ToString("N")
$pendingInstaller = $finalInstaller + ".pending-" + $publishId
$pendingManifest = $finalManifest + ".pending-" + $publishId

function Publish-InstallerFileAtomically(
        [string]$source, [string]$destination) {
    if (Test-Path -LiteralPath $destination -PathType Leaf) {
        $backup = $destination + ".backup-" + [Guid]::NewGuid().ToString("N")
        try {
            [IO.File]::Replace($source, $destination, $backup, $true)
        }
        finally {
            if (Test-Path -LiteralPath $backup) {
                Remove-Item -LiteralPath $backup -Force
            }
        }
    }
    else {
        [IO.File]::Move($source, $destination)
    }
}

try {
    Copy-Item -LiteralPath $builtInstaller -Destination $pendingInstaller
    Copy-Item -LiteralPath $manifestPath -Destination $pendingManifest

& (Join-Path $env:SystemRoot `
    "System32\WindowsPowerShell\v1.0\powershell.exe") `
    -NoLogo -NoProfile -ExecutionPolicy Bypass `
    -File (Join-Path $repoRoot "utils\test-viiper-reboot-boundary.ps1") `
    -BackendScript (Join-Path $repoRoot "extras\install-viiper-backend.ps1")
if ($LASTEXITCODE -ne 0) {
    throw "USB-IP reboot-boundary simulation failed."
}

& python (Join-Path $repoRoot "utils\test-installer-state-machine.py")
if ($LASTEXITCODE -ne 0) {
    throw "Installer state-machine simulation failed."
}

& python (Join-Path $repoRoot "utils\validate-installer.py") `
    --publish-root $publishPath --manifest $manifestPath `
    --installer $pendingInstaller --bundle-source (Join-Path $repoRoot "installer\PureDS4.Bundle\Bundle.wxs")
if ($LASTEXITCODE -ne 0) { throw "Installer validation failed." }

Invoke-SignAndVerify $pendingInstaller
Assert-ReleaseSignature $pendingInstaller

    # The installer EXE is the externally visible commit point. Publish its
    # verified sidecar first so a terminated composition can never expose a
    # new installer beside an older manifest.
    Publish-InstallerFileAtomically $pendingManifest $finalManifest
    Publish-InstallerFileAtomically $pendingInstaller $finalInstaller
}
finally {
    foreach ($pending in @($pendingInstaller, $pendingManifest)) {
        if (Test-Path -LiteralPath $pending) {
            Remove-Item -LiteralPath $pending -Force
        }
    }
}

Write-Host "Standard installer ready: $finalInstaller" -ForegroundColor Green
}
finally {
    if ($buildMutexOwned) {
        try { $buildMutex.ReleaseMutex() } catch { }
    }
    $buildMutex.Dispose()
}
