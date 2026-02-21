      HED HP9825A RAM Checker, by Atkelar
*
* Boot location - forwart jump to actual boot code, past the constant list.
*
      ORG 40
SYSS  JMP *+1,I           JUMP TO BOOT LOCATION
      BSS 1               PLACEHOLDER FOR ADDRESS
*
*  Constants
*
P0    OCT 0               # 0-constant
P2    OCT 2
P3    OCT 3
P4    OCT 4
P6    OCT 6               # 6-constant
P16   DEC 16
P12   DEC 12              # 12-constant
P20   DEC 20              # 20-constant
P23   DEC 23
P24   DEC 24
SPACE DEC 32              # ASCII space.
DASH  DEC 45              ASCII "-"
B30   OCT 30
B1    OCT 1
P1    EQU B1
B10   OCT 10
B20   OCT 20
B40   OCT 40
B100  OCT 100
B120  OCT 120
M8    DEC -8
CH0   DEC 48              ASCII 0
CH1   DEC 49              ASCII 1
RTSBA OCT 76000           RAM test start block
RTBS  OCT 2000            RAM test block size
RTBSM DEC -1024           RAM test block decrement size
RTEBA OCT 30000           RAM test last block to be checked... (just above system ROM)
*
*
FLAG  OCT 100000          value taken from system ROM "delay .5s" code...
*
* Incredible: the call stack ("JSM" stack) hack works! The CPU
* happily stores and retreives return addresses in the AR2 register!
* This alone makes the code much easier to read!
*
RTRK  OCT 17              address of "AR2-1" register; to serve as a call stack!
*
PCNT  OCT 7               # of patterns to test... table follows!
*
* Problem: loading a number "indirectly" will fail for numbers with bit 15 set, because it
* will be treated as indirection continuation. "solution": split the pattern in two half words...
*  HIGH byte as word first, low byte second...
*
PATAD DEF *+1
      OCT 0
      OCT 0
      OCT 377
      OCT 377
      OCT 252
      OCT 252
      OCT 125
      OCT 125
      OCT 360
      OCT 360
      OCT 17
      OCT 17
      OCT 377
      OCT 360
*
*
*   Strings and contstantes for program flow
*
* Boot messages for hot/cold boot case.
CBTMS ASC 12,Atkelar's RAM Check-Cold
HBTMS ASC 12,Atkelar's RAM Check-Hot
* printer header / footer for a nifty report design... ahem...
PRTM1 ASC 8,RAM Check...
PRTM2 ASC 8,...done.
* display output for end of program.
DNMSG ASC 3,-done.
*
* the "address" display is taking the upper bits from the blocks
* (shift by 10 bits) and adds this value for an ASCII char.
* "top of memory" - or 76000B block addres - should arrive at "Z"
*
ADDBS DEC 59              Value to add to 0x1F to arrive at ASCII char...
*
* read and write indicators for display.
RDCHR DEC 114             ASCII "r"
WRCHR DEC 119             ASCII "w"
*
* Addresses from message content.
.CBTM DEF CBTMS
.HBTM DEF HBTMS
.PRM1 DEF PRTM1
.PRM2 DEF PRTM2
.DNMS DEF DNMSG
*
*
* actual startup location
*
      ORG 500
.INT  LDA KPA             Keyb. Select code
      STA PA              set peripheral addr
      LDA RTRK
      STA R               set up "introvert stack"
      LDA R5              read system status
      SAR 3
      RLA *+2
      JMP RESET
*
* cold boot here...
*
      LDA .CBTM
      LDB P24
      JSM DISP!
      JMP RAMCK
*
* hot boot here... needs to push paper out a bit...
*
RESET JSM PAPFD           feed paper for a few lines...
      LDA .HBTM           print message for hot boot
      LDB P23
      JSM DISP!
      JMP RAMCK
*
*
* Memory test code starts here...
*
RAMCK LDA .PRM1
      JSM PRT!
      JSM RUNON           turn on run linght.
*
* Available registers for "general" use here..
* A, B, C, D, W
* IV (? upper 12 only. As long as no interrupts are used, this should be free.)
* DMAMA, DMAC (as long as DDR is asserted, these should be "free".)
*
* IV - store for counting down RAM block address... lower bits not needed.
* patern number in DMAPA
* W bad bit result...
*
* ram checking code goes here... brrrrr...
*
*
* init variables for main loop
      LDA RTSBA
      STA IV
