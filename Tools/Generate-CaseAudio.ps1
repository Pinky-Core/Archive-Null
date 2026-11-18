param([Parameter(Mandatory=$true)][string]$UvExe,[Parameter(Mandatory=$true)][string]$FfmpegExe)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$env:UV_CACHE_DIR=Join-Path $root '.utmp/MediaTools/uv-cache'
$env:UV_PYTHON_INSTALL_DIR=Join-Path $root '.utmp/MediaTools/python'
& $UvExe run --python 3.12 --with edge-tts python (Join-Path $PSScriptRoot 'Generate-CaseAudio.py') --ffmpeg $FfmpegExe
if($LASTEXITCODE -ne 0){throw 'No se pudieron generar las voces del caso.'}