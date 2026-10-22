$ErrorActionPreference='Stop'
$frameworkPath=Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$toolDirectory=Join-Path $PSScriptRoot 'artifacts\asset-tool'
New-Item -ItemType Directory -Path $toolDirectory -Force | Out-Null
$toolExe=Join-Path $toolDirectory 'BuildBrand.exe'
& (Join-Path $frameworkPath 'csc.exe') /nologo /target:exe /platform:x64 "/out:$toolExe" "/reference:$(Join-Path $frameworkPath 'System.Xaml.dll')" "/reference:$(Join-Path $frameworkPath 'WPF\WindowsBase.dll')" "/reference:$(Join-Path $frameworkPath 'WPF\PresentationCore.dll')" "/reference:$(Join-Path $frameworkPath 'WPF\PresentationFramework.dll')" (Join-Path $PSScriptRoot 'tools\BuildBrand.cs')
if ($LASTEXITCODE -ne 0) { throw '图标生成器构建失败' }
& $toolExe (Join-Path $PSScriptRoot 'assets')
if ($LASTEXITCODE -ne 0) { throw '图标生成失败' }
Write-Output '已生成透明 PNG 和 9 个尺寸的 ICO'
