# HP 9825A 

## The project

### Overview

This project was created by me (Atkelar) as a means of handling the 
CPU instructions in the HP 9825A.
The CPU is a 16-bit processor with 15-bit addressing, so we end up 
with 32k words or 64k bytes of addressable space.

While there are other applications of the same CPU (or so I hear) 
the 9825 has a very specific hardwired configuration, so all 
utilities are initially geared towards that end.

### Details

I could not find any assembler or disassembler, so when I repaired 
my own machine, I had to look at bit patterns and compare them to 
the instruction set reference on a bit-by-bit basis.
This got old rather quickly, and so I started to write a Q&D tool 
to disassemble the ROM code, and things escalated from there.

### Goals

The primary goal was to create an assemble and disassembler pair 
that would make it easy for me to write my own custom firmware. 
Mostly for RAM testing and similar low level development ideas. 

1.: get an assembler that can read all the instructions that the
original firmware listing has. That excludes a few exotic ones.

2.: Create a program infrastructure that isn't quite as dirty
as the first brut force tool I made.

## The C# Projects

### CommandLineUtils

This is a small library I came up with; to be as independent
of any other libararies as possible, I usually do my own
command line parsing. Since I have several CLI applications
in this project, I wanted to unify it a bit.
I had a similar but much more powerful self written library
with .NET 1 back in the day, but it became a bit defunct
over time (can you say "Turbo-Vision"?). 
Maybe I'll pick it up again from here? Who knows...

### hp9825cpu

This is the core library that knows all the odds and ends of
the machine code. Actual assembly/disassembly and source
code processing in both directions happens here.

### hp9825asm

The "host" process for the assembler. Command line parsing,
state management across lines of assembly code and output
handling.

### hp9825dasm

The "host" process for the disassembler. Command line parsing 
label management and output generatino. TODO!!

### hp9825utils

A "toolbox" application for various processing steps, like
inverting a binary image to fit the ROM format of the 
negative logic. TODO - expand and improve!

### hp9825tools.tests

Unit testing for the project. Can be improved, yes, but not
by myself :D - I am a firm believer in "don't write your own
tests" philosophy. I mostly use it to test for known bugs and
then keep the tests to avoid resurrected bugs.

### Samples

A few sample files to get started and test things out.

## Assembler Description

The original assembler was documented in the CPU documentation.
It was - literally! - talking in punch cards and punch tapes
for processing. So naturally, a few design choices are a bit odd
for today's environment. Still, I tried to mimic it as closely 
as possible. This starts with the instruction format. There are
no "empty" lines, since each line was a punch card and why would 
you add an empty one?

In general, there are two different formats for a line:
Comment and Instruction.

### Comment Line

A line that is 100% a comment is easy: it starts with an asterisk
in the first column. Anything after that is a comment.
My version will treat empty lines as "ignorable" and skip them,
but counts them for error references.

### Instruction Line Format

An instruction line generally has four parts to it: A label,
the instruction (mnemonic), arguments and a comment.

Only the instruction is mandatory, all other parts are "as needed".

Here is an example for a full line:

`LBL1   LDA B   Load A from B`

And here's one for a lone mnemonic:

`      EIR`

The punch card format made it easy (or mandatory) to think in
columns. And that's exactly what they did. The parser has no way
of realibly detecting which part is a label and which isn't, without
using the columns!

The original also only used upper case ASCII. Even in comments.
My version ignores the case on input, converts everything BUT comments
into upper case to match, and the code output can do the same for
comments to match the original but keeps the source code "readable" in
mixed case.


### Labels

A label - in my version - has the same syntax rules as the original.
It can have any of the following characters, but the first one must NOT
be a numeric digit, and the lable is at most five characters long:

  `A-Z, 0-9, !, /, $, ", ?, %, #, @, &, .`

So here's a few valid labels:

`MAIN`, `.`, `.9`, `.@B`, `?!`

If the first character is a non-white-space (and also not an asterisk, see above!)
then the assembler will treat it as a label. Any subsequent whitespaces are ignored
by my assembler, so it is not as strict as the original, but will happily accept
the original format.

### Mnemonics

There are quite a few instructions defined in the CPU architecture. Overall
I'd say it's pretty clean from the opcode perspective. Mnemonics are always 
three characters long. I'd recommend avoiding three-character labels for
that reason.

#### CPU Instructions

A CPU instruction - like LDA for example - will producse an opcode as a result.
Each opcode is exactly one word. There are no mulit-word instructions.
The parameters available for each instruction vary slightly but ther are common
sets of options...

#### Data Definitions

Some instructions will create words of data as output. Examples are BSS, DEF, 
ASC. They have different features and optins, but will all create a set of
words in the output.

#### Pocessing Instructions

A processing instruction controls the assembler during the process. Commands
include "HED", "SPC", "IFN", "END". 

#### Address Manipulation

The ORG and ORR instructions are specila cases. They move the "current" output
location around, thus, the address will change with such an instruction. Any
output producing command needs to have an "ORG" first.

### Options

The options per command are documented in the CPU guide. Some options allow
only numeric constants, others allow expressions. Expressions are rather simple
and can only have + or - as operator, which also serve as sign indicators for
numerics. Her's a few examples of valid expressions:

  * `123` - 123 as decimal
  * `123B` - octal 123 (83 decimal)
  * `R4`   - register R4
  * `MAIN` - Label (address) MAIN
  * `MAIN+2` - Label (address) MAIN plus 2 words.
  * `*-2`   - current location minus 2 words, i.e. 2 words before the current instruction.
  * `-3+2` - negative three plus two - i.e. negative 1.

#### Suffixes

Some commands support an additional option, or flag. THese are expressed in
a ",?" syntax.

  * `LDA B,I`  - Load A from B, indirect. i.e. "B" contains a memory address
                and the value from that memory location is loaded into A.

#### Constants

The CPU does not have multi-word instructions, so "literals" or "immediate" values
are not possible. This means that any literal value has to be stored in some location
In the original ROM, the location 177(oct) contains a zero, so every time we want to
load a zero to "A" we have the opcode for "LD A,177" - usually expressed via one of
several labels, like "LD A,KPA" or "LD A,P0" which both refer to the same location.


### Comments

Any text that is left over in the line after all the options are parsed will be treated
as a comment - sans the separation character. Comments are NOT trimmed to preserve any
indentation they might have in round-trip processes.

## Assembe It!

To run the assembler, start the assembler and provide the input filename.

`hp9825asm democode.asm -o democode.bin -len 12288`

The assembler will show the command line syntax and comments via a "-h" or "--help"
option, but here's the gist:

  * democode.asm - the input file.
  * -o democode.bin - the output filename. If not specified, the assembler will only print
                out the listing.
  * -len 12288 - the size of the output to produce.


Note that the assembler will always use the full 64k of memory, i.e. 32k words by default.
It is up to you to "pick" the range that you want to save. The system ROM of the 9825
is within the first 12k words - i.e. 12288 words.

