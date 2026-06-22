# Jellyfin Arr Links — task runner (https://just.systems)

sdk        := "mcr.microsoft.com/dotnet/sdk:9.0"
project    := "Jellyfin.Plugin.Arr.csproj"
plugin_dir := env_var('HOME') / "Library/Application Support/jellyfin/plugins/Arr Links_1.0.0.0"

# list recipes
default:
    @just --list

# build the plugin in Docker (no local .NET SDK needed) -> dist/
build:
    docker run --rm -v "$PWD":/src -w /src {{sdk}} dotnet build {{project}} -c Release -nologo
    mkdir -p dist
    cp bin/Release/net9.0/Jellyfin.Plugin.Arr.dll dist/
    cp meta.json dist/

# run the inject.js self-check
test:
    node Web/inject.test.js

# build, then copy the DLL into the local Jellyfin plugins folder (restart Jellyfin after)
deploy: build
    cp dist/Jellyfin.Plugin.Arr.dll "{{plugin_dir}}/"
    @echo "Deployed. Quit & reopen Jellyfin, then hard-refresh the browser."

# delete all build artefacts
clean:
    rm -rf bin obj dist
