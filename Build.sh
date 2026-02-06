#!/bin/bash

netver="net8.0"

function packup() {
    pushd "$1"
    zip -r "hp985tools-$version-$2.zip" *
    popd
    mv $1/*.zip BuildOutput/
}

function buildandpack() {
    dotnet publish --configuration Release /p:Version="$version" -r "$1"
    packup "bin/Release/$netver/$1/publish" "$1"
}

version=$(head -n 1 .buildversion)

#cleanup first... careful with the paths!
rm -r bin/Release
dotnet publish --configuration Release /p:Version="$version"

packup "bin/Release/$netver/publish" "net8"

rm -r bin/Release
# /p:PublishTrimmed=true - didn't work; hosed the command line parser at a minimum...

buildandpack "win-x64"
buildandpack "linux-x64"
buildandpack "linux-musl-x64"
buildandpack "linux-arm64"
buildandpack "linux-arm"
buildandpack "osx-x64"

rm -r bin/Release
