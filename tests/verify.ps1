[CmdletBinding()]
param([switch]$Measure, [string]$ProgramDirectory='release')
$ErrorActionPreference = 'Stop'
$taskRoot = Split-Path -Parent $PSScriptRoot
$exePath = Join-Path $taskRoot ($ProgramDirectory+'\桌面小组件.exe')
if (!(Test-Path -LiteralPath $exePath)) { & (Join-Path $taskRoot 'build.ps1') }
$mode = if ($Measure) { '--measure' } else { '--verify' }
$resultName = if ($Measure) { 'performance.txt' } else { 'results.txt' }
$resultPath = Join-Path $taskRoot ($ProgramDirectory+'\verification\' + $resultName)
$testStarted = Get-Date
$testProcess = Start-Process -FilePath $exePath -ArgumentList $mode -WindowStyle Hidden -PassThru
$timeout = if ($Measure) { 360000 } else { 60000 }
if (!$testProcess.WaitForExit($timeout)) { throw "验收超时，进程 ID：$($testProcess.Id)。请检查窗口和日志。" }
if (!(Test-Path -LiteralPath $resultPath)) { throw '没有生成验收报告，请检查 release/verification-fatal.txt。' }
if ((Get-Item -LiteralPath $resultPath).LastWriteTime -lt $testStarted.AddSeconds(-2)) { throw '验收报告未更新，可能已有验收进程正在运行。' }
$reportText = Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8
Write-Output $reportText
$evidenceVersion = [Version](Get-Item -LiteralPath $exePath).VersionInfo.FileVersion
$evidencePath = Join-Path $taskRoot ('artifacts\v' + $evidenceVersion.ToString($(if ($evidenceVersion.Revision -eq 0) { 3 } else { 4 })))
New-Item -ItemType Directory -Path $evidencePath -Force | Out-Null
Copy-Item -LiteralPath $resultPath -Destination $evidencePath -Force
if (!$Measure) { Get-ChildItem (Join-Path $taskRoot ($ProgramDirectory+'\verification')) -Filter '*.png' | Copy-Item -Destination $evidencePath -Force }
if ($testProcess.ExitCode -ne 0 -or $reportText -match '(?m)^FAIL ') { throw '验收存在失败项，见 artifacts 中的报告。' }
if ($reportText -match '(?m)^NOT VERIFIED') { Write-Warning '存在因执行环境限制未验证的项目，请阅读报告。' }
