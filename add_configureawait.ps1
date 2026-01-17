# PowerShell script to add ConfigureAwait(false) to all service await statements

$serviceFiles = @(
    "d:\Code Antinigaty\Phan mem quan ly file V4_Python_co thu vien\MICROSOFT C\OfflineProjectManager\Features\Task\Services\TaskService.cs",
    "d:\Code Antinigaty\Phan mem quan ly file V4_Python_co thu vien\MICROSOFT C\OfflineProjectManager\Features\Project\Services\ProjectService.cs",
    "d:\Code Antinigaty\Phan mem quan ly file V4_Python_co thu vien\MICROSOFT C\OfflineProjectManager\Services\SearchService.cs",
    "d:\Code Antinigaty\Phan mem quan ly file V4_Python_co thu vien\MICROSOFT C\OfflineProjectManager\Services\ContentIndexService.cs",
    "d:\Code Antinigaty\Phan mem quan ly file V4_Python_co thu vien\MICROSOFT C\OfflineProjectManager\Services\IndexerService.cs"
)

foreach ($file in $serviceFiles) {
    if (Test-Path $file) {
        $content = Get-Content $file -Raw
        
        # Add ConfigureAwait(false) to await statements that don't have it
        # Pattern: await <expression>; where <expression> doesn't already contain ConfigureAwait
        $pattern = '(await\s+(?:(?!ConfigureAwait)[^;\r\n])+)(;)'
        $replacement = '$1.ConfigureAwait(false)$2'
        
        $newContent = $content -replace $pattern, $replacement
        
        if ($content -ne $newContent) {
            Set-Content -Path $file -Value $newContent -NoNewline
            Write-Host "Updated: $file"
        } else {
            Write-Host "No changes needed: $file"
        }
    }
}

Write-Host "`nConfigureAwait(false) applied to all service files!"
