param(
    [Parameter(Mandatory=$true)][string]$GameDir,
    [Parameter(Mandatory=$true)][string]$Jdk,
    [string]$Version='1.7.0'
)
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
if(!(Test-Path -LiteralPath (Join-Path $Jdk 'bin\javac.exe'))){throw 'Pass the JDK installation folder, containing bin/javac.exe.'}
$taskGameJar=Join-Path $GameDir 'desktop-1.0-original.jar'
if(!(Test-Path -LiteralPath $taskGameJar)){$taskGameJar=Join-Path $GameDir 'desktop-1.0.jar'}
if(!(Test-Path -LiteralPath $taskGameJar)){throw 'The locally installed original game jar is required.'}
& '.\tools\fetch-dependencies.ps1'
& '.\build.ps1' -GameDir $GameDir -Jdk $Jdk -Version $Version -LayerVersion '1.6.0'
& '.\build-catalog.ps1' -GameJar $taskGameJar -Jdk $Jdk -Output 'build\portable-catalog.json'
Copy-Item -LiteralPath 'build\portable-catalog.json' -Destination 'catalog.json' -Force
Copy-Item -LiteralPath 'build\portable-catalog.en.json' -Destination 'catalog.en.json' -Force
Copy-Item -LiteralPath 'build\portable-catalog.json' -Destination 'dist\STSCardEditor\catalog.json' -Force
Copy-Item -LiteralPath 'build\portable-catalog.en.json' -Destination 'dist\STSCardEditor\catalog.en.json' -Force
& '.\package-release.ps1' -Version $Version
