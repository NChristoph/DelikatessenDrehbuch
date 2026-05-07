# Migration Script: Azure Blob → Bunny.net Storage
# Kopiert alle Dateien von Azure Blob Storage zu Bunny.net

param(
    [Parameter(Mandatory=$true)]
    [string]$AzureConnectionString,

    [Parameter(Mandatory=$true)]
    [string]$AzureContainerName = "blob-world-mini-app",

    [Parameter(Mandatory=$true)]
    [string]$BunnyStorageUrl = "https://storage.bunnycdn.com/avocadon",

    [Parameter(Mandatory=$true)]
    [string]$BunnyAccessKey
)

Write-Host "🚀 Migration: Azure Blob → Bunny.net Storage" -ForegroundColor Cyan
Write-Host "Container: $AzureContainerName" -ForegroundColor Gray
Write-Host "Bunny URL: $BunnyStorageUrl" -ForegroundColor Gray
Write-Host ""

# Azure Storage Module prüfen
if (-not (Get-Module -ListAvailable -Name Az.Storage)) {
    Write-Host "⚠️  Az.Storage Modul nicht gefunden. Installiere mit:" -ForegroundColor Yellow
    Write-Host "   Install-Module -Name Az.Storage -Force -AllowClobber" -ForegroundColor Yellow
    exit 1
}

Import-Module Az.Storage

# Azure Blob Client erstellen
$ctx = New-AzStorageContext -ConnectionString $AzureConnectionString
$container = Get-AzStorageContainer -Name $AzureContainerName -Context $ctx

# Alle Blobs abrufen
Write-Host "📦 Lade Liste aller Blobs..." -ForegroundColor Yellow
$blobs = Get-AzStorageBlob -Container $AzureContainerName -Context $ctx
$totalBlobs = $blobs.Count

Write-Host "✅ Gefunden: $totalBlobs Dateien" -ForegroundColor Green
Write-Host ""

$successCount = 0
$errorCount = 0
$skippedCount = 0

foreach ($blob in $blobs) {
    $blobName = $blob.Name
    $progress = [math]::Round(($successCount + $errorCount + $skippedCount) / $totalBlobs * 100, 1)

    Write-Progress -Activity "Migration" -Status "$progress% - $blobName" -PercentComplete $progress

    try {
        # Download von Azure Blob
        $tempFile = [System.IO.Path]::GetTempFileName()
        Get-AzStorageBlobContent -Blob $blobName -Container $AzureContainerName -Context $ctx -Destination $tempFile -Force | Out-Null

        # Content-Type ermitteln
        $extension = [System.IO.Path]::GetExtension($blobName).ToLower()
        $contentType = switch ($extension) {
            ".webp" { "image/webp" }
            ".jpg"  { "image/jpeg" }
            ".jpeg" { "image/jpeg" }
            ".png"  { "image/png" }
            ".mp4"  { "video/mp4" }
            ".mov"  { "video/quicktime" }
            ".webm" { "video/webm" }
            default { "application/octet-stream" }
        }

        # Upload zu Bunny.net
        $bunnyUrl = "$BunnyStorageUrl/$blobName"
        $fileBytes = [System.IO.File]::ReadAllBytes($tempFile)

        $headers = @{
            "AccessKey" = $BunnyAccessKey
            "Content-Type" = $contentType
        }

        $response = Invoke-WebRequest -Uri $bunnyUrl -Method Put -Headers $headers -Body $fileBytes -UseBasicParsing

        if ($response.StatusCode -eq 201 -or $response.StatusCode -eq 200) {
            Write-Host "✅ $blobName" -ForegroundColor Green
            $successCount++
        } else {
            Write-Host "❌ $blobName - Status: $($response.StatusCode)" -ForegroundColor Red
            $errorCount++
        }

        # Temp-Datei löschen
        Remove-Item $tempFile -Force

    } catch {
        Write-Host "❌ $blobName - Fehler: $($_.Exception.Message)" -ForegroundColor Red
        $errorCount++
    }

    # Rate-Limiting: 100ms Pause zwischen Uploads
    Start-Sleep -Milliseconds 100
}

Write-Progress -Activity "Migration" -Completed

Write-Host ""
Write-Host "📊 Migration abgeschlossen:" -ForegroundColor Cyan
Write-Host "   ✅ Erfolgreich: $successCount" -ForegroundColor Green
Write-Host "   ❌ Fehler: $errorCount" -ForegroundColor Red
Write-Host "   ⏭️  Übersprungen: $skippedCount" -ForegroundColor Yellow
Write-Host ""

if ($errorCount -gt 0) {
    Write-Host "⚠️  Es gab Fehler. Bitte Logs prüfen." -ForegroundColor Yellow
    exit 1
} else {
    Write-Host "🎉 Alle Dateien erfolgreich migriert!" -ForegroundColor Green
    exit 0
}
