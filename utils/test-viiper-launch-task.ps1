[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BackendScript,
    [switch]$EmitInstallerTasks
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

if ($EmitInstallerTasks) {
    # CDXML module autoload can replace same-named mock functions. Import
    # before installing the interceptors; only the New-* constructors stay real.
    Import-Module ScheduledTasks -ErrorAction Stop
    $script:MemoryTasks = @{}
    function Register-ScheduledTask {
        param($TaskPath, $TaskName, $Action, $Principal, $Settings, $Trigger,
            [switch]$Force)
        if ($TaskPath -ne '\' -or -not $Force) {
            throw 'Unexpected registration contract.'
        }
        $script:MemoryTasks[$TaskName] = [pscustomobject]@{
            Actions = @($Action)
            Principal = $Principal
            Settings = $Settings
            Triggers = @($Trigger)
        }
    }
    function Enable-ScheduledTask {
        param($TaskPath, $TaskName, $ErrorAction)
        if (-not $script:MemoryTasks.ContainsKey($TaskName)) {
            throw 'Enabling an unknown in-memory task.'
        }
    }
    function Get-ScheduledTask {
        param($TaskPath, $TaskName, $ErrorAction)
        return $script:MemoryTasks[$TaskName]
    }
    function Unregister-ScheduledTask {
        param($TaskPath, $TaskName, $Confirm, $ErrorAction)
        throw 'Registration failed in the memory-only installer fixture.'
    }
    function Start-ScheduledTask { throw 'Task execution is forbidden in this fixture.' }
    function Write-SetupLog { param($Message, $Color) }

    foreach ($commandName in @('Register-ScheduledTask', 'Enable-ScheduledTask',
            'Get-ScheduledTask', 'Unregister-ScheduledTask', 'Start-ScheduledTask')) {
        $command = Get-Command $commandName
        if ($command.CommandType -ne 'Function' -or $command.ModuleName) {
            throw "Host task command is not intercepted: $commandName"
        }
    }
    foreach ($functionName in @('Register-HighestLogonTask',
            'Register-ViiperRunTask', 'Register-Ds4WindowsRunTask')) {
        $node = $ast.Find({
            param($candidate)
            $candidate -is [Management.Automation.Language.FunctionDefinitionAst] -and
                $candidate.Name -eq $functionName
        }, $true)
        if (-not $node) { throw "Installer function missing: $functionName" }
        Invoke-Expression $node.Extent.Text
    }
    if (-not (Register-ViiperRunTask $viiper 'RunPureDS4VIIPER') -or
            -not (Register-Ds4WindowsRunTask $app)) {
        throw 'Installer did not approve its in-memory task definitions.'
    }
    $fixtures = foreach ($name in @('RunPureDS4VIIPER', 'RunPureDS4')) {
        $task = $script:MemoryTasks[$name]
        if ($task.Actions.Count -ne 1) { throw 'Unexpected action count.' }
        $action = $task.Actions[0]
        [pscustomobject]@{
            Name = $name
            Execute = [string]$action.Execute
            Arguments = [string]$action.Arguments
            WorkingDirectory = [string]$action.WorkingDirectory
            UserId = [string]$task.Principal.UserId
            RunLevel = [string]$task.Principal.RunLevel
            LogonType = [string]$task.Principal.LogonType
            Enabled = [bool]$task.Settings.Enabled
            Priority = [int]$task.Settings.Priority
            ExecutionTimeLimit = [string]$task.Settings.ExecutionTimeLimit
            MultipleInstances = [int]$task.Settings.MultipleInstances
            DisallowStartIfOnBatteries = [bool]$task.Settings.DisallowStartIfOnBatteries
            StopIfGoingOnBatteries = [bool]$task.Settings.StopIfGoingOnBatteries
            Triggers = @($task.Triggers | Where-Object { $null -ne $_ } |
                ForEach-Object {
                    [pscustomobject]@{
                        Type = $_.CimClass.CimClassName
                        UserId = [string]$_.UserId
                        Enabled = [bool]$_.Enabled
                    }
                })
        }
    }
    ConvertTo-Json -InputObject @($fixtures) -Depth 5
    return
}

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
