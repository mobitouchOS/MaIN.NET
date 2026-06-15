param(
    [string]$InstallPath = "$env:LocalAppData\MaIN\CLI"
)
# Ensure we're running with admin privileges
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "Please run this script as Administrator" -ForegroundColor Red
    exit 1
}
# Create installation directory
if (-not (Test-Path $InstallPath)) {
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}
# Define files and directories to copy
$itemsToCopy = @{
    Files = @(
        "mcli.ps1",
        "start.ps1",
        "start-api.ps1",
        "download-models.ps1",
        "docker-compose.yml",
        "models_map.txt",
        ".models"
    )
    Directories = @(
        "server"
    )
}
# Copy files
foreach ($file in $itemsToCopy.Files) {
    if (Test-Path $file) {
        Write-Host "Copying file: $file"
        Copy-Item $file -Destination $InstallPath -Force
    } else {
        Write-Host "Warning: File not found: $file" -ForegroundColor Yellow
    }
}
# Copy directories
foreach ($dir in $itemsToCopy.Directories) {
    if (Test-Path $dir) {
        Write-Host "Copying directory: $dir"
        Copy-Item $dir -Destination $InstallPath -Recurse -Force
    } else {
        Write-Host "Warning: Directory not found: $dir" -ForegroundColor Yellow
    }
}
# Create batch file wrapper for easier execution
$batchContent = @"
@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%LocalAppData%\MaIN\CLI\mcli.ps1" %*
"@
$batchPath = "$env:SystemRoot\System32\mcli.cmd"
Set-Content -Path $batchPath -Value $batchContent -Force
# Add installation directory to PATH if not already present
$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
if ($userPath -notlike "*$InstallPath*") {
    [Environment]::SetEnvironmentVariable("PATH", "$userPath;$InstallPath", "User")
}
# Set execution policy for the current user
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned -Force
# Create uninstaller
$uninstallerContent = @'
$InstallPath = "$env:LocalAppData\MaIN\CLI"
$batchPath = "$env:SystemRoot\System32\mcli.cmd"
# Remove batch file
if (Test-Path $batchPath) {
    Remove-Item $batchPath -Force
}
# Remove installation directory
if (Test-Path $InstallPath) {
    Remove-Item $InstallPath -Recurse -Force
}
# Remove from PATH
$userPath = [Environment]::GetEnvironmentVariable("PATH", "User")
$userPath = ($userPath -split ';' | Where-Object { $_ -ne $InstallPath }) -join ';'
[Environment]::SetEnvironmentVariable("PATH", $userPath, "User")
Write-Host "MaIN CLI (mcli) has been uninstalled successfully!" -ForegroundColor Green
'@
Set-Content -Path "$InstallPath\uninstall.ps1" -Value $uninstallerContent
Write-Host @"
MaIN CLI (mcli) has been installed successfully!
Installation path: $InstallPath
Wrapper script: $batchPath
Directory structure created:
$InstallPath
$(Get-ChildItem $InstallPath -Recurse | ForEach-Object { "  " + $_.FullName.Replace($InstallPath, '') })
You can now use 'mcli' from any terminal. Try:
    mcli help
To uninstall, run:
    mcli uninstall
"@ -ForegroundColor Green