[CmdletBinding()]
param(
    [switch]$ReleaseCandidate,
    [string]$EventName = $env:GITHUB_EVENT_NAME,
    [string]$SourceCommit = $env:GITHUB_SHA,
    [string]$RunNumber = $env:GITHUB_RUN_NUMBER,
    [datetime]$BuildDate = (Get-Date),
    [string]$ProjectPath = (Join-Path $PSScriptRoot '..\PureDS4\PureDS4.csproj')
)

$ErrorActionPreference = 'Stop'
if ($SourceCommit -notmatch '^[0-9a-fA-F]{40}$' -or $RunNumber -notmatch '^\d+$') {
    throw 'Build identity requires a full source SHA and numeric workflow run number.'
}
if ($ReleaseCandidate -and $EventName -ne 'workflow_dispatch') {
    throw 'Target-version candidates require an explicit manual workflow dispatch.'
}

[xml]$project = Get-Content -LiteralPath $ProjectPath -Raw
$assemblyVersion = @($project.Project.PropertyGroup.AssemblyVersion | Where-Object { $_ })
$displayVersion = @($project.Project.PropertyGroup.Version | Where-Object { $_ })
if ($assemblyVersion.Count -ne 1 -or $assemblyVersion[0] -notmatch '^\d+\.\d+\.\d+\.\d+$') {
    throw 'Expected one four-part application assembly version.'
}
$productVersion = ($assemblyVersion[0] -split '\.')[0..2] -join '.'
if ($displayVersion.Count -ne 1 -or $displayVersion[0] -cne $productVersion) {
    throw 'Application display and MSI product versions must agree for this packaging workflow.'
}

[pscustomobject]@{
    DisplayVersion = if ($ReleaseCandidate) { $productVersion } else {
        '{0}-ci-{1}' -f $BuildDate.ToString('yyyy.MM.dd'), $SourceCommit.Substring(0, 7)
    }
    ProductVersion = $productVersion
    BundleVersion = if ($ReleaseCandidate) { $productVersion } else { "$productVersion-ci.$RunNumber" }
    Kind = if ($ReleaseCandidate) { 'release-candidate' } else { 'ci' }
}
