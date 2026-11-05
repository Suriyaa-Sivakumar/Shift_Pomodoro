Add-Type -AssemblyName System.Drawing

$srcPath = "C:\Users\Suriyaa Sivakumar\.gemini\antigravity\brain\5d92e4dc-9be2-4173-9953-33f8838cbe09\.user_uploaded\media_1793893365061_d10e98d2.jpg"
$assetsDir = Join-Path (Get-Location).Path "src\FlowWindows.App\Assets"

Write-Host "Loading source logo from $srcPath..."
$rawBmp = [System.Drawing.Bitmap]::FromFile($srcPath)
$width = $rawBmp.Width
$height = $rawBmp.Height

# Step 1: Create transparent glyph bitmap (1024x1024)
Write-Host "Extracting transparent glyph..."
$glyphBmp = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$glyphWhiteBmp = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$glyphGreenBmp = New-Object System.Drawing.Bitmap $width, $height, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

# Lock bits for high-speed pixel manipulation
$rect = New-Object System.Drawing.Rectangle 0, 0, $width, $height
$rawBits = $rawBmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::ReadOnly, [System.Drawing.Imaging.PixelFormat]::Format24bppRgb)
$glyphBits = $glyphBmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$whiteBits = $glyphWhiteBmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$greenBits = $glyphGreenBmp.LockBits($rect, [System.Drawing.Imaging.ImageLockMode]::WriteOnly, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

$rawStride = $rawBits.Stride
$outStride = $glyphBits.Stride

$rawScan0 = $rawBits.Scan0
$glyphScan0 = $glyphBits.Scan0
$whiteScan0 = $whiteBits.Scan0
$greenScan0 = $greenBits.Scan0

$rawBytes = New-Object byte[] ($rawStride * $height)
$glyphBytes = New-Object byte[] ($outStride * $height)
$whiteBytes = New-Object byte[] ($outStride * $height)
$greenBytes = New-Object byte[] ($outStride * $height)

[System.Runtime.InteropServices.Marshal]::Copy($rawScan0, $rawBytes, 0, $rawBytes.Length)

for ($y = 0; $y -lt $height; $y++) {
    $rawRow = $y * $rawStride
    $outRow = $y * $outStride
    for ($x = 0; $x -lt $width; $x++) {
        $b = $rawBytes[$rawRow + $x * 3]
        $g = $rawBytes[$rawRow + $x * 3 + 1]
        $r = $rawBytes[$rawRow + $x * 3 + 2]

        $lum = 0.299 * $r + 0.587 * $g + 0.114 * $b

        # Calculate alpha: pure white (lum >= 245) -> 0; dark (lum <= 70) -> 255
        if ($lum -ge 245) {
            $a = 0
        } elseif ($lum -le 70) {
            $a = 255
        } else {
            $a = [int](255.0 * (245.0 - $lum) / (245.0 - 70.0))
        }

        if ($a -gt 0) {
            # Dark charcoal symbol: #222326 (34, 35, 38)
            $glyphBytes[$outRow + $x * 4] = 38       # B
            $glyphBytes[$outRow + $x * 4 + 1] = 35   # G
            $glyphBytes[$outRow + $x * 4 + 2] = 34   # R
            $glyphBytes[$outRow + $x * 4 + 3] = [byte]$a

            # White symbol: #FFFFFF
            $whiteBytes[$outRow + $x * 4] = 255
            $whiteBytes[$outRow + $x * 4 + 1] = 255
            $whiteBytes[$outRow + $x * 4 + 2] = 255
            $whiteBytes[$outRow + $x * 4 + 3] = [byte]$a

            # Emerald Green symbol: #2DB88F (143, 184, 45)
            $greenBytes[$outRow + $x * 4] = 143      # B
            $greenBytes[$outRow + $x * 4 + 1] = 184  # G
            $greenBytes[$outRow + $x * 4 + 2] = 45   # R
            $greenBytes[$outRow + $x * 4 + 3] = [byte]$a
        } else {
            $glyphBytes[$outRow + $x * 4 + 3] = 0
            $whiteBytes[$outRow + $x * 4 + 3] = 0
            $greenBytes[$outRow + $x * 4 + 3] = 0
        }
    }
}

[System.Runtime.InteropServices.Marshal]::Copy($glyphBytes, 0, $glyphScan0, $glyphBytes.Length)
[System.Runtime.InteropServices.Marshal]::Copy($whiteBytes, 0, $whiteScan0, $whiteBytes.Length)
[System.Runtime.InteropServices.Marshal]::Copy($greenBytes, 0, $greenScan0, $greenBytes.Length)

$rawBmp.UnlockBits($rawBits)
$glyphBmp.UnlockBits($glyphBits)
$glyphWhiteBmp.UnlockBits($whiteBits)
$glyphGreenBmp.UnlockBits($greenBits)

# Save Master PNGs
$glyphBmp.Save((Join-Path $assetsDir "AppLogo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$glyphWhiteBmp.Save((Join-Path $assetsDir "AppLogo-White.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$glyphGreenBmp.Save((Join-Path $assetsDir "AppLogo-Green.png"), [System.Drawing.Imaging.ImageFormat]::Png)
Write-Host "Saved AppLogo.png, AppLogo-White.png, AppLogo-Green.png."

# Helper to render squircle app icon
function New-SquircleAppIcon([int]$size, [System.Drawing.Image]$logoImage) {
    $target = New-Object System.Drawing.Bitmap $size, $size, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($target)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)

    # Draw rounded squircle card
    $radius = [float]($size * 0.22)
    $pad = [float]($size * 0.04)
    $cardSize = [float]($size - $pad * 2)
    
    $path = New-Object System.Drawing.Drawing2D.GraphicsPath
    $path.AddArc($pad, $pad, $radius, $radius, 180, 90)
    $path.AddArc($pad + $cardSize - $radius, $pad, $radius, $radius, 270, 90)
    $path.AddArc($pad + $cardSize - $radius, $pad + $cardSize - $radius, $radius, $radius, 0, 90)
    $path.AddArc($pad, $pad + $cardSize - $radius, $radius, $radius, 90, 90)
    $path.CloseFigure()

    # Clean off-white background with subtle border
    $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 252, 252, 253))
    $g.FillPath($brush, $path)
    $brush.Dispose()

    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(40, 0, 0, 0)), ([float]($size * 0.015))
    $g.DrawPath($pen, $path)
    $pen.Dispose()

    # Draw centered logo glyph
    $logoPad = [float]($size * 0.18)
    $logoSize = [float]($size - $logoPad * 2)
    $g.DrawImage($logoImage, $logoPad, $logoPad, $logoSize, $logoSize)

    $path.Dispose()
    $g.Dispose()
    return $target
}

