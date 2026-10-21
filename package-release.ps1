param([string]$Version='1.7.0')
$ErrorActionPreference='Stop'
Set-Location $PSScriptRoot
$taskRelease=Join-Path $PSScriptRoot 'dist\STSCardEditor'
$taskStage=Join-Path $PSScriptRoot ('build\release-'+[guid]::NewGuid().ToString('N'))
$taskPackage=Join-Path $taskStage 'SpireForgeAI'
New-Item -ItemType Directory -Path $taskPackage | Out-Null
foreach($taskName in @(('SpireForgeAI-v'+$Version+'.exe'),'catalog.json','catalog.en.json','tools','sdk')) {
    Copy-Item -LiteralPath (Join-Path $taskRelease $taskName) -Destination $taskPackage -Recurse
}
foreach($taskDoc in @('README.md','README.zh-CN.md','THIRD_PARTY_NOTICES.md')) {Copy-Item -LiteralPath (Join-Path $PSScriptRoot $taskDoc) -Destination $taskPackage}
New-Item -ItemType Directory -Path (Join-Path $taskPackage 'data') | Out-Null
'{"schemaVersion":2,"overrides":{},"customCards":[],"deletedCustomCardIds":[]}' | Set-Content -LiteralPath (Join-Path $taskPackage 'data\profile.json') -Encoding utf8NoBOM
$taskZip=Join-Path $PSScriptRoot ('dist\SpireForgeAI-v'+$Version+'-portable.zip')
Compress-Archive -LiteralPath $taskPackage -DestinationPath $taskZip -Force
Add-Type -AssemblyName System.IO.Compression.FileSystem
$taskArchive=[System.IO.Compression.ZipFile]::OpenRead($taskZip)
try {
    $taskExecutables=@($taskArchive.Entries | Where-Object FullName -Like '*.exe')
    if($taskExecutables.Count -ne 1 -or $taskExecutables[0].Name -ne ('SpireForgeAI-v'+$Version+'.exe')){throw 'Unexpected executables in release.'}
    if($taskArchive.Entries | Where-Object {$_.FullName -match '(settings\.json|ui-language\.json|app\.log|generated/|history/|backups/|run-card-pools/|\.autosave|desktop-1\.0)'}){throw 'Private or game files found in release.'}
    $taskProfile=@($taskArchive.Entries | Where-Object FullName -Like '*/data/profile.json')[0]
    $taskReader=[System.IO.StreamReader]::new($taskProfile.Open())
    try{$taskDoc=$taskReader.ReadToEnd() | ConvertFrom-Json}finally{$taskReader.Dispose()}
    if($taskDoc.customCards.Count -ne 0){throw 'Release contains a user card library.'}
}finally{$taskArchive.Dispose()}
Get-FileHash -LiteralPath $taskZip -Algorithm SHA256 | Select-Object Path,Hash
