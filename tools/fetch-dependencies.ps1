$ErrorActionPreference='Stop'
$taskDestination=Join-Path $PSScriptRoot 'asm-9.7.1.jar'
$taskExpected='8CADD43AC5EB6D09DE05FAECCA38B917A040BB9139C7EDEB4CC81C740B713281'
if(Test-Path -LiteralPath $taskDestination) {
    if((Get-FileHash -LiteralPath $taskDestination -Algorithm SHA256).Hash -eq $taskExpected){Write-Output 'ASM 9.7.1 verified.';return}
    throw 'Existing ASM file does not match the pinned SHA-256. Inspect it before replacing it.'
}
$taskPending=$taskDestination+'.download'
try {
    Invoke-WebRequest -Uri 'https://repo.maven.apache.org/maven2/org/ow2/asm/asm/9.7.1/asm-9.7.1.jar' -OutFile $taskPending
    if((Get-FileHash -LiteralPath $taskPending -Algorithm SHA256).Hash -ne $taskExpected){throw 'ASM checksum mismatch.'}
    Move-Item -LiteralPath $taskPending -Destination $taskDestination
} finally {if(Test-Path -LiteralPath $taskPending){Remove-Item -LiteralPath $taskPending}}
