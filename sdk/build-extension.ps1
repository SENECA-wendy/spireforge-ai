param(
    [Parameter(Mandatory=$true)][string]$SourceDir,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z0-9_.-]{1,120}$')][string]$Id,
    [Parameter(Mandatory=$true)][ValidatePattern('^[A-Za-z_$][A-Za-z0-9_$.]*$')][string]$ClassName,
    [string]$Name = '',
    [string]$GameDir = 'D:\steam\steamapps\common\SlayTheSpire',
    [string]$Jdk = $env:JAVA_HOME,
    [string]$OutputDir = '',
    [switch]$Install
)
$ErrorActionPreference = 'Stop'
$taskSources = @(Get-ChildItem -LiteralPath $SourceDir -Recurse -Filter '*.java' | ForEach-Object FullName)
if ($taskSources.Count -eq 0) { throw '没有找到 Java 源码。' }
$taskSdk = Join-Path $PSScriptRoot 'card-extension-sdk.jar'
if (!(Test-Path -LiteralPath $taskSdk)) { throw '缺少 card-extension-sdk.jar。' }
$taskGameJar = Join-Path $GameDir 'desktop-1.0-original.jar'
if (!(Test-Path -LiteralPath $taskGameJar)) { $taskGameJar = Join-Path $GameDir 'desktop-1.0.jar' }
if ([string]::IsNullOrEmpty($OutputDir)) { $OutputDir = Join-Path $PSScriptRoot ('built\' + $Id + '-' + (Get-Date -Format 'yyyyMMdd-HHmmss')) }
$taskClasses = Join-Path $OutputDir 'classes'
New-Item -ItemType Directory -Path $taskClasses -Force | Out-Null
& (Join-Path $Jdk 'bin\javac.exe') --release 8 -encoding UTF-8 -cp "$taskGameJar;$taskSdk" -d $taskClasses $taskSources
if ($LASTEXITCODE -ne 0) { throw '扩展编译失败，没有安装。' }
$taskJarPath = Join-Path $OutputDir ($Id + '.jar')
& (Join-Path $Jdk 'bin\jar.exe') cf $taskJarPath -C $taskClasses .
if ($LASTEXITCODE -ne 0) { throw '扩展打包失败，没有安装。' }
if ([string]::IsNullOrEmpty($Name)) { $Name = $Id }
$taskManifest = @{ apiVersion=1; id=$Id; name=$Name; className=$ClassName; jar=($Id + '.jar'); sha256=(Get-FileHash -LiteralPath $taskJarPath).Hash.ToLowerInvariant(); enabled=$true }
$taskManifestPath = Join-Path $OutputDir ($Id + '.extension.json')
$taskManifest | ConvertTo-Json | Set-Content -LiteralPath $taskManifestPath -Encoding utf8NoBOM
if ($Install) {
    $taskDestination = Join-Path $GameDir 'sts-card-editor\extensions'
    if (Test-Path -LiteralPath (Join-Path $taskDestination ($Id + '.jar'))) {
        $taskRunning = Get-Process javaw,SlayTheSpire -ErrorAction SilentlyContinue | Where-Object { $_.ProcessName -eq 'SlayTheSpire' -or $_.MainWindowTitle -eq 'Slay the Spire' }
        if ($taskRunning) { throw '更新已加载的同 ID 扩展需要先退出游戏。构建结果已保留。' }
    }
    New-Item -ItemType Directory -Path $taskDestination -Force | Out-Null
    Copy-Item -LiteralPath $taskJarPath -Destination $taskDestination -Force
    Copy-Item -LiteralPath $taskManifestPath -Destination $taskDestination -Force
    Write-Output "Installed: $taskDestination"
}
Write-Output "Built: $taskJarPath"
Write-Output "Manifest: $taskManifestPath"