# Helper to render transparent icon directly
function New-ResizedBitmap([int]$w, [int]$h, [System.Drawing.Image]$srcImage) {
    $target = New-Object System.Drawing.Bitmap $w, $h, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($target)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
    $g.Clear([System.Drawing.Color]::Transparent)
    $g.DrawImage($srcImage, 0, 0, $w, $h)
    $g.Dispose()
    return $target
}

# 1. Square150x150Logo.scale-200.png (300x300)
$sq150 = New-SquircleAppIcon 300 $glyphBmp
$sq150.Save((Join-Path $assetsDir "Square150x150Logo.scale-200.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$sq150.Dispose()

# 2. Square44x44Logo.scale-200.png (88x88)
$sq44 = New-SquircleAppIcon 88 $glyphBmp
$sq44.Save((Join-Path $assetsDir "Square44x44Logo.scale-200.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$sq44.Dispose()

# 3. Square44x44Logo.targetsize-24_altform-unplated.png (24x24 transparent glyph)
$sq24unplated = New-ResizedBitmap 24 24 $glyphBmp
$sq24unplated.Save((Join-Path $assetsDir "Square44x44Logo.targetsize-24_altform-unplated.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$sq24unplated.Dispose()

# 4. Square44x44Logo.targetsize-48_altform-lightunplated.png (48x48 transparent glyph)
$sq48unplated = New-ResizedBitmap 48 48 $glyphBmp
$sq48unplated.Save((Join-Path $assetsDir "Square44x44Logo.targetsize-48_altform-lightunplated.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$sq48unplated.Dispose()

# 5. StoreLogo.png (50x50)
$store = New-SquircleAppIcon 50 $glyphBmp
$store.Save((Join-Path $assetsDir "StoreLogo.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$store.Dispose()

# 6. LockScreenLogo.scale-200.png (48x48 transparent glyph)
$lock = New-ResizedBitmap 48 48 $glyphBmp
$lock.Save((Join-Path $assetsDir "LockScreenLogo.scale-200.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$lock.Dispose()

# 7. SplashScreen.scale-200.png (1240x600)
$splash = New-Object System.Drawing.Bitmap 1240, 600, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$sg = [System.Drawing.Graphics]::FromImage($splash)
$sg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$sg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$sg.Clear([System.Drawing.Color]::FromArgb(255, 30, 32, 35)) # Shift dark card background
$splashLogo = New-SquircleAppIcon 200 $glyphBmp
$sg.DrawImage($splashLogo, [float]((1240 - 200) / 2), [float]((600 - 200) / 2), 200, 200)
$splashLogo.Dispose()
$sg.Dispose()
$splash.Save((Join-Path $assetsDir "SplashScreen.scale-200.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$splash.Dispose()

# 8. Wide310x150Logo.scale-200.png (620x300)
$wide = New-Object System.Drawing.Bitmap 620, 300, ([System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$wg = [System.Drawing.Graphics]::FromImage($wide)
$wg.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$wg.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$wg.Clear([System.Drawing.Color]::FromArgb(255, 30, 32, 35))
$wideLogo = New-SquircleAppIcon 140 $glyphBmp
$wg.DrawImage($wideLogo, [float]((620 - 140) / 2), [float]((300 - 140) / 2), 140, 140)
$wideLogo.Dispose()
$wg.Dispose()
$wide.Save((Join-Path $assetsDir "Wide310x150Logo.scale-200.png"), [System.Drawing.Imaging.ImageFormat]::Png)
$wide.Dispose()

# Step 2: Build multi-resolution AppIcon.ico
Write-Host "Building multi-resolution AppIcon.ico..."
$icoSizes = @(256, 128, 64, 48, 32, 24, 16)
$pngStreams = [System.Collections.Generic.List[System.IO.MemoryStream]]::new()

foreach ($s in $icoSizes) {
    $icoBmp = New-SquircleAppIcon $s $glyphBmp
    $ms = New-Object System.IO.MemoryStream
    $icoBmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $icoBmp.Dispose()
    $pngStreams.Add($ms)
}

$icoPath = Join-Path $assetsDir "AppIcon.ico"
$fs = [System.IO.File]::Create($icoPath)
$bw = New-Object System.IO.BinaryWriter $fs

# ICO Header
$bw.Write([uint16]0)                # Reserved
$bw.Write([uint16]1)                # Type = 1 (ICO)
$bw.Write([uint16]$icoSizes.Count)  # Image count

# Directory Entries
$headerSize = 6 + ($icoSizes.Count * 16)
$currentOffset = $headerSize

for ($i = 0; $i -lt $icoSizes.Count; $i++) {
    $sz = $icoSizes[$i]
    $wByte = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    $hByte = if ($sz -ge 256) { [byte]0 } else { [byte]$sz }
    $len = [uint32]$pngStreams[$i].Length

    $bw.Write($wByte)               # Width
    $bw.Write($hByte)               # Height
    $bw.Write([byte]0)              # Colors (0 = 256+)
    $bw.Write([byte]0)              # Reserved
    $bw.Write([uint16]1)            # Color planes
    $bw.Write([uint16]32)           # Bits per pixel
    $bw.Write($len)                 # Size of PNG stream
    $bw.Write([uint32]$currentOffset) # Offset

    $currentOffset += $len
}

# Image Data
for ($i = 0; $i -lt $icoSizes.Count; $i++) {
    $bytes = $pngStreams[$i].ToArray()
    $bw.Write($bytes)
    $pngStreams[$i].Dispose()
}

$bw.Flush()
$fs.Close()
Write-Host "AppIcon.ico generated successfully at $icoPath!"

$rawBmp.Dispose()
$glyphBmp.Dispose()
$glyphWhiteBmp.Dispose()
$glyphGreenBmp.Dispose()

