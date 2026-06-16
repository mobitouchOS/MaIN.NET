param(
    [Parameter(Position=0)]
    [string]$command,
    
    [Parameter(ValueFromRemainingArguments=$true)]
    [string[]]$arguments
)

# Set script root as a global variable for other scripts to use
$global:MCLI_ROOT = $PSScriptRoot
$modelsPath = $env:MaIN_ModelsPath
function Get-ModelsMap {
    $modelsMapFile = "$PSScriptRoot\models_map.txt"
    if (-not (Test-Path $modelsMapFile)) {    
        Write-Host "Models map file not found at $modelsMapFile. Please provide a valid file."    
        return $null
    }
    
    $modelsMap = @{}
    Get-Content $modelsMapFile | ForEach-Object {    
        $split = $_.Split("|")
        $key = $split[0].Trim()
        $value = $split[1].Trim()  
        $modelsMap[$key] = $value
    }
    return $modelsMap
}

function Show-Usage {
    Write-Host @"
MaIN CLI (mcli) - Command Line Interface for MaIN

Usage:
    mcli <command> [options]

Commands:
    start-demo       Start all services (API and download models)
    api         Start only the API service
    model       Download and manage models
    help        Show this help message
    uninstall   Uninstall mcli

Options for 'start':
    --hard           Hard cleanup of containers
    --no-api         Skip starting the API
    --no-models      Skip model downloads
    --models=<list>  Specify comma-separated list of models to download

Options for 'api':
    --hard           Hard cleanup of containers

Options for 'model':
    download <name>   Download a specific model
    present          List installed models
    list             List available models
    update           Update all installed models

Examples:
    mcli start-demo
    mcli api --hard
    mcli model download gemma2-2b-maIN
    mcli help
"@
}

function Show-CommandHelp {
    param([string]$cmd)
    
    switch ($cmd) {
        "start-demo" { 
            Write-Host @"
mcli start-demo - Start all MaIN services

Usage:
    mcli start [options]

Options:
    --hard           Perform hard cleanup of containers before starting
    --no-api         Skip starting the API
    --no-models      Skip model downloads
    --models=<list>  Specify comma-separated list of models to download

Examples:
    mcli start-demo
    mcli start-demo --models=gemma2-2b-maIN
"@
        }
        "api" {
            Write-Host @"
mcli api - Start the MaIN API service

Usage:
    mcli api [options]

Options:
    --hard    Perform hard cleanup of containers before starting

Examples:
    mcli api
    mcli api --hard
"@
        }
        "model" {
            Write-Host @"
mcli model - Manage MaIN models

Usage:
    mcli model <subcommand>

Subcommands:
    download <name>   Download a specific model
    list             List available models
    present          List installed models
    update           Update all installed models

Examples:
    mcli model download llama2-7b
    mcli model list
    mcli model present
    mcli model update
"@
        }
        default {
            Show-Usage
        }
    }
}

# Handle commands
switch ($command) {
    "start-demo" {
        & "$PSScriptRoot\start.ps1" $arguments
    }
    "api" {
        & "$PSScriptRoot\start-api.ps1" $arguments
    }
    "model" {
        $subcommand = $arguments[0]
        $modelArgs = $arguments[1..($arguments.Length-1)]
        
        switch ($subcommand) {
            "download" {
                & "$PSScriptRoot\download-models.ps1" $modelArgs[0]
            }
            "list" {
                $modelsMap = Get-ModelsMap
                if ($null -eq $modelsMap) { return }
            
                Write-Host "Available models:"
                $modelsMap.Keys | Sort-Object | ForEach-Object {
                    Write-Host "- $_"
                }
            }
            "present" {
                Write-Host "Downloaded models:"
                Write-Host "Models path: $modelsPath"
                $downloadedModels = Get-ChildItem -Path $modelsPath -Filter "*.gguf" | 
                    Select-Object -ExpandProperty Name |
                    ForEach-Object { $_ -replace '\.gguf$','' }
                
                if ($downloadedModels.Count -eq 0) {
                    Write-Host "No models found in $modelsPath"
                } 
                else {
                    $downloadedModels | Sort-Object | ForEach-Object {
                        Write-Host "- $_"
                    }
                }
            }
            "update" {
                Write-Host "Updating all installed models..."
                & "$PSScriptRoot\download-models.ps1"
            }
            default {
                Show-CommandHelp "model"
            }
        }
    }
    "uninstall" {
        & "$PSScriptRoot\uninstall.ps1"
    }
    "help" {
        if ($arguments.Count -gt 0) {
            Show-CommandHelp $arguments[0]
        } else {
            Show-Usage
        }
    }
    default {
        Show-Usage
        if ($command) {
            Write-Host "`nError: Unknown command '$command'" -ForegroundColor Red
        }
    }
}

