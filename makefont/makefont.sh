#!/bin/bash

dotnet run -ps 16 -ds 4 -ov -con 1024 -out hp9825.svg
dotnet run -ps 16 -ds 4 -ov -con 1024 -inv -out hp9825-bold.svg

version=$(<"version.txt")

./fontconvert.pe hp9825.svg "HP9825Adigit" "HP 9825A Digital" "HP 9825A Digital Regular" "normal" "Font file copyright 2026 by Atkelar, based on HP design" $version
./fontconvert.pe hp9825-bold.svg "HP9825AdigitBold" "HP 9825A Digital" "HP 9825A Digital Bold" "bold" "Font file copyright 2026 by Atkelar, based on HP design" $version
