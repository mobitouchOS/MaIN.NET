#!/bin/bash

hard=false
models=()
noInfra=false
infraOnly=false

# Manually parse the command-line arguments for double-dash parameters
for arg in "$@"; do
    case $arg in
        --hard)
            hard=true
            ;;
        --models=*)
            modelsString="${arg#*=}"
            # Split the models string into an array, assuming comma-separated models
            IFS=',' read -ra models <<< "$modelsString"
            ;;
        --no-infra)
            noInfra=true
            ;;
        --infra-only)
            infraOnly=true
            ;;
    esac
done

# Run infrastructure-related tasks only if --no-infra is not provided or if --infra-only is provided
if [[ $noInfra == false || $infraOnly == true ]]; then
    # Determine models to pull: from parameter if provided, otherwise from file
    if [[ ${#models[@]} -gt 0 ]]; then
        echo "Using provided models list..."
    else
        echo "No models provided as parameter, reading from .models file..."
        # Read models from the .models file, ignoring comments and blank lines
        models=($(grep -v '^#' .models | grep -v '^$'))
    fi

    # Pull each model, ignoring comments if reading from file
    for model in "${models[@]}"; do
        echo "Pulling model: $model"
        ollama pull "$model"
    done

    sleep 10
    echo "Running the Ollama serve."
    sleep 5
fi

# Run Docker-related tasks only if --infra-only is not provided
if [[ $infraOnly == false ]]; then
    # Stop and remove Docker containers, networks, images (and volumes if --hard is provided)
    if [[ $hard == true ]]; then
        echo "Stopping and removing Docker containers, networks, images, and volumes..."
        docker compose down -v
    else
        echo "Stopping and removing Docker containers, networks, and images (volumes retained)..."
        docker compose down
    fi

    # Start Docker containers in detached mode
    echo "Starting Docker containers in detached mode..."
    docker compose up -d

    # Wait for 5 seconds to ensure the containers are up and running
    echo "Waiting for 5 seconds to ensure the containers are up and running..."
    sleep 5

    echo "
MMMMMMMM               MMMMMMMM                       AAA                       IIIIIIIIII        NNNNNNNN        NNNNNNNN
M:::::::M             M:::::::M                      A:::A                      I::::::::I        N:::::::N       N::::::N
M::::::::M           M::::::::M                     A:::::A                     I::::::::I        N::::::::N      N::::::N
M:::::::::M         M:::::::::M                    A:::::::A                    II::::::II        N:::::::::N     N::::::N
M::::::::::M       M::::::::::M                   A:::::::::A                     I::::I          N::::::::::N    N::::::N
M:::::::::::M     M:::::::::::M                  A:::::A:::::A                    I::::I          N:::::::::::N   N::::::N
M:::::::M::::M   M::::M:::::::M                 A:::::A A:::::A                   I::::I          N:::::::N::::N  N::::::N
M::::::M M::::M M::::M M::::::M                A:::::A   A:::::A                  I::::I          N::::::N N::::N N::::::N
M::::::M  M::::M::::M  M::::::M               A:::::A     A:::::A                 I::::I          N::::::N  N::::N:::::::N
M::::::M   M:::::::M   M::::::M              A:::::AAAAAAAAA:::::A                I::::I          N::::::N   N:::::::::::N
M::::::M    M:::::M    M::::::M             A:::::::::::::::::::::A               I::::I          N::::::N    N::::::::::N
M::::::M     MMMMM     M::::::M            A:::::AAAAAAAAAAAAA:::::A              I::::I          N::::::N     N:::::::::N
M::::::M               M::::::M           A:::::A             A:::::A           II::::::II        N::::::N      N::::::::N
M::::::M               M::::::M ......   A:::::A               A:::::A   ...... I::::::::I ...... N::::::N       N:::::::N
M::::::M               M::::::M .::::.  A:::::A                 A:::::A  .::::. I::::::::I .::::. N::::::N        N::::::N
MMMMMMMM               MMMMMMMM ...... AAAAAAA                   AAAAAAA ...... IIIIIIIIII ...... NNNNNNNN         NNNNNNN
"

    # Wait for all background jobs to complete
    echo "Listening on http://localhost:5001 - happy travels"
fi
