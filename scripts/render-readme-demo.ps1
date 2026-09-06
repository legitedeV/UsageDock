param([string]$ScreenshotDirectory=(Join-Path $PSScriptRoot '..\docs\screenshots'),[string]$OutputPath=(Join-Path $PSScriptRoot '..\docs\media\demo.gif'))
$ErrorActionPreference='Stop'
$ffmpeg=(Get-Command ffmpeg -ErrorAction Stop).Source
$source=(Resolve-Path -LiteralPath $ScreenshotDirectory).Path
$output=[IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output))|Out-Null
$scratch=Join-Path ([IO.Path]::GetTempPath()) ('UsageDock-readme-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($scratch)|Out-Null
function Invoke-FFmpeg([string[]]$Arguments){ & $ffmpeg -hide_banner -loglevel error -y @Arguments; if($LASTEXITCODE -ne 0){throw "ffmpeg failed: $LASTEXITCODE"} }
try {
    # Inputs must be existing screenshots from the application's synthetic --screenshot mode.
    $scenes=@(
        @{File='dashboard.png';Label='01 / All your accounts';Detail='Separate limits. One clear view.'},
        @{File='statistics.png';Label='02 / Usage at a glance';Detail='Subscription windows and API spending, kept separate.'},
        @{File='resets.png';Label='03 / Banked Codex resets';Detail='Check availability and expiry before you confirm.'},
        @{File='widget.png';Label='04 / Desktop widget';Detail='Pinned accounts. Dark and light themes.'},
        @{File='dashboard-light.png';Label='05 / Make it yours';Detail='Switch themes instantly in the dashboard or widget.'}
    )
    $font=(Join-Path $env:WINDIR 'Fonts\segoeui.ttf').Replace('\','/').Replace(':','\:')
    $bold=(Join-Path $env:WINDIR 'Fonts\seguisb.ttf').Replace('\','/').Replace(':','\:')
    $clips=@()
    for($i=0;$i -lt $scenes.Count;$i++){
        $scene=$scenes[$i];$inputFile=Join-Path $source $scene.File
        if(!(Test-Path -LiteralPath $inputFile)){throw "Missing screenshot: $inputFile"}
        $png=Join-Path $scratch "scene-$i.png"
        $frame="drawbox=x=24:y=57:w=912:h=2:color=0x3CD3AD:t=fill,drawtext=fontfile='$bold':text='$($scene.Label)':x=24:y=20:fontsize=20:fontcolor=0xE3E8EB,drawtext=fontfile='$font':text='UsageDock':x=w-tw-24:y=24:fontsize=16:fontcolor=0xA2ADB8,drawtext=fontfile='$font':text='$($scene.Detail)':x=24:y=697:fontsize=17:fontcolor=0xE3E8EB,drawtext=fontfile='$font':text='Synthetic demo / Polish UI':x=24:y=726:fontsize=13:fontcolor=0xA2ADB8"
        if($scene.File -eq 'widget.png'){
            $light=Join-Path $source 'widget-light.png'
            if(!(Test-Path -LiteralPath $light)){throw 'Missing light widget screenshot'}
            $filter="color=c=0x0B1217:s=960x752[bg];[0:v]format=rgba[a];[1:v]format=rgba[b];[bg][a]overlay=x=124:y=104[tmp];[tmp][b]overlay=x=568:y=104,$frame"
            Invoke-FFmpeg @('-i',$inputFile,'-i',$light,'-filter_complex',$filter,'-frames:v','1',$png)
        }else{
            $filter="scale=912:612:force_original_aspect_ratio=decrease:flags=lanczos,pad=960:752:(ow-iw)/2:72:color=0x0B1217,$frame"
            Invoke-FFmpeg @('-i',$inputFile,'-vf',$filter,'-frames:v','1',$png)
        }
        $clip=Join-Path $scratch "scene-$i.mkv"
        # Fades clearly separate static scenes. This is a screenshot tour, not simulated interaction.
        $fades=if($i -eq 0){'fade=t=out:st=3.75:d=0.25'}else{'fade=t=in:st=0:d=0.25,fade=t=out:st=3.75:d=0.25'}
        Invoke-FFmpeg @('-loop','1','-framerate','8','-i',$png,'-t','4','-vf',$fades,'-c:v','ffv1','-pix_fmt','bgr0',$clip)
        $clips+=$clip
    }
    $concat=Join-Path $scratch 'scenes.txt'
    [IO.File]::WriteAllLines($concat,($clips|ForEach-Object{"file '"+$_.Replace('\','/').Replace("'","'\''")+"'"}),[Text.UTF8Encoding]::new($false))
    Invoke-FFmpeg @('-f','concat','-safe','0','-i',$concat,'-filter_complex','split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',$output)
    $file=Get-Item -LiteralPath $output
    if($file.Length -gt 4MB){throw "GIF exceeds 4 MiB: $($file.Length) bytes"}
    Write-Output "Created $($file.FullName) ($($file.Length) bytes, 960x752, 20 seconds)."
}finally{
    $resolved=[IO.Path]::GetFullPath($scratch);$tempRoot=[IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    if($resolved.StartsWith($tempRoot,[StringComparison]::OrdinalIgnoreCase) -and [IO.Path]::GetFileName($resolved).StartsWith('UsageDock-readme-')){Remove-Item -LiteralPath $resolved -Recurse -Force}
}
