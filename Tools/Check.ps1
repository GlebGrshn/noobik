param(
    [string]$UnityPath = 'C:\Program Files\Unity\Hub\Editor\6000.6.3f1\Editor\Unity.exe',
    [switch]$BuildWeb
)
$ErrorActionPreference = 'Stop'
$project = Split-Path -Parent $PSScriptRoot
if (!(Test-Path -LiteralPath $UnityPath)) { throw "Unity not found: $UnityPath" }
New-Item -ItemType Directory -Force (Join-Path $project 'Logs'), (Join-Path $project 'TestResults') | Out-Null
function Invoke-Unity([string[]]$UnityArguments) {
    $process = Start-Process -FilePath $UnityPath -ArgumentList $UnityArguments -PassThru -WindowStyle Hidden
    $process.WaitForExit()
    if ($process.ExitCode -ne 0) { throw "Unity exited with code $($process.ExitCode). See Logs." }
}
Invoke-Unity @('-batchmode', '-nographics', '-projectPath', ('"' + $project + '"'), '-runTests', '-testPlatform', 'EditMode', '-testResults', ('"' + (Join-Path $project 'TestResults/editmode.xml') + '"'), '-logFile', ('"' + (Join-Path $project 'Logs/tests.log') + '"'))
[xml]$result = Get-Content -LiteralPath (Join-Path $project 'TestResults/editmode.xml')
if ($result.'test-run'.result -ne 'Passed') { throw 'Unity tests did not pass.' }
if ($BuildWeb) {
    Invoke-Unity @('-batchmode', '-nographics', '-quit', '-projectPath', ('"' + $project + '"'), '-executeMethod', 'ProjectSetup.BuildWeb', '-logFile', ('"' + (Join-Path $project 'Logs/webgl.log') + '"'))
}
Write-Output 'Unity checks passed.'
