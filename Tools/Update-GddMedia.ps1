param([string]$CaptureDirectory='Artifacts/FeedbackValidation')
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.IO.Compression.FileSystem
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.Drawing
$root=Split-Path $PSScriptRoot -Parent
$document=Join-Path $root 'Assets/ArchiveNull/Documentation/GDD_Archive_NULL-V4.docx'
$pending=Join-Path $root '.utmp/GDD-with-gameplay-media.docx'
Copy-Item -LiteralPath $document -Destination $pending -Force
function Read-ZipText($zip,$name) {
    $reader=[IO.StreamReader]::new($zip.GetEntry($name).Open())
    try {$reader.ReadToEnd()} finally {$reader.Dispose()}
}
function Write-ZipText($zip,$name,$text) {
    $old=$zip.GetEntry($name);if($old){$old.Delete()}
    $writer=[IO.StreamWriter]::new($zip.CreateEntry($name).Open(),[Text.UTF8Encoding]::new($false))
    try {$writer.Write($text)} finally {$writer.Dispose()}
}
function Escape-Xml($value) {[Security.SecurityElement]::Escape($value)}
$zip=[IO.Compression.ZipFile]::Open($pending,[IO.Compression.ZipArchiveMode]::Update)
try {
    [xml]$xml=Read-ZipText $zip 'word/document.xml'
    [xml]$relationships=Read-ZipText $zip 'word/_rels/document.xml.rels'
    [xml]$types=Read-ZipText $zip '[Content_Types].xml'
    $ns=[Xml.XmlNamespaceManager]::new($xml.NameTable);$ns.AddNamespace('w','http://schemas.openxmlformats.org/wordprocessingml/2006/main')
    $old=$xml.SelectSingleNode('//w:sdt[w:sdtPr/w:tag[@w:val="ArchiveFeedbackMedia"]]',$ns)
    if($old){$old.ParentNode.RemoveChild($old)|Out-Null}
    $content=[Text.StringBuilder]::new()
    [void]$content.Append('<w:p><w:pPr><w:pageBreakBefore/><w:pStyle w:val="Heading1"/></w:pPr><w:r><w:t>Capturas y secuencias del prototipo jugable</w:t></w:r></w:p>')
    [void]$content.Append('<w:p><w:r><w:t>Material capturado en Play Mode sobre el juego implementado. Las voces son sintéticas y provisionales. Los GIF y el video se entregan como archivos externos para conservar la animación; dentro de este documento se incluyen capturas reales.</w:t></w:r></w:p>')
    [void]$content.Append('<w:p><w:r><w:t>Controles actuales: WASD y mouse para explorar; Ctrl izquierdo para agacharse; E para interactuar; G para elegir equipo; TAB para consultar evidencias y reconstrucción. La UV revela residuos fluorescentes y el pincel revela huellas latentes. El PIN se consulta en una pegatina junto al teléfono, separado de la nota de Julián.</w:t></w:r></w:p>')
    $captures=[ordered]@{
        'desktop'='Ordenador de la oficina: escritorio físico y cierre de sesión.'
        'house'='Casa Herrera: exploración de la primera memoria.'
        'phone-voice'='Teléfono: nota de voz reproducible con transcripción.'
        'echo'='Recuerdo eco: siluetas y partículas en tercera persona.'
        'reconstruction'='Libreta: reconstrucción de la secuencia de salida.'
        'salas-computer'='Oficina de Salas: documentos y registro de comprobantes.'
        'pause-options'='Opciones de pausa de una memoria.'
        'board'='Pizarrón: fotografías ampliadas y guía para conectar pruebas.'
    }
    $index=0
    foreach($name in $captures.Keys) {
        $path=Join-Path $root "$CaptureDirectory/$name.png"
        if(!(Test-Path -LiteralPath $path)){throw "Falta la captura $path"}
        $index++;$rid="rIdFeedbackImage$index";$media="feedback-$name.png"
        $oldEntry=$zip.GetEntry("word/media/$media");if($oldEntry){$oldEntry.Delete()}
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($zip,$path,"word/media/$media")|Out-Null
        $image=[Drawing.Image]::FromFile($path)
        try {$width=5200000;$height=[long]($width*$image.Height/$image.Width)} finally {$image.Dispose()}
        $relationship=$relationships.CreateElement('Relationship',$relationships.DocumentElement.NamespaceURI)
        $relationship.SetAttribute('Id',$rid);$relationship.SetAttribute('Type','http://schemas.openxmlformats.org/officeDocument/2006/relationships/image');$relationship.SetAttribute('Target',"media/$media")
        $existing=$relationships.DocumentElement.ChildNodes | Where-Object {$_.Id -eq $rid};foreach($item in $existing){$relationships.DocumentElement.RemoveChild($item)|Out-Null}
        $relationships.DocumentElement.AppendChild($relationship)|Out-Null
        [void]$content.Append("<w:p><w:pPr><w:keepNext/></w:pPr><w:r><w:rPr><w:b/></w:rPr><w:t>$(Escape-Xml $captures[$name])</w:t></w:r></w:p>")
        [void]$content.Append(@"
<w:p><w:r><w:drawing><wp:inline><wp:extent cx="$width" cy="$height"/><wp:docPr id="$(900+$index)" name="$media"/><a:graphic><a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture"><pic:pic><pic:nvPicPr><pic:cNvPr id="0" name="$media"/><pic:cNvPicPr/></pic:nvPicPr><pic:blipFill><a:blip r:embed="$rid"/><a:stretch><a:fillRect/></a:stretch></pic:blipFill><pic:spPr><a:xfrm><a:off x="0" y="0"/><a:ext cx="$width" cy="$height"/></a:xfrm><a:prstGeom prst="rect"><a:avLst/></a:prstGeom></pic:spPr></pic:pic></a:graphicData></a:graphic></wp:inline></w:drawing></w:r></w:p>
"@)
    }
    $links=[ordered]@{'reconstruccion.gif'='GIF: reconstrucción de la memoria';'recuerdo-eco.gif'='GIF: recuerdo en tercera persona';'ArchiveNull_Explicativo_SinAudio.mp4'='Video explicativo sin sonido'}
    foreach($file in $links.Keys) {
        if(!(Test-Path (Join-Path $root "Docs/Media/$file"))){throw "Falta el archivo multimedia $file"}
        $rid="rIdFeedbackMedia$index";$index++
        $relationship=$relationships.CreateElement('Relationship',$relationships.DocumentElement.NamespaceURI);$relationship.SetAttribute('Id',$rid);$relationship.SetAttribute('Type','http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink');$relationship.SetAttribute('Target',"../../../Docs/Media/$file");$relationship.SetAttribute('TargetMode','External')
        $existing=$relationships.DocumentElement.ChildNodes | Where-Object {$_.Id -eq $rid};foreach($item in $existing){$relationships.DocumentElement.RemoveChild($item)|Out-Null};$relationships.DocumentElement.AppendChild($relationship)|Out-Null
        [void]$content.Append("<w:p><w:hyperlink r:id=`"$rid`"><w:r><w:rPr><w:color w:val=`"0563C1`"/><w:u w:val=`"single`"/></w:rPr><w:t>$(Escape-Xml $links[$file])</w:t></w:r></w:hyperlink></w:p>")
    }
    [xml]$fragment=@"
<w:sdt xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships" xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing" xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main" xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"><w:sdtPr><w:tag w:val="ArchiveFeedbackMedia"/></w:sdtPr><w:sdtContent>$content</w:sdtContent></w:sdt>
"@
    $body=$xml.SelectSingleNode('//w:body',$ns);$section=$body.SelectSingleNode('w:sectPr',$ns)
    $node=$xml.ImportNode($fragment.DocumentElement,$true);if($section){$body.InsertBefore($node,$section)|Out-Null}else{$body.AppendChild($node)|Out-Null}
    if(!@($types.Types.Default | Where-Object Extension -eq 'png').Count){$type=$types.CreateElement('Default',$types.DocumentElement.NamespaceURI);$type.SetAttribute('Extension','png');$type.SetAttribute('ContentType','image/png');$types.DocumentElement.AppendChild($type)|Out-Null}
    Write-ZipText $zip 'word/document.xml' $xml.OuterXml
    Write-ZipText $zip 'word/_rels/document.xml.rels' $relationships.OuterXml
    Write-ZipText $zip '[Content_Types].xml' $types.OuterXml
} finally {$zip.Dispose()}
Copy-Item -LiteralPath $pending -Destination $document -Force
Write-Output "GDD actualizado: $($captures.Count) capturas y $($links.Count) enlaces multimedia."
