param(
    [string]$Source = "$PSScriptRoot\..\src\GameTranslator.App\Assets\GameTranslator.icon-master.png",
    [string]$OutputDirectory = "$PSScriptRoot\..\src\GameTranslator.App\Assets"
)

$ErrorActionPreference = "Stop"

Add-Type -AssemblyName System.Drawing

$sizes = @(16, 24, 32, 48, 64, 128, 256, 512, 1024)
$icoSizes = @(16, 24, 32, 48, 64, 128, 256)

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null

$sourceBitmap = [System.Drawing.Bitmap]::FromFile((Resolve-Path $Source))

try {
    foreach ($size in $sizes) {
        $target = New-Object System.Drawing.Bitmap(
            $size,
            $size,
            [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)

        try {
            $graphics = [System.Drawing.Graphics]::FromImage($target)

            try {
                $graphics.CompositingMode = [System.Drawing.Drawing2D.CompositingMode]::SourceCopy
                $graphics.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighSpeed
                $graphics.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
                $graphics.PixelOffsetMode = [System.Drawing.Drawing2D.PixelOffsetMode]::Half
                $graphics.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::None
                $graphics.DrawImage(
                    $sourceBitmap,
                    [System.Drawing.Rectangle]::new(0, 0, $size, $size),
                    0,
                    0,
                    $sourceBitmap.Width,
                    $sourceBitmap.Height,
                    [System.Drawing.GraphicsUnit]::Pixel)
            }
            finally {
                $graphics.Dispose()
            }

            $target.Save(
                (Join-Path $OutputDirectory "GameTranslator-$size.png"),
                [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $target.Dispose()
        }
    }
}
finally {
    $sourceBitmap.Dispose()
}

$pngPayloads = foreach ($size in $icoSizes) {
    ,([System.IO.File]::ReadAllBytes((Join-Path $OutputDirectory "GameTranslator-$size.png")))
}

$iconPath = Join-Path $OutputDirectory "GameTranslator.ico"
$stream = [System.IO.File]::Create($iconPath)
$writer = New-Object System.IO.BinaryWriter($stream)

try {
    $writer.Write([uint16]0)
    $writer.Write([uint16]1)
    $writer.Write([uint16]$icoSizes.Count)

    $offset = 6 + (16 * $icoSizes.Count)

    for ($index = 0; $index -lt $icoSizes.Count; $index++) {
        $size = $icoSizes[$index]
        $payload = $pngPayloads[$index]

        $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
        $writer.Write([byte]($(if ($size -eq 256) { 0 } else { $size })))
        $writer.Write([byte]0)
        $writer.Write([byte]0)
        $writer.Write([uint16]1)
        $writer.Write([uint16]32)
        $writer.Write([uint32]$payload.Length)
        $writer.Write([uint32]$offset)

        $offset += $payload.Length
    }

    foreach ($payload in $pngPayloads) {
        $writer.Write($payload)
    }
}
finally {
    $writer.Dispose()
    $stream.Dispose()
}

Write-Host "Generated $($sizes.Count) PNG files and $iconPath"
