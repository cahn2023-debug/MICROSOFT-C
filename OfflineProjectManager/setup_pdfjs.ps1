$url = "https://github.com/mozilla/pdf.js/releases/download/v3.11.174/pdfjs-3.11.174-dist.zip"
$outputZip = "pdfjs.zip"
$extractPath = "d:\Code Antinigaty\Phan mem quan ly file V4_Python_co thu vien\MICROSOFT C\OfflineProjectManager\pdfjs"

Write-Host "Downloading PDF.js from $url..."
Invoke-WebRequest -Uri $url -OutFile $outputZip

if (Test-Path $outputZip) {
    Write-Host "Download complete. Extracting to $extractPath..."
    if (Test-Path $extractPath) {
        Remove-Item -Path $extractPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $extractPath -Force | Out-Null
    
    Expand-Archive -Path $outputZip -DestinationPath $extractPath -Force
    
    Write-Host "Extraction complete."
    Remove-Item -Path $outputZip
    
    # Verify viewer.html
    if (Test-Path "$extractPath\web\viewer.html") {
        Write-Host "Success: viewer.html found."
    } else {
        Write-Host "Error: viewer.html not found in extracted files."
    }
} else {
    Write-Host "Error: Download failed."
}
