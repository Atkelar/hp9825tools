// using HP9825CPU;

// namespace hp9825tools.tests;

// public class TestDisassembler
// {
//     [SetUp]
//     public void Setup()
//     {
//     }

//     [Test]
//     public void TestLoadRegister()
//     {
//         // opcode: 5 => LDA R5
//         var fmt = new CodeFormatOptions();
//         var cl = Disassembler.Disassemble(5, 7, ".INT", "test");
//         // var result = cl.CreateOutput(fmt);
//         // Assert.That(result, Is.EqualTo("00007  000005  .INT  LDA R5       test"));
//     }

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
//     }
// }