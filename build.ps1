$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\WindowsAdminShortcuts\WindowsAdminShortcuts.csproj"
$output  = Join-Path $PSScriptRoot "dist"

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

& dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    --output $output

Write-Host "Готово: $output\WindowsAdminShortcuts.exe" -ForegroundColor Green
