param(
    [string]$OutputName,
    [switch]$SkipChatService
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
if ([string]::IsNullOrWhiteSpace($OutputName)) {
    $timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $OutputName = "ventagram-local-deploy-$timestamp.tar.gz"
}

$outputPath = Join-Path $root $OutputName
$tempRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("ventagram-hostinger-package-" + [Guid]::NewGuid().ToString("N"))
$stage = Join-Path $tempRoot "ventagram"

New-Item -ItemType Directory -Path $stage | Out-Null

$items = @(
    "Ventagram.Web",
    "docker-compose.hostinger.yml",
    ".env.hostinger.example",
    "README.md",
    "DEPLOY_HOSTINGER.md"
)

if (-not $SkipChatService) {
    $items += "Ventagram.ChatService"
}

foreach ($item in $items) {
    $source = Join-Path $root $item
    if (-not (Test-Path -LiteralPath $source)) {
        continue
    }

    $destination = Join-Path $stage $item
    if (Test-Path -LiteralPath $source -PathType Container) {
        robocopy $source $destination /E /XD bin obj node_modules .git .vs /XF appsettings.Development.json *.user *.suo | Out-Null
        if ($LASTEXITCODE -gt 7) {
            throw "Fallo copiando $item con robocopy. Codigo: $LASTEXITCODE"
        }
        $global:LASTEXITCODE = 0
    } else {
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
}

if (Test-Path -LiteralPath $outputPath) {
    Remove-Item -LiteralPath $outputPath -Force
}

Push-Location $stage
try {
    tar -czf $outputPath .
    if ($LASTEXITCODE -ne 0) {
        throw "Fallo generando el paquete tar.gz."
    }
} finally {
    Pop-Location
    Remove-Item -LiteralPath $tempRoot -Recurse -Force
}

Write-Host "Paquete generado: $outputPath"
