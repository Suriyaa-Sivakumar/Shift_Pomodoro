Add-Type -AssemblyName System.Drawing

$assetsDir = Join-Path (Get-Location).Path "src\FlowWindows.App\Assets"
$srcLogoPath = Join-Path $assetsDir "AppLogo.png"

Write-Host "Loading master AppLogo.png from $srcLogoPath..."
$rawLogo = [System.Drawing.Bitmap]::FromFile($srcLogoPath)

# 1. Exact tight crop of the hourglass glyph
$cropRect = New-Object System.Drawing.Rectangle 308, 266, 406, 458
$croppedGlyph = $rawLogo.Clone($cropRect, $rawLogo.PixelFormat)

# Function to render the new CIRCULAR app icon with larger center glyph
function New-CircleAppIcon([int]$size, [System.Drawing.Image]$glyph) {
    $target = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($target)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    $pad = [float]($size * 0.03)
    $diameter = [float]($size - $pad * 2)

    # Clean crisp white circle
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255, 255))
    $g.FillEllipse($brush, $pad, $pad, $diameter, $diameter)
    $brush.Dispose()

    # Subtle anti-aliased border to define the circle cleanly on both dark and light taskbars
    $penWidth = [float][Math]::Max(1.0, $size * 0.02)
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(35, 0, 0, 0)), $penWidth
    $g.DrawEllipse($pen, $pad, $pad, $diameter, $diameter)
    $pen.Dispose()

    # Center glyph - enlarged to 72% of the circle diameter for maximum legibility on taskbar
    $glyphH = [float]($diameter * 0.72)
    $glyphW = [float]($glyphH * (406.0 / 458.0))
    $glyphX = [float]($pad + ($diameter - $glyphW) / 2.0)
    $glyphY = [float]($pad + ($diameter - $glyphH) / 2.0)

    $g.DrawImage($glyph, $glyphX, $glyphY, $glyphW, $glyphH)
    $g.Dispose()
    return $target
}

# 1. Square150x150Logo.scale-200.png (300x300)
$sq150 = New-CircleAppIcon 300 $croppedGlyph
$sq150.Save((Join-Path $assetsDir "Square150x150Logo.scale-200.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$sq150.Dispose()

# 2. Square44x44Logo.scale-200.png (88x88)
$sq44 = New-CircleAppIcon 88 $croppedGlyph
$sq44.Save((Join-Path $assetsDir "Square44x44Logo.scale-200.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$sq44.Dispose()

# 3. Square44x44Logo.targetsize-24_altform-unplated.png (24x24)
$sq24 = New-CircleAppIcon 24 $croppedGlyph
$sq24.Save((Join-Path $assetsDir "Square44x44Logo.targetsize-24_altform-unplated.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$sq24.Dispose()

# 4. Square44x44Logo.targetsize-48_altform-lightunplated.png (48x48)
$sq48 = New-CircleAppIcon 48 $croppedGlyph
$sq48.Save((Join-Path $assetsDir "Square44x44Logo.targetsize-48_altform-lightunplated.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$sq48.Dispose()

# 5. StoreLogo.png (50x50)
$store = New-CircleAppIcon 50 $croppedGlyph
$store.Save((Join-Path $assetsDir "StoreLogo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$store.Dispose()

# 6. LockScreenLogo.scale-200.png (48x48)
$lock = New-CircleAppIcon 48 $croppedGlyph
$lock.Save((Join-Path $assetsDir "LockScreenLogo.scale-200.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$lock.Dispose()

# 7. SplashScreen.scale-200.png (1240x600)
$splash = New-Object System.Drawing.Bitmap 1240, 600, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sg = [System.Drawing.Graphics]::FromImage($splash)
$sg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$sg.Clear([System.Drawing.Color]::FromArgb(255, 30, 32, 35))
$splashCircle = New-CircleAppIcon 220 $croppedGlyph
$sg.DrawImage($splashCircle, [float]((1240 - 220) / 2), [float]((600 - 220) / 2), 220, 220)
$splashCircle.Dispose()
$sg.Dispose()
$splash.Save((Join-Path $assetsDir "SplashScreen.scale-200.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$splash.Dispose()

# 8. Wide310x150Logo.scale-200.png (620x300)
$wide = New-Object System.Drawing.Bitmap 620, 300, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$wg = [System.Drawing.Graphics]::FromImage($wide)
$wg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$wg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$wg.Clear([System.Drawing.Color]::FromArgb(255, 30, 32, 35))
$wideCircle = New-CircleAppIcon 140 $croppedGlyph
$wg.DrawImage($wideCircle, [float]((620 - 140) / 2), [float]((300 - 140) / 2), 140, 140)
$wideCircle.Dispose()
$wg.Dispose()
$wide.Save((Join-Path $assetsDir "Wide310x150Logo.scale-200.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$wide.Dispose()

# 9. Multi-resolution AppIcon.ico
Write-Host "Building multi-resolution AppIcon.ico..."
$icoSizes = @(256, 128, 64, 48, 32, 24, 16)
$pngStreams = [System.Collections.Generic.List[System.IO.MemoryStream]]::new()

foreach ($s in $icoSizes) {
    $icoBmp = New-CircleAppIcon $s $croppedGlyph
    $ms = New-Object System.IO.MemoryStream
    $icoBmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $icoBmp.Dispose()
    $pngStreams.Add($ms)
}

$icoPath = Join-Path $assetsDir "AppIcon.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs

$bw.Write([uint16]0)
$bw.Write([uint16]1)
$bw.Write([uint16]$icoSizes.Count)

$headerSize = 6 + ($icoSizes.Count * 16)
$currentOffset = $headerSize

for ($i = 0; $i -lt $icoSizes.Count; $i++) {
    $sz = $icoSizes[$i]
    $wByte = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    $hByte = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    $len = [uint32]$pngStreams[$i].Length

    $bw.Write($wByte)
    $bw.Write($hByte)
    $bw.Write([byte]0)
    $bw.Write([byte]0)
    $bw.Write([uint16]1)
    $bw.Write([uint16]32)
    $bw.Write($len)
    $bw.Write([uint32]$currentOffset)

    $currentOffset += $len
}

for ($i = 0; $i -lt $icoSizes.Count; $i++) {
    $bytes = $pngStreams[$i].ToArray()
    $bw.Write($bytes)
    $pngStreams[$i].Dispose()
}

$bw.Flush()
$fs.Close()

$croppedGlyph.Dispose()
$rawLogo.Dispose()

Write-Host "New circular icons with enlarged center glyph created successfully!"
