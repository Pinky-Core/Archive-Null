param([string]$InputDirectory,[string]$OutputDirectory,[string]$PythonExe,[switch]$IncludeTests,[switch]$Open)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$InputDirectory){$InputDirectory=Join-Path $env:USERPROFILE 'AppData/LocalLow/PinkyCore/Archive Null/Telemetry'}
if(!$OutputDirectory){$OutputDirectory=Join-Path $root 'Artifacts/TelemetryReport'}
if(!$PythonExe){
    $local=Get-ChildItem (Join-Path $root '.utmp/MediaTools/python') -Filter python.exe -Recurse -ErrorAction SilentlyContinue | Where-Object {$_.FullName -notlike '*Lib*'} | Select-Object -First 1
    if($local){$PythonExe=$local.FullName}else{$PythonExe='python'}
}
$arguments=@((Join-Path $PSScriptRoot 'Export-Telemetry.py'),'--input',$InputDirectory,'--output',$OutputDirectory)
if($IncludeTests){$arguments+='--include-tests'}
& $PythonExe @arguments
if($LASTEXITCODE -ne 0){throw 'No se pudo exportar la telemetría. Se necesita Python 3.10 o posterior.'}
if($Open){Start-Process (Join-Path $OutputDirectory 'index.html') -WindowStyle Hidden}
