param(
    [Parameter(Mandatory = $true)]
    [string]$InputPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string[]]$Names
)

Add-Type -AssemblyName System.Drawing

if ($Names.Count -ne 4) {
    throw "Names must contain exactly 4 output filenames."
}

$resolvedInput = (Resolve-Path -LiteralPath $InputPath).Path
$resolvedOutput = (Resolve-Path -LiteralPath $OutputDirectory).Path

$bitmap = [System.Drawing.Bitmap]::new($resolvedInput)

try {
    $halfWidth = [int]([Math]::Floor($bitmap.Width / 2))
    $halfHeight = [int]([Math]::Floor($bitmap.Height / 2))

    $rectangles = @(
        [System.Drawing.Rectangle]::new(0, 0, $halfWidth, $halfHeight),
        [System.Drawing.Rectangle]::new($halfWidth, 0, $bitmap.Width - $halfWidth, $halfHeight),
        [System.Drawing.Rectangle]::new(0, $halfHeight, $halfWidth, $bitmap.Height - $halfHeight),
        [System.Drawing.Rectangle]::new($halfWidth, $halfHeight, $bitmap.Width - $halfWidth, $bitmap.Height - $halfHeight)
    )

    for ($i = 0; $i -lt 4; $i++) {
        $targetPath = Join-Path $resolvedOutput $Names[$i]
        $clone = $bitmap.Clone($rectangles[$i], $bitmap.PixelFormat)
        try {
            $clone.Save($targetPath, [System.Drawing.Imaging.ImageFormat]::Png)
        }
        finally {
            $clone.Dispose()
        }
    }
}
finally {
    $bitmap.Dispose()
}
