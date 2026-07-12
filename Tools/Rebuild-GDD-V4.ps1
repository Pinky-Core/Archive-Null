param(
    [string]$Source = "Assets/GDD_Archive_NULL-V3_BACKUP.docx",
    [string]$Markdown = "Assets/GDD_Archive_NULL-V4.md",
    [string]$Output = "Assets/GDD_Archive_NULL-V4.docx"
)

$ErrorActionPreference = "Stop"
$w = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"

function Get-MarkdownSections([string]$path) {
    $sections = @{}
    $current = $null
    foreach ($line in [System.IO.File]::ReadAllLines((Resolve-Path $path), [System.Text.Encoding]::UTF8)) {
        if ($line -match '^## (\d+)\.\s+(.+)$') {
            $current = $Matches[1]
            $sections[$current] = [System.Collections.Generic.List[string]]::new()
            continue
        }
        if ($null -ne $current) { $sections[$current].Add($line) }
    }
    return $sections
}

function Convert-MarkdownLines($sections, [string[]]$numbers) {
    $result = [System.Collections.Generic.List[object]]::new()
    foreach ($number in $numbers) {
        foreach ($raw in $sections[$number]) {
            $line = $raw.Trim()
            if (-not $line -or $line -eq '---') { continue }
            if ($line -match '^###\s+(.+)$') {
                $result.Add(@{ Text = $Matches[1]; Bold = $true })
                continue
            }
            $line = $line -replace '^[-*]\s+', '- '
            $line = $line -replace '^\d+\.\s+', { param($m) $m.Value }
            $line = $line -replace '\*\*([^*]+)\*\*', '$1'
            $line = $line -replace '`([^`]+)`', '$1'
            $result.Add(@{ Text = $line; Bold = $false })
        }
    }
    return $result
}

function New-Paragraph([xml]$xml, [string]$text, [bool]$bold) {
    $p = $xml.CreateElement('w', 'p', $w)
    $pPr = $xml.CreateElement('w', 'pPr', $w)
    $spacing = $xml.CreateElement('w', 'spacing', $w)
    [void]$spacing.SetAttribute('after', $w, $(if ($bold) { '120' } else { '80' }))
    [void]$spacing.SetAttribute('line', $w, '276')
    [void]$spacing.SetAttribute('lineRule', $w, 'auto')
    [void]$pPr.AppendChild($spacing)
    [void]$p.AppendChild($pPr)
    $r = $xml.CreateElement('w', 'r', $w)
    if ($bold) {
        $rPr = $xml.CreateElement('w', 'rPr', $w)
        [void]$rPr.AppendChild($xml.CreateElement('w', 'b', $w))
        [void]$r.AppendChild($rPr)
    }
    $t = $xml.CreateElement('w', 't', $w)
    [void]$t.SetAttribute('space', 'http://www.w3.org/XML/1998/namespace', 'preserve')
    $t.InnerText = $text
    [void]$r.AppendChild($t)
    [void]$p.AppendChild($r)
    return ,$p
}

$sections = Get-MarkdownSections $Markdown
$mapping = [ordered]@{
    '2.1 Deseo' = @('1','2')
    '2.2 Situaciones interesantes variadas' = @('15')
    '2.3 Imagen' = @('18')
    '2.4 Gesto' = @('9','10')
    '2.5 Fantasía, Valores Nucleares, Pilares de juego, Abstracciones Clave' = @('3','4','5','6')
    '2.6 Datos generales' = @('7')
    '2.7 Key Features' = @('5','10')
    '2.8 Descripción del juego' = @('1','8','12','13')
    '2.9 Monetización' = $null
    '3.1 Sustantivos, atributos y verbos' = @('6','9','10')
    '3.2 Mecánicas' = @('11')
    '3.3 Requerimientos de diseño' = @('20','21','24')
    '3.4 Información adicional' = @('8')
    '4.1 Niveles' = @('14')
    '4.1.1 Nombre de Nivel / Puzzle / Escenario / Wave / Etc.' = @('14')
    '4.1.2 Propósito' = @('12')
    '4.1.3 Descripción' = @('13','14')
    '4.1.4 Representación gráfica abstracta' = @('8')
    '4.2 Arte de niveles' = @('18')
    'Experiencia de usuario (UX)' = @('16','17','20','22')
    'Consideraciones sobre el codigo' = @('20','21','23','24')
    'Extra' = @('19')
    'Pitch deck + Exposición oral + Prueba a desconocidos' = @('25')
}

