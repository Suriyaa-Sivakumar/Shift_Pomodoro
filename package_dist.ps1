$ErrorActionPreference = "Stop"

$repoRoot = $PSScriptRoot
$publishDir = Join-Path $repoRoot "src\FlowWindows.App\bin\Release\net8.0-windows10.0.19041.0\win-x64\publish"
$distDir = Join-Path $repoRoot "dist\FlowWindows-x64"

Write-Host "1. Syncing publish output to $distDir..."
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Path $distDir -Force | Out-Null
}
Copy-Item "$publishDir\*" -Destination $distDir -Recurse -Force

Write-Host "2. Running build_pri.ps1 for dist\FlowWindows-x64..."
& (Join-Path $repoRoot "build_pri.ps1") -TargetDir $distDir

Write-Host "3. Verifying self-contained runtime config..."
$runtimeConfig = Get-Content (Join-Path $distDir "FlowWindows.App.runtimeconfig.json") -Raw
if ($runtimeConfig -match "includedFrameworks") {
    Write-Host "Confirmed: App is configured as 100% self-contained."
} else {
    Write-Warning "Warning: App might not be self-contained."
}

Write-Host "4. Packaging portable zip..."
$zipPath = Join-Path $repoRoot "dist\FlowWindows-v1.0.0-win-x64-portable.zip"
if (Test-Path $zipPath) { Remove-Item -Force $zipPath }
Compress-Archive -Path "$distDir\*" -DestinationPath $zipPath -Force
Write-Host "Portable zip packaged: $zipPath"

Write-Host "5. Compiling Inno Setup installer..."
$iscc = "C:\Users\Suriyaa Sivakumar\AppData\Local\Programs\Inno Setup 6\ISCC.exe"
$iss = Join-Path $repoRoot "installer\FlowWindows.iss"
& $iscc $iss
Write-Host "Setup installer packaged successfully."