*
* next "memory block" loops back here.
BLKLP LDA IV
      JSM DSPA!           show current address on display...
      LDA IV
      JSM PRTA!           add curretn address to printout...
      LDA IV
      LDB PCNT            start with first pattern
      SBL 1               mul 2 for 2-word workaround
      STB DMAPA           move to "counter"
      LDA P0
      STA W
*
* next "pattern in block" loops back here.
PATLP LDA DMAPA           read current block #
      DSZ A               count down...
      JMP NEXTP           not zero yet...
      JMP BLKDN           done with all blocks!
* yes, there is one more pattern.
NEXTP STA DMAPA           remember for next round...
      LDA IV
      STA C               write current block start to C
      LDB DMAPA
      ADB PATAD
      LDA B,I             load pattern...
      DSZ B
      NOP
      SAL 8
      IOR B,I
      STA D               remember pattern for compare...
      LDA DMAPA
      DSZ A
      STA DMAPA           byte/word trickery... second byte!
      LDB RTBS            block size in B
      JSM DSPW!           add a "w" to the display
      LDA D
*
* now we have: A = pattern to write, B = # of words, C = start address...
*
      DSZ C               decrement, becuase "place" increments first, places later...
      NOP                 ignore DSZ result
* write loop
WLP1  PWC A,I             push word A to C
      DSZ B               test for "block done"
      JMP WLP1            not yet.
      JSM DSPR!           add an "r" to the display output.
      JSM DLY!            delay a bit so we absolutely had a refresh cycle...
      JSM DLY!            delay a bit so we absolutely had a refresh cycle...
      LDA IV              all words are set to current "pattern"... set up for read back loop...
      STA C
      LDB RTBS
RLP1  WWC A,I             pull word from C to A
      CPA D               KEY POINT: same word read as writen?
      JMP NXWD
* this is wwhere we need to set all bits in W that differ between A and D...
* running out of registers... save "B" because we need A and B for comarison..
      STB DMAMA
      LDB D
* also, loop counting is out... unroll the 16-bit comparisons. A will end up with only
* the bits set that are different
* NOTE: this is basically a "hand rolled" 16-bit "A = A XOR B"... The HP CPU doesn't
*       have a built in XOR though. Or I completely missed it!
* compare strategy: highest bit in A != B, force bit set in A, RBR/RAR 1 - 16 times!
* SAP/SBP - skip if positive (b15=0), SAM/SBM - skip if minus (b15=1)
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RBR 1               BIT DONE
      RAR 1
      SAM *+3             A bit non zero?
      SBP *+5             B bit zero?
      JMP *+3
      SBP *+3             case of A bit non-zero...
      SAM *+2,C
      SAM *+1,S           BITS DIFFER
      RAR 1               WORD DONE!
* now OR A to W...
      IOR W
      STA W
      LDB DMAMA
NXWD  DSZ B
      JMP RLP1            next word if any
      JMP PATLP           next pattern if any...
*
* last pattern of current block has just been processed.
BLKDN LDA W
      JSM PRTME           print status for this block...
*
* check for next block...
      LDA IV
      CPA RTEBA
      JMP DONE!           this one was the last block!
      ADA RTBSM           add "negative block size", i.e. move down...
      STA IV
      JMP BLKLP           next block...
*
*
* all done!
DONE! LDA .PRM2           print footer line
      JSM PRT!
      JSM PAPFD           and feed out paper a bit.
      LDA .DNMS           display done message...
      LDB P6
      JSM DISP!
      JSM RUNOF           turn off run light
*
*
* twiddle thumbs
*
HLT   LDB M8
      LDA FLAG
      RIA *
      RIB *-2
      JMP HLT
*
*   control the RUN light...
*
RUNON LDA B10             Run Light on.
      STA R5
      RET 1
RUNOF LDA B20             Run Light off.
      STA R5
      RET 1
RUNXX LDA B30             TOGGLE Run Light
      STA R5
      RET 1
*
*
* prints a 1/0 line based on the bits in A.
* will hose "B" and "C"
PRTME LDB P16
      STA C
* wait for printer ready...
      LDA R5
      SAR 2
      RLA *-2             not ready yet...
