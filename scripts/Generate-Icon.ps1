$ErrorActionPreference = "Stop"
Add-Type -AssemblyName System.Drawing
$root = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$dest = Join-Path $root "assets\SeedWallpaper.ico"
New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
$bitmap = [System.Drawing.Bitmap]::new(256,256)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
$graphics.Clear([System.Drawing.Color]::Transparent)
$bg = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml("#092631"))
$seed = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml("#50E0AD"))
$leaf = [System.Drawing.SolidBrush]::new([System.Drawing.ColorTranslator]::FromHtml("#A1F3C6"))
$stem = [System.Drawing.Pen]::new([System.Drawing.ColorTranslator]::FromHtml("#D7FFE9"), 10)
try {
  $graphics.FillEllipse($bg, 5, 5, 246, 246)
  $graphics.FillEllipse($seed, 90, 115, 78, 102)
  $graphics.DrawBezier($stem, 127, 141, 128, 99, 148, 69, 171, 49)
  $graphics.TranslateTransform(117, 75)
  $graphics.RotateTransform(-40)
  $graphics.FillEllipse($seed, -57, -17, 65, 31)
  $graphics.ResetTransform()
  $graphics.TranslateTransform(173, 59)
  $graphics.RotateTransform(-35)
  $graphics.FillEllipse($leaf, -10, -15, 68, 35)
  $graphics.ResetTransform()
  $stream = [System.IO.MemoryStream]::new()
  try {
    $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
    $png = $stream.ToArray()
    $out = [System.IO.File]::Create($dest)
    try {
      $writer = [System.IO.BinaryWriter]::new($out)
      $writer.Write([uint16]0); $writer.Write([uint16]1); $writer.Write([uint16]1)
      $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0); $writer.Write([byte]0)
      $writer.Write([uint16]1); $writer.Write([uint16]32)
      $writer.Write([uint32]$png.Length); $writer.Write([uint32]22)
      $writer.Write($png)
      $writer.Flush()
    } finally { $out.Dispose() }
  } finally { $stream.Dispose() }
} finally { $graphics.Dispose(); $bitmap.Dispose(); $bg.Dispose(); $seed.Dispose(); $leaf.Dispose(); $stem.Dispose() }
Write-Host "Created $dest"