$temp = Join-Path $env:TEMP ("gdd_v4_" + [guid]::NewGuid())
$unpacked = Join-Path $temp 'unpacked'
New-Item -ItemType Directory -Path $temp | Out-Null
Copy-Item -LiteralPath $Source -Destination (Join-Path $temp 'source.zip')
Expand-Archive -LiteralPath (Join-Path $temp 'source.zip') -DestinationPath $unpacked

$documentPath = Join-Path $unpacked 'word/document.xml'
$xml = [System.Xml.XmlDocument]::new()
$xml.PreserveWhitespace = $true
$xml.Load($documentPath)
$ns = [System.Xml.XmlNamespaceManager]::new($xml.NameTable)
$ns.AddNamespace('w', $w)
$body = $xml.SelectSingleNode('//w:body', $ns)

function Get-Text($node) {
    return (($node.SelectNodes('.//w:t', $ns) | ForEach-Object { $_.InnerText }) -join '').Trim()
}

function Normalize-Heading([string]$value) {
    $formD = $value.Normalize([Text.NormalizationForm]::FormD)
    $chars = $formD.ToCharArray() | Where-Object {
        [Globalization.CharUnicodeInfo]::GetUnicodeCategory($_) -ne [Globalization.UnicodeCategory]::NonSpacingMark
    }
    return ((-join $chars) -replace '\s+', ' ').Trim().ToLowerInvariant()
}

$orderedHeadings = [System.Collections.Generic.List[object]]::new()
foreach ($node in @($body.ChildNodes)) {
    if ($node.LocalName -ne 'p') { continue }
    $text = Get-Text $node
    foreach ($key in $mapping.Keys) {
        $keyNumber = if ($key -match '^(\d+(?:\.\d+)*)\b') { $Matches[1] } else { $null }
        $textNumber = if ($text -match '^(\d+(?:\.\d+)*)\b') { $Matches[1] } else { $null }
        $matches = if ($keyNumber) {
            $keyNumber -eq $textNumber
        } else {
            (Normalize-Heading $text) -eq (Normalize-Heading $key)
        }
        if ($matches) {
            $orderedHeadings.Add([pscustomobject]@{ Key = $key; Value = $node })
            break
        }
    }
}

for ($i = $orderedHeadings.Count - 1; $i -ge 0; $i--) {
    $entry = $orderedHeadings[$i]
    $heading = $entry.Value
    $nextHeading = if ($i -lt $orderedHeadings.Count - 1) { $orderedHeadings[$i + 1].Value } else { $body.SelectSingleNode('./w:sectPr', $ns) }

    if ($null -eq $mapping[$entry.Key]) { continue }

    $cursor = $heading.NextSibling
    while ($null -ne $cursor -and $cursor -ne $nextHeading) {
        $next = $cursor.NextSibling
        if ($cursor.LocalName -eq 'p') { [void]$body.RemoveChild($cursor) }
        $cursor = $next
    }

    $anchor = $heading
    foreach ($item in (Convert-MarkdownLines $sections $mapping[$entry.Key])) {
        $paragraph = New-Paragraph $xml $item.Text $item.Bold
        [void]$body.InsertAfter($paragraph, $anchor)
        $anchor = $paragraph
    }
}

$settingsPath = Join-Path $unpacked 'docProps/core.xml'
if (Test-Path $settingsPath) {
    $core = [System.Xml.XmlDocument]::new()
    $core.PreserveWhitespace = $true
    $core.Load($settingsPath)
    $title = $core.SelectSingleNode('//*[local-name()="title"]')
    if ($title) { $title.InnerText = 'Archive: NULL - Game Design Document V4' }
    $core.Save($settingsPath)
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$writerSettings = [System.Xml.XmlWriterSettings]::new()
$writerSettings.Encoding = $utf8NoBom
$writerSettings.Indent = $false
$writer = [System.Xml.XmlWriter]::Create($documentPath, $writerSettings)
$xml.Save($writer)
$writer.Close()

$zipOut = Join-Path $temp 'result.zip'
Compress-Archive -Path (Join-Path $unpacked '*') -DestinationPath $zipOut
Copy-Item -LiteralPath $zipOut -Destination $Output -Force
Remove-Item -LiteralPath $temp -Recurse -Force
Write-Output "Creado: $Output"
