param([string]$Jdk = $env:JAVA_HOME, [string]$GameJar = 'D:\steam\steamapps\common\SlayTheSpire\desktop-1.0-original.jar')
$ErrorActionPreference = 'Stop'
$env:JAVA_HOME=$Jdk
$env:SPIREFORGE_TEST_GAME_DIR=Split-Path $GameJar
Set-Location (Split-Path $PSScriptRoot)
New-Item -ItemType Directory -Path 'build\tests-java','build\tests-work','build\tests-editor' -Force | Out-Null
.\build-catalog.ps1 -GameJar $GameJar -Jdk $Jdk -Output 'build\catalog-regression.json'
$taskFixtures = Get-ChildItem 'tests\fixtures' -Recurse -Filter '*.java' | ForEach-Object FullName
& (Join-Path $Jdk 'bin\javac.exe') --release 8 -encoding UTF-8 -cp $GameJar -d 'build\tests-java' $taskFixtures 'src\java\com\stseditor\runtime\CardSpec.java' 'src\java\com\stseditor\runtime\LiveDeckService.java' 'src\java\com\stseditor\runtime\ZeroCostPower.java' 'src\java\com\stseditor\runtime\RunCardPoolService.java' 'src\java\com\stseditor\runtime\LiveEditSafety.java' 'tests\LiveDeckTest.java' 'tests\ZeroCostTest.java' 'tests\RunCardPoolTest.java' 'src\java\com\stseditor\runtime\LiveCostPolicy.java' 'tests\LiveCostTest.java'
if ($LASTEXITCODE -ne 0) { throw 'Java tests compilation failed' }
$taskClasses = Join-Path (Get-Location) 'build\tests-java'
Push-Location 'build\tests-work'
try {
    & (Join-Path $Jdk 'bin\java.exe') -cp "$taskClasses;$GameJar" LiveDeckTest; if ($LASTEXITCODE -ne 0) { throw 'Java tests failed' }
    & (Join-Path $Jdk 'bin\java.exe') -cp "$taskClasses;$GameJar" ZeroCostTest; if ($LASTEXITCODE -ne 0) { throw 'Zero-cost tests failed' }
    & (Join-Path $Jdk 'bin\java.exe') -cp "$taskClasses;$GameJar" RunCardPoolTest; if ($LASTEXITCODE -ne 0) { throw 'Run-card-pool tests failed' }
    & (Join-Path $Jdk 'bin\java.exe') -cp "$taskClasses;$GameJar" LiveCostTest; if ($LASTEXITCODE -ne 0) { throw 'Live-cost tests failed' }
}
finally { Pop-Location }
Copy-Item -LiteralPath 'build\catalog-regression.json' -Destination 'build\tests-editor\catalog.json' -Force
Copy-Item -LiteralPath 'build\catalog-regression.en.json' -Destination 'build\tests-editor\catalog.en.json' -Force
$taskSources = Get-ChildItem 'app\*.cs' | Where-Object Name -NE 'ParserTest.cs' | ForEach-Object FullName
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /main:EditorTests /codepage:65001 /out:build/tests-editor/EditorTests.exe /reference:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Security.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll $taskSources 'tests\EditorTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Editor tests compilation failed' }
& '.\build\tests-editor\EditorTests.exe'
if ($LASTEXITCODE -ne 0) { throw 'Editor tests failed' }
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /main:LiveDefinitionSyncTests /codepage:65001 /out:build/tests-editor/LiveDefinitionSyncTests.exe /reference:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Security.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll $taskSources 'tests\LiveDefinitionSyncTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Live-definition tests compilation failed' }
& '.\build\tests-editor\LiveDefinitionSyncTests.exe'
if ($LASTEXITCODE -ne 0) { throw 'Live-definition tests failed' }
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /main:NativeLiveEditTests /codepage:65001 /out:build/tests-editor/NativeLiveEditTests.exe /reference:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Security.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll $taskSources 'tests\NativeLiveEditTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Native live-edit tests compilation failed' }
& '.\build\tests-editor\NativeLiveEditTests.exe'
if ($LASTEXITCODE -ne 0) { throw 'Native live-edit tests failed' }
New-Item -ItemType Directory -Path 'build\tests-editor\tools' -Force | Out-Null
Copy-Item -LiteralPath 'tools\creative-probe-src' -Destination 'build\tests-editor\tools' -Recurse -Force
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /main:CreativeValidationTests /codepage:65001 /out:build/tests-editor/CreativeValidationTests.exe /reference:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Security.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll $taskSources 'tests\CreativeValidationTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Creative-generation tests compilation failed' }
& '.\build\tests-editor\CreativeValidationTests.exe'
if ($LASTEXITCODE -ne 0) { throw 'Creative-generation tests failed' }
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /main:CustomCardRevisionTests /codepage:65001 /out:build/tests-editor/CustomCardRevisionTests.exe /reference:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Security.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll $taskSources 'tests\CustomCardRevisionTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Custom-card revision tests compilation failed' }
& '.\build\tests-editor\CustomCardRevisionTests.exe'
if ($LASTEXITCODE -ne 0) { throw 'Custom-card revision tests failed' }
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /main:FourProfessionTests /codepage:65001 /out:build/tests-editor/FourProfessionTests.exe /reference:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Security.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll $taskSources 'tests\FourProfessionTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Four-profession tests compilation failed' }
& '.\build\tests-editor\FourProfessionTests.exe'
if ($LASTEXITCODE -ne 0) { throw 'Four-profession tests failed' }
& 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe' /nologo /target:exe /main:LocalizationTests /codepage:65001 /out:build/tests-editor/LocalizationTests.exe /reference:System.dll,System.Core.dll,System.Windows.Forms.dll,System.Drawing.dll,System.Web.Extensions.dll,System.Security.dll,System.IO.Compression.dll,System.IO.Compression.FileSystem.dll $taskSources 'tests\LocalizationTests.cs'
if ($LASTEXITCODE -ne 0) { throw 'Localization test compilation failed' }
& '.\build\tests-editor\LocalizationTests.exe'
if ($LASTEXITCODE -ne 0) { throw 'Localization tests failed' }
New-Item -ItemType Directory -Path 'build\extension-probe', 'build\tests-extensions-work' -Force | Out-Null
$taskRuntimeClasses = Join-Path (Get-Location) 'build\classes-runtime'
& (Join-Path $Jdk 'bin\javac.exe') --release 8 -encoding UTF-8 -cp "$taskRuntimeClasses;$GameJar" -d 'build\extension-probe' 'tests\extension-src\user\tests\ProbeExtension.java'
if ($LASTEXITCODE -ne 0) { throw 'Extension probe compilation failed' }
& (Join-Path $Jdk 'bin\jar.exe') cf 'build\probe-extension.jar' -C 'build\extension-probe' .
if ($LASTEXITCODE -ne 0) { throw 'Extension probe packaging failed' }
& (Join-Path $Jdk 'bin\javac.exe') --release 8 -encoding UTF-8 -cp "$taskRuntimeClasses;$GameJar" -d 'build\tests-java' 'tests\ExtensionRegistryTest.java'
if ($LASTEXITCODE -ne 0) { throw 'Extension registry test compilation failed' }
$taskProbeJar = Join-Path (Get-Location) 'build\probe-extension.jar'
Push-Location 'build\tests-extensions-work'
try {
    & (Join-Path $Jdk 'bin\java.exe') -cp "$taskClasses;$taskRuntimeClasses;$GameJar" ExtensionRegistryTest $taskProbeJar
    if ($LASTEXITCODE -ne 0) { throw 'Extension registry tests failed' }
} finally { Pop-Location }
New-Item -ItemType Directory -Path 'build\patch-linkage' -Force | Out-Null
& (Join-Path $Jdk 'bin\javac.exe') --release 8 -d 'build\patch-linkage' 'tests\PatchLinkageTest.java'
if ($LASTEXITCODE -ne 0) { throw 'Patched linkage test compilation failed' }
$taskGameJava = Join-Path (Split-Path $GameJar) 'jre\bin\java.exe'
& $taskGameJava -Xverify:all -cp "build\patch-linkage;build\patch-v1.6.0.jar;$GameJar" PatchLinkageTest
if ($LASTEXITCODE -ne 0) { throw 'Patched game classes failed Java 8 verification' }
New-Item -ItemType Directory -Path 'build\real-color-tests','build\tests-color-work' -Force | Out-Null
& (Join-Path $Jdk 'bin\javac.exe') --release 8 -cp "$taskRuntimeClasses;$GameJar" -d 'build\real-color-tests' 'tests\RealColorValidationTest.java'
if ($LASTEXITCODE -ne 0) { throw 'Actual runtime color test compilation failed' }
$taskColorPath=(Join-Path (Get-Location) 'build\real-color-tests')+';'+(Join-Path (Get-Location) 'build\catalog-probe')+';'+$taskRuntimeClasses+';'+$GameJar
Push-Location 'build\tests-color-work'
try { & $taskGameJava -Xverify:all -cp $taskColorPath RealColorValidationTest; if($LASTEXITCODE -ne 0){throw 'Actual runtime color checks failed'} } finally { Pop-Location }
