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
    $segments=@(
        @('desktop','Oficina: leé el expediente y elegí una memoria.','frames'),
        @('reconstruction-wave','La memoria se reconstruye antes de comenzar la exploración.','frames'),
        @('house','Explorá la casa, agachate y examiná las evidencias.','still'),
        @('phone-voice','Consultá mensajes, notas de voz y llamadas del teléfono.','still'),
        @('echo','Algunos objetos evocan recuerdos en tercera persona.','frames'),
        @('salas-computer','En la oficina de Salas, revisá los documentos del ordenador.','still'),
        @('board','Elegí dos fotos para conectar las pruebas. Al apuntarlas se amplían.','still'),
        @('reconstruction','Contrastá las pruebas y reconstruí la secuencia de salida.','still'),
        @('office-return','Volvé a la oficina para organizar las pruebas y presentar el informe.','still')
    )
    $concat=[Collections.Generic.List[string]]::new();$index=0
    foreach($segment in $segments) {
        $caption=".utmp/FeedbackVideo/caption-$index.txt";[IO.File]::WriteAllText((Join-Path $root $caption),$segment[1],[Text.UTF8Encoding]::new($false))
        $filter="scale=1280:720:force_original_aspect_ratio=decrease,pad=1280:720:(ow-iw)/2:(oh-ih)/2:black,setsar=1,drawbox=x=0:y=626:w=1280:h=94:color=black@0.85:t=fill,drawtext=fontfile='C\:/Windows/Fonts/segoeui.ttf':textfile='$caption':fontcolor=white:fontsize=25:x=(w-tw)/2:y=658"
        $output=".utmp/FeedbackVideo/segment-$index.mp4"
        $inputArgs=if($segment[2] -eq 'frames') {@('-framerate','8','-i',('.utmp/FeedbackFrames/'+$segment[0]+'-%03d.png'))} else {@('-loop','1','-i',('Artifacts/FeedbackValidation/'+$segment[0]+'.png'),'-t','4')}
        Run-FFmpeg ($inputArgs+@('-vf',$filter,'-r','30','-an','-c:v','libx264','-preset','fast','-crf','22','-pix_fmt','yuv420p',$output))
        $concat.Add("file 'segment-$index.mp4'");$index++
    }
    [IO.File]::WriteAllLines((Join-Path $root '.utmp/FeedbackVideo/concat.txt'),$concat,[Text.UTF8Encoding]::new($false))
    Run-FFmpeg @('-f','concat','-safe','0','-i','.utmp/FeedbackVideo/concat.txt','-c','copy','-an','-movflags','+faststart','Docs/Media/ArchiveNull_Explicativo_SinAudio.mp4')
    Write-Output 'Exportados dos GIF y un video explicativo sin pista de audio.'
} finally {Pop-Location}
