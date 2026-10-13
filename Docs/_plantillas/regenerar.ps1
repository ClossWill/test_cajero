# Regenera los .md de Docs/ reemplazando {{CODE:ruta}} por el código fuente actual del proyecto.
# Uso (desde cualquier carpeta):  powershell -NoProfile -File Docs\_plantillas\regenerar.ps1
$root = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
foreach ($f in Get-ChildItem $PSScriptRoot -Filter *.md) {
  $txt = [IO.File]::ReadAllText($f.FullName, [Text.Encoding]::UTF8)
  $txt = [regex]::Replace($txt, '\{\{CODE:([^}]+)\}\}', {
    param($m)
    $rel = $m.Groups[1].Value
    $src = [IO.File]::ReadAllText((Join-Path $root $rel), [Text.Encoding]::UTF8).TrimEnd()
    $lang = if ($rel.EndsWith('.json')) { 'jsonc' } else { 'csharp' }
    '```' + $lang + "`n" + $src + "`n" + '```'
  })
  [IO.File]::WriteAllText((Join-Path $root "Docs\$($f.Name)"), $txt, (New-Object Text.UTF8Encoding($false)))
}
Write-Host "Docs regenerados en $root\Docs"
