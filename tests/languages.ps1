[CmdletBinding()]
param([string]$ProgramDirectory='release')
$ErrorActionPreference='Stop'
$taskRoot=Split-Path -Parent $PSScriptRoot
$programPath=Join-Path $taskRoot $ProgramDirectory
$exePath=Join-Path $programPath '桌面小组件.exe'
if(!(Test-Path -LiteralPath $exePath)){throw '请先构建指定程序目录。'}
$frameworkPath=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319'
$references=@('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xaml.dll') | ForEach-Object {'/reference:'+(Join-Path $frameworkPath $_)}
$references+=@('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object {'/reference:'+(Join-Path (Join-Path $frameworkPath 'WPF') $_)}
$references+='/reference:'+$exePath
$probePath=Join-Path $programPath 'LanguageProbe.exe'
& (Join-Path $frameworkPath 'csc.exe') /nologo /target:winexe /platform:x64 /codepage:65001 ("/out:$probePath") ('/win32manifest:'+(Join-Path $taskRoot 'app.manifest')) $references (Join-Path $taskRoot 'tools/LanguageProbe.cs')
if($LASTEXITCODE -ne 0){throw '语言恢复探针编译失败。'}
Copy-Item -LiteralPath (Join-Path $taskRoot 'app.config') -Destination ($probePath+'.config') -Force
$version=[Version](Get-Item -LiteralPath $exePath).VersionInfo.FileVersion
$evidencePath=Join-Path $taskRoot ('artifacts/v'+$version.ToString(3))
New-Item -ItemType Directory -Path $evidencePath -Force | Out-Null
$runId=[Guid]::NewGuid().ToString('N')
foreach($language in @('en-US','zh-TW','system')){
    $dataPath=Join-Path $programPath ('language-probe-data-'+$runId+'-'+$language)
    $resultPath=Join-Path $evidencePath ('language-restart-'+$language+'.txt')
    foreach($mode in @('prepare','read')){
        $arguments=@($mode,$language,('"'+$dataPath+'"'),('"'+$resultPath+'"'))
        $process=Start-Process -FilePath $probePath -ArgumentList $arguments -WindowStyle Hidden -PassThru
        if(!$process.WaitForExit(15000)){throw "语言探针超时：$language/$mode"}
        if($process.ExitCode -ne 0){throw "语言探针退出异常：$language/$mode"}
    }
    $result=Get-Content -LiteralPath $resultPath -Raw -Encoding UTF8
    Write-Output $result
    if($result -notmatch '^PASS '){throw '语言另进程恢复失败。'}
}
