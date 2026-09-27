[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackendScript
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$tokens = $null
$parseErrors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile(
    (Resolve-Path -LiteralPath $BackendScript).Path,
    [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -gt 0) {
    throw 'Backend installer has PowerShell parse errors.'
}
$definition = $ast.Find({
    param($node)
    $node -is [Management.Automation.Language.FunctionDefinitionAst] -and
        $node.Name -eq 'Test-HighestLogonTask'
}, $true)
if (-not $definition) { throw 'Owned-task verifier is missing.' }
Invoke-Expression $definition.Extent.Text

$script:TargetUserSid = 'S-1-5-21-1234-5678-9012-1001'
$viiper = 'C:\Program Files\PureDS4\VIIPER\viiper.exe'
$app = 'C:\Program Files\PureDS4\PureDS4.exe'
function Convert-AccountToSid([string]$identity) { return $identity }
function Get-ScheduledTask {
    param([string]$TaskPath, [string]$TaskName, $ErrorAction)
    return $script:RegisteredTask
}
function New-FakeTask([string]$path, [string]$arguments,
        [int]$logonTriggers) {
    $triggers = @()
    if ($logonTriggers -gt 0) {
        $triggers = @([pscustomobject]@{
            CimClass = [pscustomobject]@{
                CimClassName = 'MSFT_TaskLogonTrigger'
            }
            UserId = ''
        })
    }
    return [pscustomobject]@{
        Actions = @([pscustomobject]@{
            Execute = $path
            Arguments = $arguments
            WorkingDirectory = Split-Path -Parent $path
        })
        Triggers = $triggers
        Principal = [pscustomobject]@{
            UserId = $script:TargetUserSid
            RunLevel = 'Highest'
            LogonType = 'Interactive'
        }
        Settings = [pscustomobject]@{ Enabled = $true }
    }
}

$script:RegisteredTask = New-FakeTask $viiper 'server' 0
if (-not (Test-HighestLogonTask 'RunPureDS4VIIPER' $viiper 'server' `
        (Split-Path -Parent $viiper) $true $false)) {
    throw 'The exact no-trigger VIIPER launcher was rejected.'
}
$script:RegisteredTask.Triggers = $null
if (-not (Test-HighestLogonTask 'RunPureDS4VIIPER' $viiper 'server' `
        (Split-Path -Parent $viiper) $true $false)) {
    throw 'A null trigger list was rejected for the on-demand launcher.'
}
if (Test-HighestLogonTask 'RunPureDS4VIIPER' $viiper 'server' `
        (Split-Path -Parent $viiper) $true $true) {
    throw 'VIIPER launcher was accepted as a logon task.'
}
$script:RegisteredTask = New-FakeTask $viiper 'server' 1
if (Test-HighestLogonTask 'RunPureDS4VIIPER' $viiper 'server' `
        (Split-Path -Parent $viiper) $true $false) {
    throw 'A VIIPER logon trigger was accepted as on-demand.'
}
$script:RegisteredTask = New-FakeTask $app '-m' 1
if (-not (Test-HighestLogonTask 'RunPureDS4' $app '-m' `
        (Split-Path -Parent $app) $true $true)) {
    throw 'The exact PureDS4 logon task was rejected.'
}
$script:RegisteredTask.Principal.UserId = 'S-1-5-21-9999-9999-9999-1001'
if (Test-HighestLogonTask 'RunPureDS4' $app '-m' `
        (Split-Path -Parent $app) $true $true) {
    throw 'A task owned by another account was accepted.'
}
Write-Host 'VIIPER on-demand and PureDS4 logon task contracts passed.'
