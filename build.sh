#!/bin/bash

SCRIPT_DIR="$(dirname "$(realpath $0)")"
BUILD_DIR="$SCRIPT_DIR/build"
FRONTEND_DIR="$SCRIPT_DIR/SDSM-Frontend"
BACKEND_DIR="$SCRIPT_DIR/SDSM-Backend"

if [ -d $BUILD_DIR ]; then
    rm -r $BUILD_DIR && echo "Deleting directory $BUILD_DIR"
fi
mkdir $BUILD_DIR && echo "Created directory $BUILD_DIR"

# Copy CSS and JS files to build dirbuild dir
cp -r "$FRONTEND_DIR/css" "$BUILD_DIR/css"
cp -r "$FRONTEND_DIR/js" "$BUILD_DIR/js"
cp -r "$FRONTEND_DIR/static" "$BUILD_DIR/static"

# Compile TypeScript files
tsc -p "$FRONTEND_DIR/ts/tsconfig.json" --outDir "$BUILD_DIR/js"

# Build dotnet files
dotnet build "$BACKEND_DIR/SDSM-Server/SDSM-Server.csproj" -o "$BUILD_DIR"

# Build plugins: copy manifest, publish backend assembly, compile UI
# TypeScript, copy other UI assets.
PLUGINS_DIR="$SCRIPT_DIR/SDSM-Plugins"
mkdir -p "$BUILD_DIR/plugins"
for plugin in "$PLUGINS_DIR"/*/; do
    id="$(basename "$plugin")"
    mkdir -p "$BUILD_DIR/plugins/$id"
    cp "$plugin/manifest.json" "$BUILD_DIR/plugins/$id/"
    if ls "$plugin"backend/*.csproj > /dev/null 2>&1; then
        dotnet publish "$plugin"backend/*.csproj -o "$BUILD_DIR/plugins/$id/backend" --nologo -v quiet
    fi
    if [ -d "$plugin/ui" ]; then
        mkdir -p "$BUILD_DIR/plugins/$id/ui"
        tsc -p "$plugin/ui/tsconfig.json" --outDir "$BUILD_DIR/plugins/$id/ui"
        find "$plugin/ui" -type f ! -name '*.ts' ! -name 'tsconfig.json' \
            -exec cp {} "$BUILD_DIR/plugins/$id/ui/" \;
    fi
done
