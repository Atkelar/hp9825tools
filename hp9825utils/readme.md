# Utility command set

Here is a general overview of the utility commands. First off, common parameter definitions:
The parameters are defined as a group (C# class) and can be re-used between sub-commands.
They can also be "prefixed" with a tag (written as "tag:") so the parameter "X" becomes
"tag:X" in that use case. Thus it is possible to have the same parameter set for different
use cases (i.e. two input files or two output file options that are otherwise identically
handled!) To avoid naming collisions, usually only one set of parameters is without a
prefix; typically that is the one with the most required or common paramters in it.

Parameters can be positional or by name. If "by name", they can be written with their
long name (--longname) or often a short alias (-l). Any value that is required needs to
follow in the next argument (`--test 1` or `-t 1`). There are flag arguments, which don't
take any value.


## Common Paramters

### Input File Options

 * InputFile - positional, mandatory -  The input file name (or base filename) to use. Defaults to .bin extension.
 * LittleEndian - flag (le) - When specified, the input file will be read as little endian. Normally big endian is used for 16-bit binaries. No effect when UseHighLow is also used.
 * Offset - default 0 (o) - The input file is loaded into a 'working memory'. Normally, it is loaded to address 0 in that, but it can be moved to any (word!) offset here.
 * Size - default "all bytes" (s) - The number of bytes to read; when not specified, reads until either the file or the working memory is exhausted. When specified, will be range checked.
 * Use16Bit - flag (u16) - When specified, assume 16-bit addressing mode. Extends the valid ragnes of offsets.
 * UseHighLow - flag (hl) - When specified, the input filename is interpreted as a base name and 'high.' and 'low.' is added for the double-byte version.

### Ouput File Options

 * File - positional, optional - The output filename. Defautls to a derived version of the input filename.
 * LittleEndian - flag (le) - When specified, the file will be written as little endian. Normally big endian is used for 16-bit binaries.
 * Offset - default 0 (o) - The offset in the working memory to start writing; Uses the same as the input offset if not provided.
 * Size - default "all bytes" (s) - The number of words to write. If not specified, uses the same number of words as the input.
 * UseHighLow - flag, (hl) - When specified, the output file is interpreted as a base name and 'high.' and 'low.' is added for the double-byte version.
 * Overwrite - flag, (ovr) - If the output file(s) already exists, it will be overwritten. If not specified, an existing output file will terminate the program.

## Invert Bits Command

The command uses the input file options without prefix, the output file options with the "o" prefix.

The purpose of this tool is to invert all the bits in the specified region.

## Create Image Command

The create image command creaes a blank binary image file. It uses the output options without prefix. The Output filename becomes mandatory in this case.

Additionally, there are the following parameters available:

 * Negate - flag (n) - create the empty file with all ones instead of all zero. Use as a baseline for EPROM programming images to keep "empty" sections at "zero" in the negative logic.

