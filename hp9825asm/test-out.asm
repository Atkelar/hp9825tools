      HDR HP9825A RAM Checker, by Atkelar
*    SPC 3
* Boot location - forwart jump to actual boot code, past the constant list.
      ORG 40
SYSS  JMP *+1,I           JUMP TO BOOT LOCATION
      BSS 1               PLACEHOLDER FOR ADDRESS
P0    OCT 0               # 0-constant
P2    OCT 2
P6    OCT 6               # 6-constant
B10   OCT 10
B20   OCT 20
M8    DEC -8
FLAG  OCT 100000
*
*   Strings and contstantes for program flow
*
      ORG 100
CBTMS ASC 6,RAM Check-C
HBTMS ASC 6,RAM Check-H
*
* Startup location.
*
.INT  LDA KPA             Keyb. Select code
      STA PA              set peripheral addr
      LDA R5              read system status
      SAR 3
      RLA *+2
      JMP RESET
*
* Cold boot.
*
      LDA HBTMS
      JMP MTSTS
*
* Hot boot.
*
RESET LDA HBTMS           * print message for hot boot
      JMP MTSTS
*
* Memory test code...
*
MTSTS STA C
      LDA KPA
      STA PA
      LDA B20             B20 = turn off run light, B10 = on. Bits 3+4 trigger J/K signals on R5 write
      STA R5
      LDB P6
DSLP1 WBC R4,I            Load char and push to display
      DSZ B               Done?
      JMP DSLP1
      LDA P2              Trigger display update.
      STA R5
*
* twiddle thumbs
*
HLT   LDB M8
      LDA FLAG
      RIA *
      RIB *-2
      LDA B10             B20 = turn off run light, B10 = on. Bits 3+4 trigger J/K signals on R5 write
      STA R5
      LDB M8
      LDA FLAG
      RIA *
      RIB *-2
      LDA B20             B20 = turn off run light, B10 = on. Bits 3+4 trigger J/K signals on R5 write
      STA R5
      JMP HLT
*
* FIXUP location pointer placeholders.
*
      ORG SYSS+1          # update the correct target address in the BSS part.
      DEF .INT
KPA   EQU P0
      END
