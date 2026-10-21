param(
    [string]$GameDir = 'D:\steam\steamapps\common\SlayTheSpire',
    [string]$Jdk = $env:JAVA_HOME,
    [string]$Version = '1.7.0',
    [string]$LayerVersion = '1.6.0'
)
$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
$taskJar = Join-Path $GameDir 'desktop-1.0-original.jar'
if (!(Test-Path -LiteralPath $taskJar)) { $taskJar = Join-Path $GameDir 'desktop-1.0.jar' }
$taskJava = Join-Path $Jdk 'bin\java.exe'
$taskJavac = Join-Path $Jdk 'bin\javac.exe'
$taskJarTool = Join-Path $Jdk 'bin\jar.exe'
$taskCsc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
New-Item -ItemType Directory -Path 'build\classes-runtime','build\classes-tools','dist\STSCardEditor\tools\runtime-classes' -Force | Out-Null
Copy-Item -LiteralPath 'tools\creative-probe-src' -Destination 'dist\STSCardEditor\tools' -Recurse -Force
$taskSources = Get-ChildItem 'src\java\com\stseditor\runtime\*.java' | ForEach-Object FullName
& $taskJavac --release 8 -encoding UTF-8 -cp $taskJar -d 'build\classes-runtime' $taskSources
if ($LASTEXITCODE -ne 0) { throw 'Runtime compilation failed' }
& $taskJavac --release 8 -encoding UTF-8 -cp 'tools\asm-9.7.1.jar' -d 'build\classes-tools' 'src\java\com\stseditor\patcher\Patcher.java'
if ($LASTEXITCODE -ne 0) { throw 'Patcher compilation failed' }
& $taskJarTool cf 'dist\STSCardEditor\tools\patcher.jar' -C 'build\classes-tools' .
if ($LASTEXITCODE -ne 0) { throw 'Patcher packaging failed' }
Copy-Item -LiteralPath 'build\classes-runtime\com' -Destination 'dist\STSCardEditor\tools\runtime-classes' -Recurse -Force
$taskCs = Get-ChildItem 'app\*.cs' | Where-Object Name -NE 'ParserTest.cs' | ForEach-Object FullName
$taskExe = 'dist\STSCardEditor\SpireForgeAI-v' + $Version + '.exe'
& $taskCsc /nologo /target:winexe /codepage:65001 "/out:$taskExe" /win32manifest:app/app.manifest /reference:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Security.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll $taskCs
if ($LASTEXITCODE -ne 0) { throw 'Editor compilation failed' }
& $taskJava -cp 'dist\STSCardEditor\tools\patcher.jar;tools\asm-9.7.1.jar' com.stseditor.patcher.Patcher $taskJar 'build\classes-runtime' ('build\patch-v' + $LayerVersion + '.jar')
if ($LASTEXITCODE -ne 0) { throw 'Patch generation failed' }
& $taskJarTool cf 'sdk\card-extension-sdk.jar' -C 'build\classes-runtime' .
if ($LASTEXITCODE -ne 0) { throw 'Extension SDK packaging failed' }
Copy-Item -LiteralPath 'sdk' -Destination 'dist\STSCardEditor' -Recurse -Force
New-Item -ItemType Directory -Path 'build\classes-bootstrap' -Force | Out-Null
& $taskJavac --release 8 -encoding UTF-8 -cp $taskJar -d 'build\classes-bootstrap' 'src\java\com\stseditor\bootstrap\Bootstrap.java'
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap compilation failed' }
"Manifest-Version: 1.0`r`nMain-Class: com.stseditor.bootstrap.Bootstrap`r`nClass-Path: sts-card-editor/patch.jar desktop-1.0-original.jar`r`n`r`n" | Set-Content -LiteralPath 'build\bootstrap-manifest.mf' -Encoding ascii
& $taskJarTool cfm 'dist\STSCardEditor\tools\wrapper.jar' 'build\bootstrap-manifest.mf' -C 'build\classes-bootstrap' .
if ($LASTEXITCODE -ne 0) { throw 'Bootstrap packaging failed' }
Copy-Item -LiteralPath 'tools\asm-9.7.1.jar' -Destination 'dist\STSCardEditor\tools\asm-9.7.1.jar' -Force
Write-Output "Build completed: $taskExe"
