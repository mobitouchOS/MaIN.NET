param(
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$arguments
)

# Initialize variables
$hard = $false
$models = @()
$noApi = $false
$apiOnly = $false
$noModels = $false

# Parse command-line arguments
# First, ensure arguments are split properly if they came as a single string
if ($arguments.Count -eq 1) {
    $arguments = $arguments[0] -split '\s+(?=--)'
}

foreach ($arg in $arguments) {
    Write-Host "Processing argument: $arg" # Debug line
    
    if ($arg -eq '--hard') {
        $hard = $true
    }
    elseif ($arg -match '^--models=(.+)$') {
        $models = $matches[1] -split ','
    }
    elseif ($arg -eq '--no-api') {
        $noApi = $true
    }
    elseif ($arg -eq '--api-only') {
        $apiOnly = $true
    }
    elseif ($arg -eq '--no-models') {
        $noModels = $true
    }
    elseif ($arg.Trim()) {
        Write-Host "Warning: Unknown argument '$arg'" -ForegroundColor Yellow
    }
}

# Show parsed arguments for debugging
Write-Host "Parsed arguments:"
Write-Host "Hard: $hard"
Write-Host "Models: $($models -join ', ')"
Write-Host "No API: $noApi"
Write-Host "API Only: $apiOnly"
Write-Host "No Models: $noModels"

# Run setup tasks unless --api-only is provided
if (-not $apiOnly) {
    # Handle model downloads unless --no-models is specified
    if (-not $noModels) {
        Write-Host "Starting model downloads..."
        if ($models.Count -gt 0) {
            & "$PSScriptRoot\download-models.ps1" -models $models
        } else {
            & "$PSScriptRoot\download-models.ps1"
        }
    }
}

# Start API unless --no-api is specified
if (-not $noApi) {
    Write-Host "Starting main API..."
    if ($hard) {
        & "$PSScriptRoot\start-api.ps1" -hard
    } else {
        & "$PSScriptRoot\start-api.ps1"
    }
}