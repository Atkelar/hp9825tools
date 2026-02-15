using HP9825CPU;
using System;
using System.Collections.Generic;

namespace hp9825tools.tests
{

    public class TestAssembler
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestLoadRegister()
        {
            // opcode: 5 => LDA R5
            var cl = Assembler.Parse(SourceLineRef.Unknown, ".INT LDA R5 test", null, 7);
            var memory = HP9825CPU.Memory.MakeMemory(false);
            cl.ApplyTo(memory);
            Assert.That(memory[7], Is.EqualTo(5));
        }

        [Test]
        public void TestLoadIndirect()
        {
            // opcode: 5 => LDA R5
            var cl = Assembler.Parse(SourceLineRef.Unknown, ".INT LDA C,I test", null, 7);
            var memory = HP9825CPU.Memory.MakeMemory(false);
            cl.ApplyTo(memory);
            Assert.That(memory[7], Is.EqualTo(32782));
            Assert.That(cl.Comment, Is.EqualTo("test"));
            Assert.That(cl.Label, Is.EqualTo(".INT"));
        }

        [Test]
        public void TestStackOperations()
        {
            var memory = HP9825CPU.Memory.MakeMemory(false);
            var cl = Assembler.Parse(SourceLineRef.Unknown, "  WWC A,I", null, 7);
            cl.ApplyTo(memory);
            cl = Assembler.Parse(SourceLineRef.Unknown, "  PWD A,I", null, 8);
            cl.ApplyTo(memory);
            Assert.That(memory[7], Is.EqualTo(0x7170));
            Assert.That(memory[8], Is.EqualTo(0x7168));
        }

        [Test]
        public void TestLoadAddress()
        {
            var mgr = new LabelManager();
            mgr.SetOrg(7);
            var cl1 = Assembler.Parse(SourceLineRef.Unknown, ".INT LDA R5 test", mgr);
            var cl2 = Assembler.Parse(SourceLineRef.Unknown, "  JMP .INT test2", mgr);

            var memory = HP9825CPU.Memory.MakeMemory(false);
            cl1.ApplyTo(memory);
            cl2.ApplyTo(memory);
            Assert.That(mgr.GetPLC(), Is.EqualTo(9));
            Assert.That(memory[7], Is.EqualTo(5));
            Assert.That(memory[8], Is.EqualTo(26631));
        }


        [Test]
        public void TestWBCIncrement()
        {
            // opcode: 5 => LDA R5
            var cl = Assembler.Parse(SourceLineRef.Unknown, "    WBC R4,I  test", null, 7);
            var memory = HP9825CPU.Memory.MakeMemory(false);
            cl.ApplyTo(memory);
            Assert.That(memory[7], Is.EqualTo(0x7974));
            Assert.That(cl.Comment, Is.EqualTo("test"));
        }

        //  

        [Test]
        public void TestAscii()
        {
            // opcode: 5 => LDA R5
            var mgr = new LabelManager();
            mgr.SetOrg(7);
            var cl1 = Assembler.Parse(SourceLineRef.Unknown, "TTYP  ASC 3,ABCDE", mgr);

            var memory = HP9825CPU.Memory.MakeMemory(false);
            cl1.ApplyTo(memory);

            Assert.That(mgr.GetPLC(), Is.EqualTo(10));
            Assert.That(memory[7], Is.EqualTo(0x4142));
            Assert.That(memory[8], Is.EqualTo(0x4344));
            Assert.That(memory[9], Is.EqualTo(0x4520));
        }


        const string BootCode = @"
    ORG 77633B
JSTAK BSS 33
    ORG 40B
    *
    * SYSTEM STARTUP
    *
SYSS  JMP *+1,I
    BSS 1      ORIGINAL had placeholder

    ORG 154B
M8 DEC -8
    ORG 177B
P0 OCT 000000
    ORG 263B
FLAG OCT 100000
TESTF OCT 1,2,3

    ORG 300B
AJSTK DEF JSTAK-1
AJSMS DEF JSTAK

    ORG 10000B
.INT LDA KPA
    STA D
    STA PA
    LDA R5
    SAR 3
    RLA *+2
    *
    JMP RESET
    *
    LDB M8
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
        ";

        [Test]
        public void TestBootCodeRaw()
        {
            var mgr = new LabelManager();
            var sr = new System.IO.StringReader(BootCode);
            string? line;
            List<AssemblyLine> output = new List<AssemblyLine>();
            int ln = 0;
            while ((line = sr.ReadLine()) != null)
            {
                ln++;
                var cl = Assembler.Parse(new SourceLineRef("dummy", ln), line, mgr);
                output.Add(cl);
            }
            mgr.Relocate();
            var mem = Memory.MakeMemory(false);
            foreach (var cl in output)
            {
                cl.ApplyTo(mem);
            }

            var fmt = new CodeFormatOptions();
            var p = new ListingPrinter(opts: fmt);
            foreach (var cl in output)
            {
                cl.CreateOutput(p);
            }
            Console.WriteLine(p.ToString());
            Assert.That(mem[0x103B], Is.EqualTo(0xC0)); // check some key outputs. LDA AJSTK...
            Assert.That(mem[0x100A], Is.EqualTo(0x7C7E)); // RIB *-2...
            Assert.That(mem[0x10AD], Is.EqualTo(0xF582)); // SAM LASRC,C...
        }


        [Test]
        public void TestLabelValidatorPositives([Values(".ABCD", ".1234", "A.123", ".", "ABC12")] string check)
        {
            var x = Assembler.ValidLabel.IsMatch(check);
            Assert.That(x, Is.True);
        }
        [Test]
        public void TestLabelValidatorNegatives([Values("1.AB", "ABC123", "A*BC", " ABC")] string check)
        {
            var x = Assembler.ValidLabel.IsMatch(check);
            Assert.That(x, Is.False);
        }
    }
}