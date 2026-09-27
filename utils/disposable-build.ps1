param(
    [ValidateSet('Inspect', 'Build', 'Clean')]
    [string]$Mode = 'Inspect'
)

$ErrorActionPreference = 'Stop'
$repoRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$artifactRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$slot = [IO.Path]::GetFullPath((Join-Path $artifactRoot 'disposable'))
$app = Join-Path $slot 'app'
$marker = Join-Path $slot '.pureds4-disposable-slot'
$manifest = Join-Path $app '.pureds4-managed-files.txt'

if ([IO.Path]::GetDirectoryName($slot) -ne $artifactRoot -or
    -not (Test-Path -LiteralPath (Join-Path $repoRoot 'PureDS4.sln') -PathType Leaf)) {
    throw 'The disposable slot is not inside the expected PureDS4 repository.'
}

function Assert-PlainDirectory([string]$path) {
    if ((Get-Item -LiteralPath $path -Force).Attributes.HasFlag(
            [IO.FileAttributes]::ReparsePoint)) {
        throw "Refusing a reparse-point directory: $path"
    }
}

function Assert-OwnedSlot {
    Assert-PlainDirectory $artifactRoot
    Assert-PlainDirectory $slot
    if (-not (Test-Path -LiteralPath $marker -PathType Leaf)) {
        throw "The disposable slot has no valid ownership marker: $slot"
    }
    if ((Get-Item -LiteralPath $marker -Force).Attributes.HasFlag(
            [IO.FileAttributes]::ReparsePoint)) {
        throw "The disposable marker is a reparse point: $marker"
    }
    if ((Get-Content -LiteralPath $marker -Raw).Trim() -ne
        'PureDS4 disposable build slot v1') {
        throw "The disposable slot has no valid ownership marker: $slot"
    }
    $top = @(Get-ChildItem -LiteralPath $slot -Force)
    if (@($top | Where-Object { $_.Name -notin @('app', '.pureds4-disposable-slot') }).Count -gt 0 -or
        -not (Test-Path -LiteralPath $app -PathType Container) -or
        -not (Test-Path -LiteralPath $manifest -PathType Leaf)) {
        throw "The disposable slot is incomplete or contains unowned content: $slot"
    }
    Assert-PlainDirectory $app
    if ((Get-Item -LiteralPath $manifest -Force).Attributes.HasFlag(
            [IO.FileAttributes]::ReparsePoint)) {
        throw "The disposable manifest is a reparse point: $manifest"
    }
    $entries = @(Get-ChildItem -LiteralPath $app -Force -Recurse)
    if (@($entries | Where-Object {
                $_.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)
            }).Count -gt 0) {
        throw "The disposable slot contains a reparse point: $slot"
    }
    $owned = @((Get-Content -LiteralPath $manifest | Where-Object { $_ } |
            ForEach-Object { $_.Replace('\', '/') }))
    $actual = @($entries | Where-Object { -not $_.PSIsContainer } |
        ForEach-Object { $_.FullName.Substring($app.Length + 1).Replace('\', '/') } |
        Where-Object { $_ -ne '.pureds4-managed-files.txt' })
    if (@($actual | Where-Object { $_ -notin $owned }).Count -gt 0 -or
        @($owned | Where-Object { $_ -notin $actual }).Count -gt 0) {
        throw "The disposable slot contains files outside its package manifest: $slot"
    }
    return $actual.Count
}

if (Test-Path -LiteralPath $artifactRoot) {
    Assert-PlainDirectory $artifactRoot
}

if ($Mode -eq 'Inspect') {
    if (Test-Path -LiteralPath $slot) {
        $count = Assert-OwnedSlot
        Write-Output "Disposable build: $app ($count managed files)"
    } else {
        Write-Output "Disposable slot is empty: $slot"
    }
    return
}

if (Test-Path -LiteralPath $slot) {
    $null = Assert-OwnedSlot
    $running = @(Get-CimInstance Win32_Process -Filter "Name = 'PureDS4.exe'")
    foreach ($process in $running) {
        if ([string]::IsNullOrWhiteSpace($process.ExecutablePath)) {
            throw 'A running PureDS4 process has an unreadable path; close it before replacing the disposable build.'
        }
        $processPath = [IO.Path]::GetFullPath($process.ExecutablePath)
        if ($processPath.StartsWith($slot + [IO.Path]::DirectorySeparatorChar,
                [StringComparison]::OrdinalIgnoreCase)) {
            throw "The disposable build is running (PID $($process.ProcessId)); close it first."
        }
    }
    Remove-Item -LiteralPath $slot -Recurse -Force
    Write-Output "Removed previous verified disposable build: $slot"
}

if ($Mode -eq 'Clean') { return }

New-Item -ItemType Directory -Path $app -Force | Out-Null
Set-Content -LiteralPath $marker -Value 'PureDS4 disposable build slot v1' -Encoding ascii
& dotnet publish (Join-Path $repoRoot 'PureDS4\PureDS4.csproj') `
    -c Release -p:Platform=x64 -r win-x64 --self-contained false `
    -p:RestoreLockedMode=true -o $app
if ($LASTEXITCODE -ne 0 -or
    -not (Test-Path -LiteralPath (Join-Path $app 'PureDS4.exe') -PathType Leaf)) {
    throw "Disposable publish failed. Inspect the incomplete slot before cleaning: $slot"
}
$null = Assert-OwnedSlot
Write-Output "Disposable build ready (not launched): $(Join-Path $app 'PureDS4.exe')"
