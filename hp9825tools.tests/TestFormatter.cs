using HP9825CPU;

namespace hp9825tools.tests;

public class TestFormatter
{
    [SetUp]
    public void Setup()
    {
    }

    [Test]
    public void CommentLineOnly()
    {
        var fmt = new CodeFormatOptions();
        var cl = AssemblyLine.FromComment(SourceLineRef.Unknown, 123, " Testing?");
        var p = new ListingPrinter(opts: fmt);
        cl.CreateOutput(p);

        Assert.That(p.ToString(), Is.EqualTo("00001000               * TESTING?\n\n\n"));
    }

    [Test]
    public void TestFromatNoLineNumbers()
    {
        var fmt = new CodeFormatOptions() { IncludeAddress = false, IncludeLineNumbers = false, IncludeValues = false };
        var cl = AssemblyLine.FromComment(SourceLineRef.Unknown, 123, " Testing?");
        var p = new ListingPrinter(opts: fmt);
        cl.CreateOutput(p);

        Assert.That(p.ToString(), Is.EqualTo("* TESTING?\n\n\n"));
    }
}