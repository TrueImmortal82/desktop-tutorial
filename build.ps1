$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\WindowsAdminShortcuts\WindowsAdminShortcuts.csproj"
$tests   = Join-Path $PSScriptRoot "tests\WindowsAdminShortcuts.Tests\WindowsAdminShortcuts.Tests.csproj"
$output  = Join-Path $PSScriptRoot "dist"

dotnet run --project $tests --configuration Release

if (Test-Path $output) {
    Remove-Item $output -Recurse -Force
}

dotnet publish $project `
    --configuration Release `
    --runtime win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    --output $output

Copy-Item (Join-Path $PSScriptRoot "Start-WindowsAdminShortcuts.bat") `
    (Join-Path $output "Start-WindowsAdminShortcuts.bat")

Write-Host "Готово: $output\WindowsAdminShortcuts.exe" -ForegroundColor Green
