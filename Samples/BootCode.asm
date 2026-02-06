    HED 9825A Boot Code Example
*
* This is just a test code.
* It won't really "boot", but it has most of the
* instructions and symbols defined that make up the boot code.
*
    UNL
* Just an example of "unliste source code".
* these lines should not appear on the list output.
    LST
*
* Empty lines are treated as full-line comments, just like '*' in the first column.
*
*
*
* SYSTEM STARTUP
*
    ORG 40B
SYSS  JMP *+1,I JUMP TO BOOT LOCATION
    BSS 1      PLACEHOLDER FOR ADDRESS

* Some additional constatnts in the base-page.
    ORG 77633B
JSTAK BSS 33
    ORG 154B
M8 DEC -8
    ORG 177B
P0 OCT 000000
    ORG 263B
FLAG OCT 100000

    ORG 300B
AJSTK DEF JSTAK-1
AJSMS DEF JSTAK

    HED Control Supervisor
*
*     CNSP
*
    ORG 10000B
* Power on Routines: Check reset bit
* finds the amoutn of R/W mem in system: zeros all R/W
* Memory; waits .5 sec for the cassette
*
*
.INT LDA KPA    Keyb. Select code
    STA D       set flag for auto start routines
    STA PA      set peripheral addr
    LDA R5      read system status
    SAR 3       position power-on bit
    RLA *+2     skip if power on
    *
    JMP RESET   reset key was pressed
    *
    LDB M8      wait .5 seconds
    LDA FLAG
    RIA *
    RIB *-2

    ORG 10073B
RESET LDA AJSTK

    ORG 10255B
    SAM LASRC,C
    SLA LASRC,C
LASRC RAR 10


    ORG SYSS+1
    DEF .INT

KPA EQU P0  
    END
