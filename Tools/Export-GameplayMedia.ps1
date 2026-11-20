param([Parameter(Mandatory=$true)][string]$FfmpegExe)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
Push-Location $root
try {
    New-Item Docs/Media -ItemType Directory -Force|Out-Null
    New-Item .utmp/FeedbackVideo -ItemType Directory -Force|Out-Null
    function Run-FFmpeg([string[]]$Arguments) {& $FfmpegExe -hide_banner -loglevel error -y @Arguments;if($LASTEXITCODE -ne 0){throw 'Falló la exportación multimedia'}}
    foreach($entry in @(@('reconstruction-wave','reconstruccion'),@('echo','recuerdo-eco'))) {
        $pattern='.utmp/FeedbackFrames/'+$entry[0]+'-%03d.png'
        $filter='fps=8,scale=720:-2:flags=lanczos,split[a][b];[a]palettegen=max_colors=128[p];[b][p]paletteuse=dither=bayer:bayer_scale=3'
        Run-FFmpeg @('-framerate','8','-i',$pattern,'-filter_complex',$filter,'-loop','0',('Docs/Media/'+$entry[1]+'.gif'))
    }
    & (Join-Path $PSScriptRoot 'Export-ExplainerVideo.ps1') -FfmpegExe $FfmpegExe
    Write-Output 'Exportados dos GIF y un video explicativo sin pista de audio.'
} finally {Pop-Location}
