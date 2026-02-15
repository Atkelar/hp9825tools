using HP9825CPU;

namespace hp9825tools.tests
{
    public class TestDisassembler
    {
        [SetUp]
        public void Setup()
        {
        }

        [Test]
        public void TestLoadRegister()
        {
            // opcode: 5 => LDA R5
            var cl = Disassembler.Disassemble(5, 7, ".INT", "test");
            Assert.That(cl.ToString(), Is.EqualTo(".INT  LDA R5              test"));
        }

        [Test]
        public void TestWithdrawWord()
        {
            // opcode: 5 => LDA R5
            // defaults for assembler: "I" for "Place", "D" for "Withdraw"
            var cl = Disassembler.Disassemble(0x7170, 0);
            Assert.That(cl.ToString(), Is.EqualTo("      WWC A,I"));
        }
        [Test]
        public void TestPlaceWord()
        {
            // opcode: 5 => LDA R5
            var cl = Disassembler.Disassemble(0x7168, 0 , includeDefaults: true);
            Assert.That(cl.ToString(), Is.EqualTo("      PWD A,I"));
        }

    //     [Test]
    //     public void TestStoreRegister()
    //     {
    //         var fmt = new CodeFormatOptions();
    //         var cl = Disassembler.Disassemble(Convert.ToInt32("034016", 8), Convert.ToInt32("10015", 8), ".INTL", "SAVE ADDRESS");
    //         var result = cl.CreateOutput(fmt);
    //         Assert.That(result, Is.EqualTo("10015  034016  .INTL STB C        SAVE ADDRESS"));  // actual source line in patent, page 366
    //     }

    //     [Test]
    //     public void TestBitMods()
    //     {
    //         // opcode: 172602 => SAM 2,C
    //         var fmt = new CodeFormatOptions();
    //         var cl = Disassembler.Disassemble(Convert.ToInt32("172602", 8), Convert.ToInt32("10255", 8), null, "CLEAR BIT 5 IN ALL CASES");
    //         var result = cl.CreateOutput(fmt);
    //         Assert.That(result, Is.EqualTo("10255  172602        SAM *+2,C    CLEAR BIT 5 IN ALL CASES"));  // actual source line in patent, page 366
    //    }
    }
}