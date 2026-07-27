param(
    [string]$OutputPath = "src\WindowsAdminShortcuts\Assets\WindowsAdminShortcuts.ico",
    [string]$PreviewPath = "artifacts\app-icon-preview.png"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 20, 24, 32, 40, 48, 64, 128, 256)
$renderScale = 4

function New-RoundedRectanglePath {
    param(
        [System.Drawing.RectangleF]$Rectangle,
        [float]$Radius
    )

    $diameter = $Radius * 2
    $path = [System.Drawing.Drawing2D.GraphicsPath]::new()
    $path.AddArc($Rectangle.Left, $Rectangle.Top, $diameter, $diameter, 180, 90)
    $path.AddArc($Rectangle.Right - $diameter, $Rectangle.Top, $diameter, $diameter, 270, 90)
    $path.AddArc(
        $Rectangle.Right - $diameter,
        $Rectangle.Bottom - $diameter,
        $diameter,
        $diameter,
        0,
        90)
    $path.AddArc($Rectangle.Left, $Rectangle.Bottom - $diameter, $diameter, $diameter, 90, 90)
    $path.CloseFigure()
    return $path
}

function Convert-Point {
    param(
        [float]$X,
        [float]$Y,
        [float]$Scale
    )

    return [System.Drawing.PointF]::new($X * $Scale, $Y * $Scale)
}

