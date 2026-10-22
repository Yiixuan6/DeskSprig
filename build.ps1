[CmdletBinding()]
param([string]$OutputDirectory = 'release')
$ErrorActionPreference = 'Stop'
$taskRoot = $PSScriptRoot
$frameworkPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compilerPath = Join-Path $frameworkPath 'csc.exe'
$wpfPath = Join-Path $frameworkPath 'WPF'
if (!(Test-Path -LiteralPath $compilerPath)) { throw '未找到 .NET Framework x64 编译器，请确认已安装 .NET Framework 4.8。' }
$releaseKey = Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full'
if ($releaseKey.Release -lt 528040) { throw '需要 .NET Framework 4.8 或更新的 4.x 版本。' }
$outputPath = Join-Path $taskRoot $OutputDirectory
New-Item -ItemType Directory -Path $outputPath -Force | Out-Null
$exePath = Join-Path $outputPath '桌面小组件.exe'
$versionMatch = [regex]::Match((Get-Content -LiteralPath (Join-Path $taskRoot 'src\AssemblyInfo.cs') -Raw -Encoding UTF8), 'const string Value = "(\d+\.\d+\.\d+\.\d+)"')
if (!$versionMatch.Success) { throw '无法读取 AppVersion.Value。' }
[xml]$manifestTemplate = Get-Content -LiteralPath (Join-Path $taskRoot 'app.manifest') -Raw -Encoding UTF8
$manifestTemplate.assembly.assemblyIdentity.SetAttribute('version',$versionMatch.Groups[1].Value)
$generatedManifestPath = Join-Path $outputPath 'app.generated.manifest'
$manifestTemplate.Save($generatedManifestPath)
$references = @('System.dll','System.Core.dll','System.Runtime.Serialization.dll','System.Xml.dll','System.Xml.Linq.dll','System.Xaml.dll') | ForEach-Object { '/reference:' + (Join-Path $frameworkPath $_) }
$references += @('WindowsBase.dll','PresentationCore.dll','PresentationFramework.dll') | ForEach-Object { '/reference:' + (Join-Path $wpfPath $_) }
$sources = Get-ChildItem -LiteralPath (Join-Path $taskRoot 'src') -Filter '*.cs' | ForEach-Object FullName
if (!(Test-Path (Join-Path $taskRoot 'assets\app.ico'))) { & (Join-Path $taskRoot 'build-assets.ps1') }
$resources = @('/resource:' + (Join-Path $taskRoot 'assets\app.png') + ',Brand.png')
$resources += '/resource:' + (Join-Path $taskRoot 'assets\icons\LICENSE') + ',FluentIcons.LICENSE'
$resources += Get-ChildItem (Join-Path $taskRoot 'assets\icons') -Filter '*.svg' | ForEach-Object { '/resource:' + $_.FullName + ',Icons.' + $_.Name }
& $compilerPath /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /warn:4 "/out:$exePath" "/win32manifest:$generatedManifestPath" "/win32icon:$(Join-Path $taskRoot 'assets\app.ico')" $references $resources $sources
if ($LASTEXITCODE -ne 0) { throw '编译失败，见上方诊断。' }
Copy-Item -LiteralPath (Join-Path $taskRoot 'app.config') -Destination ($exePath + '.config') -Force
Copy-Item -LiteralPath (Join-Path $taskRoot 'tools\打开管理页.cmd') -Destination $outputPath -Force
if (Test-Path -LiteralPath (Join-Path $taskRoot '使用说明.md')) { Copy-Item -LiteralPath (Join-Path $taskRoot '使用说明.md') -Destination $outputPath -Force }
if (Test-Path -LiteralPath (Join-Path $taskRoot '第三方许可.md')) { Copy-Item -LiteralPath (Join-Path $taskRoot '第三方许可.md') -Destination $outputPath -Force }
Write-Output "构建成功：$exePath"
