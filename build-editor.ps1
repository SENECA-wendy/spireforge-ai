param([string]$Version='1.7.0')
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
Copy-Item -LiteralPath 'tools\creative-probe-src' -Destination 'dist\STSCardEditor\tools' -Recurse -Force
$taskSources=Get-ChildItem 'app\*.cs' | Where-Object Name -NE 'ParserTest.cs' | ForEach-Object FullName
$taskExe=Join-Path $PSScriptRoot ('dist\STSCardEditor\SpireForgeAI-v'+$Version+'.exe')
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:winexe /codepage:65001 "/out:$taskExe" /win32manifest:app/app.manifest /reference:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Security.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll $taskSources
if($LASTEXITCODE -ne 0) { throw 'Editor compilation failed' }
Write-Output "Built editor only; installed runtime unchanged: $taskExe"