function New-AppIconBitmap {
    param([int]$Size)

    $canvasSize = $Size * $renderScale
    $scale = $canvasSize / 256.0
    $canvas = [System.Drawing.Bitmap]::new(
        $canvasSize,
        $canvasSize,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $graphics = [System.Drawing.Graphics]::FromImage($canvas)
    try {
        $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
        $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $graphics.Clear([System.Drawing.Color]::Transparent)

        $shadowRectangle = [System.Drawing.RectangleF]::new(
            20 * $scale,
            24 * $scale,
            216 * $scale,
            216 * $scale)
        $shadowPath = New-RoundedRectanglePath $shadowRectangle (48 * $scale)
        $shadowBrush = [System.Drawing.SolidBrush]::new(
            [System.Drawing.Color]::FromArgb(58, 15, 23, 42))
        try {
            $graphics.FillPath($shadowBrush, $shadowPath)
        }
        finally {
            $shadowBrush.Dispose()
            $shadowPath.Dispose()
        }

        $tileRectangle = [System.Drawing.RectangleF]::new(
            12 * $scale,
            12 * $scale,
            216 * $scale,
            216 * $scale)
        $tilePath = New-RoundedRectanglePath $tileRectangle (48 * $scale)
        $tileBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            $tileRectangle,
            [System.Drawing.Color]::FromArgb(37, 99, 235),
            [System.Drawing.Color]::FromArgb(8, 145, 178),
            42.0)
        $tileBorder = [System.Drawing.Pen]::new(
            [System.Drawing.Color]::FromArgb(185, 255, 255, 255),
            5 * $scale)
        try {
            $graphics.FillPath($tileBrush, $tilePath)
            $graphics.DrawPath($tileBorder, $tilePath)
        }
        finally {
            $tileBorder.Dispose()
            $tileBrush.Dispose()
            $tilePath.Dispose()
        }

        $paneBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::White)
        try {
            $paneGap = 8 * $scale
            $paneSize = 43 * $scale
            $paneLeft = 43 * $scale
            $paneTop = 50 * $scale
            $graphics.FillRectangle($paneBrush, $paneLeft, $paneTop, $paneSize, $paneSize)
            $graphics.FillRectangle(
                $paneBrush,
                $paneLeft + $paneSize + $paneGap,
                $paneTop,
                $paneSize,
                $paneSize)
            $graphics.FillRectangle(
                $paneBrush,
                $paneLeft,
                $paneTop + $paneSize + $paneGap,
                $paneSize,
                $paneSize)
            $graphics.FillRectangle(
                $paneBrush,
                $paneLeft + $paneSize + $paneGap,
                $paneTop + $paneSize + $paneGap,
                $paneSize,
                $paneSize)
        }
        finally {
            $paneBrush.Dispose()
        }

        $shieldPoints = [System.Drawing.PointF[]]@(
            (Convert-Point 171 101 $scale),
            (Convert-Point 221 120 $scale),
            (Convert-Point 216 172 $scale),
            (Convert-Point 199 204 $scale),
            (Convert-Point 171 225 $scale),
            (Convert-Point 143 204 $scale),
            (Convert-Point 126 172 $scale),
            (Convert-Point 121 120 $scale)
        )
        $shieldPath = [System.Drawing.Drawing2D.GraphicsPath]::new()
        $shieldPath.AddPolygon($shieldPoints)
        $shieldBounds = [System.Drawing.RectangleF]::new(
            121 * $scale,
            101 * $scale,
            100 * $scale,
            124 * $scale)
        $shieldBrush = [System.Drawing.Drawing2D.LinearGradientBrush]::new(
            $shieldBounds,
            [System.Drawing.Color]::FromArgb(251, 191, 36),
            [System.Drawing.Color]::FromArgb(234, 88, 12),
            90.0)
        $shieldBorder = [System.Drawing.Pen]::new(
            [System.Drawing.Color]::White,
            7 * $scale)
        $shieldBorder.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        try {
            $graphics.FillPath($shieldBrush, $shieldPath)
            $graphics.DrawPath($shieldBorder, $shieldPath)
        }
        finally {
            $shieldBorder.Dispose()
            $shieldBrush.Dispose()
            $shieldPath.Dispose()
        }

        $checkPen = [System.Drawing.Pen]::new(
            [System.Drawing.Color]::FromArgb(30, 64, 175),
            13 * $scale)
        $checkPen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $checkPen.EndCap = [System.Drawing.Drawing2D.LineCap]::Round
        $checkPen.LineJoin = [System.Drawing.Drawing2D.LineJoin]::Round
        try {
            $graphics.DrawLines(
                $checkPen,
                [System.Drawing.PointF[]]@(
                    (Convert-Point 145 163 $scale),
                    (Convert-Point 165 183 $scale),
                    (Convert-Point 199 144 $scale)))
        }
        finally {
            $checkPen.Dispose()
        }
    }
    finally {
        $graphics.Dispose()
    }

    $bitmap = [System.Drawing.Bitmap]::new(
        $Size,
        $Size,
        [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $outputGraphics = [System.Drawing.Graphics]::FromImage($bitmap)
    try {
        $outputGraphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
        $outputGraphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $outputGraphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        $outputGraphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality
        $outputGraphics.DrawImage(
            $canvas,
            [System.Drawing.Rectangle]::new(0, 0, $Size, $Size),
            0,
            0,
            $canvas.Width,
            $canvas.Height,
            [System.Drawing.GraphicsUnit]::Pixel)
    }
    finally {
        $outputGraphics.Dispose()
        $canvas.Dispose()
    }

    return $bitmap
}

$outputDirectory = Split-Path -Parent $OutputPath
if (-not [string]::IsNullOrWhiteSpace($outputDirectory)) {
    [System.IO.Directory]::CreateDirectory((Join-Path (Get-Location) $outputDirectory)) | Out-Null
}

$images = [System.Collections.Generic.List[object]]::new()
foreach ($size in $sizes) {
    $bitmap = New-AppIconBitmap $size
    $stream = [System.IO.MemoryStream]::new()
    try {
        $bitmap.Save($stream, [System.Drawing.Imaging.ImageFormat]::Png)
        $images.Add([pscustomobject]@{
            Size = $size
            Bytes = $stream.ToArray()
            Bitmap = $bitmap
        })
    }
    finally {
        $stream.Dispose()
    }
}

try {
    $output = [System.IO.FileStream]::new(
        (Join-Path (Get-Location) $OutputPath),
        [System.IO.FileMode]::Create,
        [System.IO.FileAccess]::Write,
        [System.IO.FileShare]::None)
    $writer = [System.IO.BinaryWriter]::new($output)
    try {
        $writer.Write([uint16]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]$images.Count)
        $offset = 6 + (16 * $images.Count)
        foreach ($image in $images) {
            $dimension = if ($image.Size -eq 256) { 0 } else { $image.Size }
            $writer.Write([byte]$dimension)
            $writer.Write([byte]$dimension)
            $writer.Write([byte]0)
            $writer.Write([byte]0)
            $writer.Write([uint16]1)
            $writer.Write([uint16]32)
            $writer.Write([uint32]$image.Bytes.Length)
            $writer.Write([uint32]$offset)
            $offset += $image.Bytes.Length
        }

        foreach ($image in $images) {
            $writer.Write([byte[]]$image.Bytes)
        }
    }
    finally {
        $writer.Dispose()
        $output.Dispose()
    }

    $previewDirectory = Split-Path -Parent $PreviewPath
    if (-not [string]::IsNullOrWhiteSpace($previewDirectory)) {
        [System.IO.Directory]::CreateDirectory((Join-Path (Get-Location) $previewDirectory)) | Out-Null
    }

    $preview = [System.Drawing.Bitmap]::new(920, 430)
    $previewGraphics = [System.Drawing.Graphics]::FromImage($preview)
    try {
        $previewGraphics.Clear([System.Drawing.Color]::FromArgb(244, 247, 251))
        $titleFont = [System.Drawing.Font]::new("Segoe UI", 22, [System.Drawing.FontStyle]::Bold)
        $labelFont = [System.Drawing.Font]::new("Segoe UI", 11)
        $titleBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(15, 23, 42))
        $labelBrush = [System.Drawing.SolidBrush]::new([System.Drawing.Color]::FromArgb(71, 85, 105))
        try {
            $previewGraphics.DrawString("Windows Admin Center", $titleFont, $titleBrush, 34, 24)
            $previewGraphics.DrawString(
                "Multi-size ICO: прозрачный фон, Windows-инструменты и защитный щит",
                $labelFont,
                $labelBrush,
                38,
                68)
            $previewGraphics.DrawImage($images[-1].Bitmap, 42, 118, 256, 256)

            $x = 356
            $y = 126
            foreach ($image in $images) {
                if ($image.Size -eq 256) {
                    continue
                }

                $cellWidth = 130
                $previewGraphics.DrawImage(
                    $image.Bitmap,
                    $x + (($cellWidth - $image.Size) / 2),
                    $y,
                    $image.Size,
                    $image.Size)
                $previewGraphics.DrawString(
                    "$($image.Size) px",
                    $labelFont,
                    $labelBrush,
                    $x + 38,
                    $y + [Math]::Max($image.Size, 64) + 12)
                $x += $cellWidth
                if ($x -gt 800) {
                    $x = 356
                    $y += 140
                }
            }
        }
        finally {
            $labelBrush.Dispose()
            $titleBrush.Dispose()
            $labelFont.Dispose()
            $titleFont.Dispose()
        }

        $preview.Save(
            (Join-Path (Get-Location) $PreviewPath),
            [System.Drawing.Imaging.ImageFormat]::Png)
    }
    finally {
        $previewGraphics.Dispose()
        $preview.Dispose()
    }
}
finally {
    foreach ($image in $images) {
        $image.Bitmap.Dispose()
    }
}

Write-Output "ICON_PATH=$((Get-Item -LiteralPath $OutputPath).FullName)"
Write-Output "ICON_ENTRIES=$($images.Count)"
Write-Output "ICON_SHA256=$((Get-FileHash -Algorithm SHA256 -LiteralPath $OutputPath).Hash)"
Write-Output "PREVIEW_PATH=$((Get-Item -LiteralPath $PreviewPath).FullName)"
