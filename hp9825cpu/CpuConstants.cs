namespace HP9825CPU
{

    internal class CpuConstants
    {

        public const int MaskAll = 0xffff;
        public const int MaskTop12 = 0b1111111111110000;
        public const int MaskLower4 = 0b0000000000001111;
        public const int MaskLower3 = 0b0000000000000111;
        public const int MaskLower6 = 0b0000000000111111;
        public const int MaskLower5 = 0b0000000000011111;
        public const int MaskMemoryAddress = 0b0000011111111111;

        // Mnemonic replacement chars...
        public const char MCWordByte = '\u0001';
        public const char MCCorD = '\u0002';
        public const char MCIncOrDec = '\u0003';
        public const char MCAorB = '\u0004';
        public const char MCEmptyOrPop = '\u0005';
        public const char MCSetOrClear = '\u0006';
        public const char MCDirectOrIndirect = '\u0007';


        public static readonly MnemonicUpdateInfo WordByteMnemonicUpdate = new MnemonicUpdateInfo(MCWordByte, 'W', 'B');
        public static readonly MnemonicUpdateInfo CorDMnemonicUpdate = new MnemonicUpdateInfo(MCCorD, 'C', 'D');
        public static readonly MnemonicUpdateInfo AorBMnemonicUpdate = new MnemonicUpdateInfo(MCAorB, 'A', 'B');
        public static readonly MnemonicUpdateInfo IncrementDecrementMnemonicUpdate = new MnemonicUpdateInfo(MCIncOrDec, 'I', 'D');
        public static readonly MnemonicUpdateInfo EmptyOrPopMnemonicUpdate = new MnemonicUpdateInfo(MCEmptyOrPop, '\u0000', 'P');
        public static readonly MnemonicUpdateInfo SetOrClearMnemonicUpdate = new MnemonicUpdateInfo(MCSetOrClear, 'S', 'C');
        public static readonly MnemonicUpdateInfo ClearOrSetMnemonicUpdate = new MnemonicUpdateInfo(MCSetOrClear, 'C', 'S');
        public static readonly MnemonicUpdateInfo DirectOrIndirectMnemonicUpdate = new MnemonicUpdateInfo(MCDirectOrIndirect, '\u0000', 'I');

        // Register names are literal, except when they contain "[]"; these names are there 
        // to facilitate best-effort decoding of invalid instructions but will yield assembly errors.
        public static readonly string[] RegisterNames =
        {
            "A",
            "B",
            "P",
            "R",
            "R4",
            "R5",
            "R6",
            "R7",
            "IV",
            "PA",
            "W",
            "DMAPA",
            "DMAMA",
            "DMAC",
            "C",
            "D",
            "AR2",
            "AR2[1]",
            "AR2[2]",
            "AR2[3]",
            "SE",
            "X",
            "X[1]",
            "X[2]",
            "[30]",  // UNASSIGNED 30-37
            "[31]",
            "[32]",
            "[33]",
            "[34]",
            "[35]",
            "[36]",
            "[37]"
        };

        public static readonly CmdStructure[] Commands = {
            // first, do all 1:1 commands
            // EMC Codes...
            new CmdStructure(0b0_111_001_111_000_000, MaskAll, "CDC"),
            new CmdStructure(0b0_111_101_110_001_111, MaskAll, "MPY"),
            new CmdStructure(0b0_111_101_000_100_001, MaskAll, "FDV"),
            new CmdStructure(0b0_111_101_000_000_000, MaskAll, "FMP"),
            new CmdStructure(0b0_111_001_000_100_000, MaskAll, "CMY"),
            new CmdStructure(0b0_111_001_001_100_000, MaskAll, "CMX"),
            new CmdStructure(0b0_111_001_000_000_000, MaskAll, "MWA"),
            new CmdStructure(0b0_111_001_010_000_000, MaskAll, "FXA"),
            new CmdStructure(0b0_111_001_101_000_000, MaskAll, "NRM"),
            new CmdStructure(0b0_111_101_101_000_000, MaskAll, "NRY"),
            new CmdStructure(0b0_111_101_101_100_001, MaskAll, "MLY"),
            new CmdStructure(0b0_111_101_100_100_001, MaskAll, "DRS"),
            new CmdStructure(0b0_111_101_100_000_000, MaskAll, "MRX"),
            new CmdStructure(0b0_111_001_100_000_000, MaskTop12, "XFR", OperandType.NValue, MaskLower4),
            new CmdStructure(0b0_111_001_110_000_000, MaskTop12, "CLR", OperandType.NValue, MaskLower4),
            // IOC commands
            new CmdStructure(0b0_111_000_101_100_000, 0b1_111_011_101_110_000, $"P{MCWordByte}{MCCorD}", OperandType.RegIndex, MaskLower3)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, WordByteMnemonicUpdate),
                        new MnemonicUpdateSpec(0b0_000_000_000_010_000, CorDMnemonicUpdate),
                        new MnemonicUpdateSpec(0b0_000_000_010_000_000, IncrementDecrementMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_111_000_101_110_000, 0b1_111_011_101_110_000, $"W{MCWordByte}{MCCorD}", OperandType.RegIndex, MaskLower3)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, WordByteMnemonicUpdate),
                        new MnemonicUpdateSpec(0b0_000_000_000_010_000, CorDMnemonicUpdate),
                        new MnemonicUpdateSpec(0b0_000_000_010_000_000, IncrementDecrementMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_111_000_101_000_000, MaskAll, "CBU", true),
            new CmdStructure(0b0_111_000_101_000_000, MaskAll, "DBU", true),
            new CmdStructure(0b0_111_000_101_000_000, MaskAll, "CBL", true),
            new CmdStructure(0b0_111_000_101_000_000, MaskAll, "DBL", true),
            new CmdStructure(0b0_111_000_100_111_000, MaskAll, "DDR"),
            new CmdStructure(0b0_111_000_100_101_000, MaskAll, "PCM"),
            new CmdStructure(0b0_111_000_100_100_000, MaskAll, "DMA"),
            new CmdStructure(0b0_111_000_100_011_000, MaskAll, "DIR"),
            new CmdStructure(0b0_111_000_100_010_000, MaskAll, "EIR"),
            new CmdStructure(0b0_111_000_100_001_000, MaskAll, "SDI", true),
            new CmdStructure(0b0_111_000_100_000_000, MaskAll, "SDO", true),
            // BPC main command structure....
            new CmdStructure(0b0_000_000_000_000_000, MaskAll, "NOP"), // NOP is actually a LDA A - but decodes first as NOP for readability.
            new CmdStructure(0b1_111_000_111_000_000, 0b1_111_011_111_110_000, $"R{MCAorB}R", OperandType.NValue, MaskLower4)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate)
                    }
                },
            new CmdStructure(0b1_111_000_110_000_000, 0b1_111_011_111_110_000, $"S{MCAorB}L", OperandType.NValue, MaskLower4)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate)
                    }
                },
            new CmdStructure(0b1_111_000_101_000_000, 0b1_111_011_111_110_000, $"S{MCAorB}R", OperandType.NValue, MaskLower4)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate)
                    }
                },
            new CmdStructure(0b1_111_000_100_000_000, 0b1_111_011_111_110_000, $"A{MCAorB}R", OperandType.NValue, MaskLower4)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate)
                    }
                },
            new CmdStructure(0b1_111_000_010_000_000, 0b1_111_111_110_000_000, "RET", OperandType.SkipValueNonRelative, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_000_001_000_000, EmptyOrPopMnemonicUpdate, true) // add ",P" if bit is set...
                    }
                },
            new CmdStructure(0b1_111_000_001_100_000, 0b1_111_011_111_111_111, $"CM{MCAorB}")
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate)
                    }
                },
            new CmdStructure(0b1_111_000_000_100_000, 0b1_111_011_111_111_111, $"TC{MCAorB}")
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate)
                    }
                },
            // H flag is special... page 93.
            new CmdStructure(0b1_111_111_000_000_000, 0b1_111_111_000_000_000, $"SE{MCSetOrClear}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_000_100_000_000, SetOrClearMnemonicUpdate),
                        new MnemonicUpdateSpec(0b0_000_000_010_000_000, 0b0_000_000_001_000_000, ClearOrSetMnemonicUpdate, true)    // H bit is trigger bit for Clear or Set flag...
                    }
                },
            new CmdStructure(0b1_111_011_000_000_000, 0b1_111_111_000_000_000, $"SO{MCSetOrClear}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_000_100_000_000, SetOrClearMnemonicUpdate),
                        new MnemonicUpdateSpec(0b0_000_000_010_000_000, 0b0_000_000_001_000_000, ClearOrSetMnemonicUpdate, true)    // H bit is trigger bit for Clear or Set flag...
                    }
                },
            new CmdStructure(0b1_111_010_100_000_000, 0b1_111_011_100_000_000, $"S{MCAorB}M", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate),
                        new MnemonicUpdateSpec(0b0_000_000_010_000_000, 0b0_000_000_001_000_000, ClearOrSetMnemonicUpdate, true)    // H bit is trigger bit for Clear or Set flag...
                    }
                },
            new CmdStructure(0b1_111_010_000_000_000, 0b1_111_011_100_000_000, $"S{MCAorB}P", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate),
                        new MnemonicUpdateSpec(0b0_000_000_010_000_000, 0b0_000_000_001_000_000, ClearOrSetMnemonicUpdate, true)    // H bit is trigger bit for Clear or Set flag...
                    }
                },
            new CmdStructure(0b0_111_011_100_000_000, 0b1_111_011_100_000_000, $"RL{MCAorB}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate),
                        new MnemonicUpdateSpec(0b0_000_000_010_000_000, 0b0_000_000_001_000_000, ClearOrSetMnemonicUpdate, true)    // H bit is trigger bit for Clear or Set flag...
                    }
                },
            new CmdStructure(0b0_111_011_000_000_000, 0b1_111_011_100_000_000, $"SL{MCAorB}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate),
                        new MnemonicUpdateSpec(0b0_000_000_010_000_000, 0b0_000_000_001_000_000, ClearOrSetMnemonicUpdate, true)    // H bit is trigger bit for Clear or Set flag...
                    }
                },
            new CmdStructure(0b0_111_110_011_000_000, 0b1_111_111_011_000_000, $"SH{MCSetOrClear}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_000_100_000_000, SetOrClearMnemonicUpdate)
                    }
                },
            new CmdStructure(0b0_111_110_010_000_000, 0b1_111_111_011_000_000, $"SS{MCSetOrClear}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_000_100_000_000, SetOrClearMnemonicUpdate)
                    }
                },
            new CmdStructure(0b0_111_010_011_000_000, 0b1_111_111_011_000_000, $"SD{MCSetOrClear}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_000_100_000_000, SetOrClearMnemonicUpdate)
                    }
                },
            new CmdStructure(0b0_111_010_010_000_000, 0b1_111_111_011_000_000, $"SF{MCSetOrClear}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_000_100_000_000, SetOrClearMnemonicUpdate)
                    }
                },
            new CmdStructure(0b0_111_010_101_000_000, 0b1_111_011_111_000_000, $"SI{MCAorB}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate)
                    }
                },
            new CmdStructure(0b0_111_010_100_000_000, 0b1_111_011_111_000_000, $"SZ{MCAorB}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate)
                    }
                },
            new CmdStructure(0b0_111_010_001_000_000, 0b1_111_011_111_000_000, $"RI{MCAorB}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate)
                    }
                },
            new CmdStructure(0b0_111_010_000_000_000, 0b1_111_011_111_000_000, $"RZ{MCAorB}", OperandType.SkipValue, MaskLower6)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate)
                    }
                },
            new CmdStructure(0b0_111_000_000_000_000, 0b0_111_111_111_100_000, $"EXE", OperandType.RegIndex, MaskLower5)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b1_000_000_000_000_000, DirectOrIndirectMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_110_100_000_000_000, 0b0_111_100_000_000_000, $"JMP", OperandType.MemoryAddress, MaskMemoryAddress)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b1_000_000_000_000_000, DirectOrIndirectMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_110_000_000_000_000, 0b0_111_100_000_000_000, $"IOR", OperandType.MemoryAddress, MaskMemoryAddress)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b1_000_000_000_000_000, DirectOrIndirectMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_101_100_000_000_000, 0b0_111_100_000_000_000, $"DSZ", OperandType.MemoryAddress, MaskMemoryAddress)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b1_000_000_000_000_000, DirectOrIndirectMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_101_000_000_000_000, 0b0_111_100_000_000_000, $"AND", OperandType.MemoryAddress, MaskMemoryAddress)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b1_000_000_000_000_000, DirectOrIndirectMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_100_100_000_000_000, 0b0_111_100_000_000_000, $"ISZ", OperandType.MemoryAddress, MaskMemoryAddress)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b1_000_000_000_000_000, DirectOrIndirectMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_100_000_000_000_000, 0b0_111_100_000_000_000, $"JSM", OperandType.MemoryAddress, MaskMemoryAddress)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b1_000_000_000_000_000, DirectOrIndirectMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_011_000_000_000_000, 0b0_111_000_000_000_000, $"ST{MCAorB}", OperandType.MemoryAddress, MaskMemoryAddress)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate),
                        new MnemonicUpdateSpec(0b1_000_000_000_000_000, DirectOrIndirectMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_010_000_000_000_000, 0b0_111_000_000_000_000, $"AD{MCAorB}", OperandType.MemoryAddress, MaskMemoryAddress)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate),
                        new MnemonicUpdateSpec(0b1_000_000_000_000_000, DirectOrIndirectMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_001_000_000_000_000, 0b0_111_000_000_000_000, $"CP{MCAorB}", OperandType.MemoryAddress, MaskMemoryAddress)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate),
                        new MnemonicUpdateSpec(0b1_000_000_000_000_000, DirectOrIndirectMnemonicUpdate, true)
                    }
                },
            new CmdStructure(0b0_000_000_000_000_000, 0b0_111_000_000_000_000, $"LD{MCAorB}", OperandType.MemoryAddress, MaskMemoryAddress)
                {
                    Updates = new MnemonicUpdateSpec[] {
                        new MnemonicUpdateSpec(0b0_000_100_000_000_000, AorBMnemonicUpdate),
                        new MnemonicUpdateSpec(0b1_000_000_000_000_000, DirectOrIndirectMnemonicUpdate, true)
                    }
                }
        };

    }
}