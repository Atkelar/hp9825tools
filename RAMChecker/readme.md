# Atkelar's RAM checker...

## Assembly

The assembly is intented to go into the first 12k of system ROM - where the "mainframe" firmware usually is. Note: it's far smaller of course, but that was the memory layout that worked for the system ROMs, and so I copied it.
```
hp9825asm RAMChecker.asm -hl -len 12288 -o RAMChecker-out.bin
```

## ROM drawer

The code is written to fit into the 32k EPROMs that go with my system ROM replacement PCBs.

## EPROMs

To write the resulting files ot an EPROM (e.g. TC57256) use a command like:
``` shell
minipro -p "TC57256D@DIP28" -w output.high.bin -y
```

## it's working...

<img width="957" height="1280" alt="image" src="https://github.com/user-attachments/assets/dbcc9d03-be48-4ea0-b0ab-2142b5a88823" />
...mostly. Apparently, the display doesn't keep the "current position" when writing "half way" through. But better than nohing!