PRMEL LDA C
      SAP ISZER
* print one
      LDA CH1
      JMP NXTBT
* print zero
ISZER LDA CH0
NXTBT STA R6
      LDA C
      SAL 1
      STA C
      DSZ B
      JMP PRMEL           more digits to go
      LDA B1
      STA R5              Flush printer buffer, start "printing".
      RET 1
*
* prints a full message line.
* A = address of message, must be ASC 8,... to have 16 chars!
*
PRT!  LDB P16
* set A(15) bit for stack op..
      SAP *+1,S
      STA C
* wait for printer ready...
      LDA R5
      SAR 2
      RLA *-2             not ready yet... TODO: add "out of paper" check too?
PPCL  WBC R6,I            Load char and push to display
      DSZ B               Done?
      JMP PPCL
      LDA B1
      STA R5              Flush.
      RET 1
*
* slight delay... overwrites A and does a count loop to pass some time..
DLY!  LDA 0
      NOP
      DSZ A
      JMP *-2
      RET 1
*
* prints the "A" register as an address, not touching any other registers,
* but waits for the printer to be done too, thus creating an implied delay.
*
PRTA! STA B               remember...
* wait for printer ready...
      LDA R5
      SAR 2
      RLA *-2             not ready yet...
      LDA B
      SAR 10              shift into range...
      ADA ADDBS           make into indicator char
      STA R6
      LDA SPACE
      STA R6
      LDA DASH
      STA R6
      LDA SPACE
      STA R6
      STA R6
      STA R6
      STA R6
      STA R6
      STA R6
      STA R6
      STA R6
      STA R6
      STA R6
      STA R6
      STA R6
      STA R6
* 16 chars done, and...
      LDA B1
      STA R5              Flush.
      RET 1
*
* Make a multiline paper feed on the printer
* - i.e. print n-lines of all space.
*
PAPFD LDA P3
      STA D
* wait for printer ready...
PFLL  LDA R5
      SAR 2
      RLA *-2             not yet...
      LDA SPACE           space char...
      LDB P16
PFCL  STA R6              Load char and push to display
      DSZ B               Done?
      JMP PFCL            fill next space.
      LDA B1
      STA R5              Flush.
      LDA D
      DSZ A
      JMP PFLL2           another line...
      RET 1
PFLL2 STA D
      JMP PFLL
*
*
* Display "address" range from A
*
DSPA! SAR 10              shift into range...
      ADA ADDBS           make into indicator char
      STA R4
      LDA P2
      STA R5
      RET 1
*
* display a write indicator character.
DSPW! LDA WRCHR
      STA R4
      LDA P2
      STA R5
      RET 1
*
* display a read indicator character.
DSPR! LDA RDCHR
      STA R4
      LDA P2
      STA R5
      RET 1
*
* Subroutine for "pushing some chars to display".
* we intentionally don't fill the display all the way, so
* messages "accumulate" on the right..
*  NOTE: this is sadly not realiable. The "current" location
*        seems to be changing after every "flush"...
*  entry: A => address of message
*         B => number of chars
* exit: message + space pushed to display.
*
DISP! SAP *+1,S           set A(15) bit for stack op..
      STA C
*
*  DSP code...
*  What we know from the system ROM.
*   1.  if bit 7 is set (MSB) the character will include the "cursor".
*   2.  Cursor type is written to R5 as either 40 or 100 (oct) to select INS/REP cursor.
*   2a. Default seems to be the "insert" (triangle) cursor.
*   3.  written chracters start from screen right and push other characters out the left.
*   4.  Writing a set bit 1 (0x02) to the control register (R5) "triggers" the display.
*       I *think* this has to do with the address decoding scheme, where the address is
*       shared between the CPU write and the display update reads...
*   6.  Writing bit 4 (0x10) to the control register turns off the run light, bit 3 (0x8)
*       turns it on.
*
DISP1 WBC R4,I            Load char and push to display
      DSZ B               Done?
      JMP DISP1
      LDA SPACE
      STA R4
      LDA P2              Trigger display update.
      STA R5
      RET 1
*
* FIXUP location pointer placeholders.
*   mostly done to check the "BSS replacement" and EQU defs of the assembler.
*
      ORG SYSS+1          # update the correct target address in the BSS part.
      DEF .INT
KPA   EQU P0
      END
