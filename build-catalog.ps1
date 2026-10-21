param([string]$GameJar='D:\steam\steamapps\common\SlayTheSpire\desktop-1.0-original.jar',[string]$Jdk=$env:JAVA_HOME,[string]$Output='build\catalog-four-classes.json')
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
New-Item -ItemType Directory -Path 'build\catalog-probe' -Force | Out-Null
$taskSources=Get-ChildItem -LiteralPath 'tools\catalog-probe-src' -Recurse -Filter '*.java' | ForEach-Object FullName
& (Join-Path $Jdk 'bin\javac.exe') --release 8 -encoding UTF-8 -cp ($GameJar+';tools\asm-9.7.1.jar') -d 'build\catalog-probe' $taskSources
if($LASTEXITCODE -ne 0){throw 'Catalog tool compilation failed'}
& (Join-Path $Jdk 'bin\java.exe') '-Dfile.encoding=UTF-8' -cp ('build\catalog-probe;tools\asm-9.7.1.jar;'+$GameJar) CatalogProbe $GameJar $Output 'catalog.json'
if($LASTEXITCODE -ne 0){throw 'Catalog extraction failed; current release catalog unchanged'}
$taskEnglishOutput=[System.IO.Path]::ChangeExtension($Output,'en.json')
& (Join-Path $Jdk 'bin\java.exe') '-Dfile.encoding=UTF-8' -cp ('build\catalog-probe;tools\asm-9.7.1.jar;'+$GameJar) CatalogProbe $GameJar $taskEnglishOutput 'catalog.json' 'eng'
if($LASTEXITCODE -ne 0){throw 'English catalog extraction failed'}
