param([Parameter(Mandatory=$true)][string]$FfmpegExe,[string]$OutputFile,[int]$PreviewChapter=-1,[string]$CaptureDirectory='Artifacts/FeedbackValidation',[string]$FrameDirectory='.utmp/FeedbackFrames')
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
if(!$OutputFile){$OutputFile=Join-Path $root 'Docs/Media/ArchiveNull_Explicativo.mp4'}
$chapters=Get-Content (Join-Path $PSScriptRoot 'ExplainerStoryboard.json') -Raw -Encoding UTF8 | ConvertFrom-Json
if($PreviewChapter -ge 0){if($PreviewChapter -ge $chapters.Count){throw 'Capítulo de vista previa inválido'};$chapters=@($chapters[$PreviewChapter])}
$scratch=Join-Path $root '.utmp/ExplainerVideo'
New-Item -ItemType Directory -Force -Path $scratch,(Split-Path $OutputFile -Parent)|Out-Null
$encoder=(Resolve-Path $FfmpegExe).Path
Push-Location $root
try {
    $concat=[Collections.Generic.List[string]]::new();$index=0
    foreach($chapter in $chapters){
        $image=Join-Path $root ($CaptureDirectory+'/'+$chapter.image+'.png')
        if(!(Test-Path -LiteralPath $image)){throw ('Falta la captura '+$image)}
        foreach($field in @('title','body','caption','view')){
            [IO.File]::WriteAllText((Join-Path $scratch ($field+'-'+$index+'.txt')),$chapter.$field,[Text.UTF8Encoding]::new($false))
        }
        $number=('{0:D2} / {1:D2}' -f ($index+1),$chapters.Count)
        [IO.File]::WriteAllText((Join-Path $scratch ('number-'+$index+'.txt')),$number,[Text.UTF8Encoding]::new($false))
        $seconds=[int]$chapter.seconds;$frames=$seconds*30
        $inputArgs=@('-loop','1','-i',$image)
        $visual="[0:v]scale=1320:742:force_original_aspect_ratio=decrease,pad=1320:742:(ow-iw)/2:(oh-ih)/2:color=0x090f16,setsar=1,zoompan=z='min(zoom+0.00004,1.012)':x='iw/2-iw/zoom/2':y='ih/2-ih/zoom/2':d=1:s=1320x742:fps=30,trim=duration=$seconds,setpts=PTS-STARTPTS[game]"
        if($chapter.frames){
            $pattern=$FrameDirectory+'/'+$chapter.frames+'-%03d.png'
            if(Test-Path ($FrameDirectory+'/'+$chapter.frames+'-000.png')){
                $inputArgs=@('-framerate','8','-i',$pattern)
                $visual="[0:v]scale=1320:742:force_original_aspect_ratio=decrease,pad=1320:742:(ow-iw)/2:(oh-ih)/2:color=0x090f16,setsar=1,fps=30,tpad=stop_mode=clone:stop_duration=$seconds,trim=duration=$seconds,setpts=PTS-STARTPTS[game]"
            }
        }
        $font="fontfile='C\:/Windows/Fonts/segoeui.ttf'"
        $bold="fontfile='C\:/Windows/Fonts/seguisb.ttf'"
        $filter="$visual;color=c=0x101820:s=1920x1080:r=30:d=$seconds[bg];[bg][game]overlay=x=540:y=183,drawbox=x=70:y=92:w=1780:h=1:color=0x304654:t=fill,drawtext=${font}:text='ARCHIVE  NULL  /  GUÍA DEL JUEGO':x=70:y=48:fontsize=23:fontcolor=0x9bacb8,drawtext=${font}:textfile='.utmp/ExplainerVideo/number-$index.txt':x=70:y=126:fontsize=25:fontcolor=0x89e7d3,drawtext=${bold}:textfile='.utmp/ExplainerVideo/title-$index.txt':x=70:y=190:fontsize=45:line_spacing=9:fontcolor=0xffffff,drawtext=${font}:textfile='.utmp/ExplainerVideo/body-$index.txt':x=70:y=412:fontsize=25:line_spacing=10:fontcolor=0xc6d3dd,drawtext=${font}:textfile='.utmp/ExplainerVideo/view-$index.txt':x=540:y=141:fontsize=20:fontcolor=0x89e7d3,drawtext=${font}:textfile='.utmp/ExplainerVideo/caption-$index.txt':x=70:y=964:fontsize=25:fontcolor=0xf0ca7e,drawbox=x=70:y=1024:w=1780:h=3:color=0x304654:t=fill,drawbox=x=70:y=1024:w='1780':h=3:color=0x89e7d3:t=fill,fade=t=in:st=0:d=0.28,fade=t=out:st=$($seconds-.28):d=0.28,format=yuv420p[out]"
        $segment=Join-Path $scratch ('segment-'+$index+'.mp4')
        & $encoder -hide_banner -loglevel error -y @inputArgs -filter_complex $filter -map '[out]' -t $seconds -an -c:v libx264 -preset veryfast -crf 21 -threads 2 $segment
        if($LASTEXITCODE -ne 0){throw ('Falló el capítulo '+$index)}
        $concat.Add("file 'segment-$index.mp4'");$index++
        Write-Output ('Capítulo '+$index+'/'+$chapters.Count+' exportado')
    }
    $manifest=Join-Path $scratch 'concat.txt'
    [IO.File]::WriteAllLines($manifest,$concat,[Text.UTF8Encoding]::new($false))
    & $encoder -hide_banner -loglevel error -y -f concat -safe 0 -i $manifest -c copy -an -movflags +faststart $OutputFile
    if($LASTEXITCODE -ne 0){throw 'Falló la unión del video'}
    Write-Output ('Video explicativo exportado: '+$OutputFile)
} finally {Pop-Location}
