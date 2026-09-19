# Runs a Pipeline command against this repository's open Unity Editor.
# Example: ./Tools/unity-command.ps1 editor_status
# Extra CLI options can be passed as an array with -CommandArguments.
param(
    [string]$Command = 'editor_status',
    [string[]]$CommandArguments = @()
)

$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path -Parent $PSScriptRoot
$cliPath = Join-Path $env:LOCALAPPDATA 'Unity/bin/unity.exe'
if (-not (Test-Path -LiteralPath $cliPath)) {
    $cliPath = (Get-Command unity -CommandType Application -ErrorAction Stop).Source
}

& $cliPath command --project-path $projectRoot --json --non-interactive --no-pager $Command @CommandArguments
exit $LASTEXITCODE
