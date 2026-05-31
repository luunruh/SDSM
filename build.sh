#!/bin/bash

SCRIPT_DIR="$(dirname "$(realpath $0)")"
BUILD_DIR="$SCRIPT_DIR/build"
FRONTEND_DIR="$SCRIPT_DIR/SDSM-Frontend"
BACKEND_DIR="$SCRIPT_DIR/SDSM-Backend"

if [ -d $BUILD_DIR ]; then
    rm -r $BUILD_DIR && echo "Deleting directory $BUILD_DIR"
fi
mkdir $BUILD_DIR && echo "Created directory $BUILD_DIR"

# Compile TypeScript files
tsc -p "$FRONTEND_DIR/ts/tsconfig.json" --outDir "$FRONTEND_DIR/js"

# Copy CSS and JS files to build dirbuild dir
cp -r "$FRONTEND_DIR/css" "$BUILD_DIR/css"
cp -r "$FRONTEND_DIR/js" "$BUILD_DIR/js"
cp -r "$FRONTEND_DIR/static" "$BUILD_DIR/static"

# Build dotnet files
dotnet build "$BACKEND_DIR/SDSM-Server/SDSM-Server.csproj" -o "$BUILD_DIR"
