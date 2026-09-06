param(
    [ValidateSet('pl','en','de','fr','es')][string]$Language='pl',
    [string]$ScreenshotDirectory,
    [string]$OutputPath
)
$ErrorActionPreference='Stop'
$root=[IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$ffmpeg=(Get-Command ffmpeg -ErrorAction Stop).Source
$ffprobe=(Get-Command ffprobe -ErrorAction Stop).Source
if(!$ScreenshotDirectory){
    if($Language -ne 'pl'){throw 'Supply -ScreenshotDirectory with verified app renders in the selected language.'}
    $ScreenshotDirectory=Join-Path $root 'docs/screenshots'
}
if(!$OutputPath){$OutputPath=Join-Path $root "docs/media/demo-$Language.gif"}
$source=(Resolve-Path -LiteralPath $ScreenshotDirectory).Path
$output=[IO.Path]::GetFullPath($OutputPath)
[IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($output))|Out-Null
$artifactRoot=Join-Path $root 'artifacts/readme-demo'
$scratch=Join-Path $artifactRoot ($Language+'-'+[guid]::NewGuid().ToString('N'))
[IO.Directory]::CreateDirectory($scratch)|Out-Null
$completed=$false
function Invoke-FFmpeg([string[]]$Arguments){
    & $ffmpeg -hide_banner -loglevel error -y @Arguments
    if($LASTEXITCODE -ne 0){throw "ffmpeg failed: $LASTEXITCODE"}
}
function Find-Screenshot([string[]]$Candidates){
    foreach($name in $Candidates){$path=Join-Path $source $name;if(Test-Path -LiteralPath $path){return $path}}
    throw "Missing screenshot in ${source}: $($Candidates -join ' or ')"
}
function Filter-Path([string]$Path){return $Path.Replace('\','/').Replace(':','\:').Replace("'","'\''")}
function Text-File([string]$Name,[string]$Text){
    $path=Join-Path $scratch $Name
    [IO.File]::WriteAllText($path,$Text,[Text.UTF8Encoding]::new($false))
    return (Filter-Path $path)
}
$copy=@{
    pl=@{
        Titles=@('01 / Wszystkie konta','02 / Zużycie w skrócie','03 / Zapasowe resety Codex','04 / Widżet pulpitu','05 / Jasny lub ciemny')
        Details=@('Osobne limity. Jeden czytelny widok.','Okna abonamentów i koszty API pozostają osobne.','Sprawdź dostępność i ważność przed potwierdzeniem.','Przypięte konta. Jasny i ciemny motyw.','Zmień motyw od razu w panelu lub widżecie.')
        Footer='Dane demonstracyjne / Polski interfejs'
    }
    en=@{
        Titles=@('01 / All your accounts','02 / Usage at a glance','03 / Banked Codex resets','04 / Desktop widget','05 / Light or dark')
        Details=@('Separate limits. One clear view.','Subscription windows and API spending stay separate.','Check availability and expiry before confirming.','Pinned accounts. Dark and light themes.','Switch themes instantly in the dashboard or widget.')
        Footer='Demo data / English interface'
    }
    de=@{
        Titles=@('01 / Alle Konten','02 / Nutzung im Überblick','03 / Codex-Reserve-Resets','04 / Desktop-Widget','05 / Hell oder dunkel')
        Details=@('Getrennte Limits. Eine klare Übersicht.','Abonnement-Zeitfenster und API-Kosten bleiben getrennt.','Verfügbarkeit und Gültigkeit vor der Bestätigung prüfen.','Angeheftete Konten. Helles und dunkles Design.','Design sofort in der Übersicht oder im Widget wechseln.')
        Footer='Demodaten / Deutsche Oberfläche'
    }
    fr=@{
        Titles=@('01 / Tous vos comptes','02 / Utilisation en un coup d''œil','03 / Réinitialisations Codex en réserve','04 / Widget de bureau','05 / Clair ou sombre')
        Details=@('Des limites distinctes. Une vue claire.','Fenêtres d''abonnement et dépenses API restent séparées.','Vérifiez la disponibilité et l''expiration avant de confirmer.','Comptes épinglés. Thèmes clair et sombre.','Changez de thème dans le tableau de bord ou le widget.')
        Footer='Données fictives / Interface française'
    }
    es=@{
        Titles=@('01 / Todas tus cuentas','02 / Uso de un vistazo','03 / Reinicios de reserva de Codex','04 / Miniwidget de escritorio','05 / Claro u oscuro')
        Details=@('Límites separados. Una vista clara.','Las ventanas de suscripción y los gastos de API van separados.','Comprueba la disponibilidad y la caducidad antes de confirmar.','Cuentas fijadas. Temas claro y oscuro.','Cambia el tema al instante en el panel o el miniwidget.')
        Footer='Datos de demostración / Interfaz en español'
    }
}[$Language]
try {
    # Use genuine synthetic app screenshots. Localized verified matrices and legacy PL names are supported.
    $scenes=@(
        @{File=(Find-Screenshot @('dashboard-dark.png','dashboard.png'));Widget=$false},
        @{File=(Find-Screenshot @('statistics-dark.png','statistics.png'));Widget=$false},
        @{File=(Find-Screenshot @('reset-inventory.png','resets.png'));Widget=$false},
        @{File=(Find-Screenshot @('widget-dark.png','widget.png'));Widget=$true},
        @{File=(Find-Screenshot @('dashboard-light.png'));Widget=$false}
    )
    $font=Filter-Path (Join-Path $env:WINDIR 'Fonts/segoeui.ttf')
    $bold=Filter-Path (Join-Path $env:WINDIR 'Fonts/seguisb.ttf')
    $footer=Text-File 'footer.txt' $copy.Footer
    $clips=@()
    for($i=0;$i -lt $scenes.Count;$i++){
        $scene=$scenes[$i]
        $png=Join-Path $scratch "scene-$i.png"
        $label=Text-File "label-$i.txt" $copy.Titles[$i]
        $detail=Text-File "detail-$i.txt" $copy.Details[$i]
        # Text files preserve accents and apostrophes; expansion=none keeps literal punctuation intact.
        $frame="drawbox=x=24:y=57:w=912:h=2:color=0x3CD3AD:t=fill,drawtext=fontfile='$bold':textfile='$label':expansion=none:x=24:y=20:fontsize=20:fontcolor=0xE3E8EB,drawtext=fontfile='$font':text='UsageDock':x=w-tw-24:y=24:fontsize=16:fontcolor=0xA2ADB8,drawtext=fontfile='$font':textfile='$detail':expansion=none:x=24:y=697:fontsize=17:fontcolor=0xE3E8EB,drawtext=fontfile='$font':textfile='$footer':expansion=none:x=24:y=726:fontsize=13:fontcolor=0xA2ADB8"
        if($scene.Widget){
            $light=Find-Screenshot @('widget-light.png')
            $filter="color=c=0x0B1217:s=960x752[bg];[0:v]format=rgba[a];[1:v]format=rgba[b];[bg][a]overlay=x=124:y=104[tmp];[tmp][b]overlay=x=568:y=104,$frame"
            Invoke-FFmpeg @('-i',$scene.File,'-i',$light,'-filter_complex',$filter,'-frames:v','1',$png)
        }else{
            $filter="scale=912:612:force_original_aspect_ratio=decrease:flags=lanczos,pad=960:752:(ow-iw)/2:72:color=0x0B1217,$frame"
            Invoke-FFmpeg @('-i',$scene.File,'-vf',$filter,'-frames:v','1',$png)
        }
        $clip=Join-Path $scratch "scene-$i.mkv"
        # Fades separate real stills; this is a screenshot tour, not simulated interaction.
        $fades=if($i -eq 0){'fade=t=out:st=3.75:d=0.25'}else{'fade=t=in:st=0:d=0.25,fade=t=out:st=3.75:d=0.25'}
        Invoke-FFmpeg @('-loop','1','-framerate','8','-i',$png,'-t','4','-vf',$fades,'-c:v','ffv1','-pix_fmt','bgr0',$clip)
        $clips+=$clip
    }
    $concat=Join-Path $scratch 'scenes.txt'
    [IO.File]::WriteAllLines($concat,($clips|ForEach-Object{"file '"+$_.Replace('\','/').Replace("'","'\''")+"'"}),[Text.UTF8Encoding]::new($false))
    Invoke-FFmpeg @('-f','concat','-safe','0','-i',$concat,'-filter_complex','split[a][b];[a]palettegen=max_colors=192:stats_mode=diff[p];[b][p]paletteuse=dither=bayer:bayer_scale=3:diff_mode=rectangle','-loop','0',$output)
    $file=Get-Item -LiteralPath $output
    if($file.Length -gt 4MB){throw "GIF exceeds 4 MiB: $($file.Length) bytes"}
    $probe=& $ffprobe -v error -select_streams v:0 -show_entries stream=width,height,nb_frames:format=duration -of json $output
    if($LASTEXITCODE -ne 0){throw 'ffprobe failed.'}
    $metadata=($probe -join "`n")|ConvertFrom-Json
    $duration=[double]::Parse($metadata.format.duration,[Globalization.CultureInfo]::InvariantCulture)
    if($metadata.streams[0].width -ne 960 -or $metadata.streams[0].height -ne 752 -or $metadata.streams[0].nb_frames -ne '160' -or [Math]::Abs($duration-20) -gt 0.1){throw 'Unexpected GIF dimensions, duration or frame count.'}
    $sheet=Join-Path $artifactRoot "demo-$Language-contact.png"
    Invoke-FFmpeg @('-i',$output,'-vf',"select='eq(n,8)+eq(n,40)+eq(n,72)+eq(n,104)+eq(n,136)',scale=480:376,tile=3x2:color=0x0B1217",'-frames:v','1',$sheet)
    $completed=$true
    [pscustomobject]@{Language=$Language;File=$file.FullName;Bytes=$file.Length;Width=960;Height=752;Seconds=$duration;Frames=160;ReviewSheet=$sheet}
}finally{
    if($completed){
        $resolved=[IO.Path]::GetFullPath($scratch)
        $allowed=[IO.Path]::GetFullPath($artifactRoot).TrimEnd('\','/')+[IO.Path]::DirectorySeparatorChar
        if(!$resolved.StartsWith($allowed,[StringComparison]::OrdinalIgnoreCase)){throw 'Refusing to clean scratch outside the repository artifact directory.'}
        Remove-Item -LiteralPath $resolved -Recurse -Force
    }
}
