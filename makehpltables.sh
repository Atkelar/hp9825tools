#!/bin/bash

./runutil mkhpl private/hp9825a-system.bin -hl -rom system
./runutil mkhpl private/hp9825a-system.bin -hl -rom sysmatha
./runutil mkhpl private/hp9825a-system.bin -hl -rom sysmathb
./runutil mkhpl private/STRING_T.BIN -rom strings
./runutil mkhpl private/ADVPGM_T.BIN -rom advpgm
./runutil mkhpl private/GENIO_T.BIN -rom genericio
./runutil mkhpl private/EXTIO_T.BIN -rom extendedio
./runutil mkhpl private/PLOT62.BIN -rom plot62
./runutil mkhpl private/PLOT62.BIN -rom plot72
./runutil mkhpl private/SYSPGM.BIN -rom sysprog
./runutil mkhpl private/MATRIX.BIN -rom matrix

