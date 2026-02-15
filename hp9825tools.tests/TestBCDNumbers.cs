using System.Threading.Tasks;
using HP9825CPU;

namespace hp9825tools.tests
{

    public class TestBCDNumbers
    {

        [Test]
        public async Task TestParse()
        {
            FloatingPointNumber f = FloatingPointNumber.Parse("0");
            Assert.That(f.IsZero, Is.True);
            f = FloatingPointNumber.Parse("1");
            Assert.That(f == FloatingPointNumber.One, Is.True);
            f = FloatingPointNumber.Parse("3.1415926536");
            Assert.That(f == FloatingPointNumber.Pi, Is.True);
            f = FloatingPointNumber.Parse("-.005");
            Assert.That(f.ToString(), Is.EqualTo("-5.00000000000e-3"));
            f = FloatingPointNumber.Parse("-.005e-6");
            Assert.That(f.ToString(), Is.EqualTo("-5.00000000000e-9"));
            f = FloatingPointNumber.Parse("-00000.005e-6");
            Assert.That(f.ToString(), Is.EqualTo("-5.00000000000e-9"));
            f = FloatingPointNumber.Parse("12345.9998923847298387429847");
            Assert.That(f.ToString(), Is.EqualTo("1.23459998923e4"));
        }

        [Test]
        public async Task TestSample()
        {
            FloatingPointNumber f = FloatingPointNumber.Parse("3.587219e-3");
            Assert.That(f.ToString(), Is.EqualTo("3.58721900000e-3"));
            Assert.That(f.M, Is.EqualTo(0b1_111111101_00000_0));
            Assert.That(f.M1, Is.EqualTo(0b0011_0101_1000_0111));
            Assert.That(f.M2, Is.EqualTo(0b0010_0001_1001_0000));
            Assert.That(f.M3, Is.EqualTo(0));
        }

        [Test]
        public async Task TestConstants()
        {
            Assert.That(FloatingPointNumber.Zero.IsValid, Is.True);
            Assert.That(FloatingPointNumber.One.IsValid, Is.True);
            Assert.That(FloatingPointNumber.MinusOne.IsValid, Is.True);
            Assert.That(FloatingPointNumber.Ten.IsValid, Is.True);
            Assert.That(FloatingPointNumber.Pi.IsValid, Is.True);

            Assert.That(FloatingPointNumber.Zero.IsZero, Is.True);
            Assert.That(FloatingPointNumber.One.IsZero, Is.False);
            Assert.That(FloatingPointNumber.MinusOne.IsZero, Is.False);
            Assert.That(FloatingPointNumber.Ten.IsZero, Is.False);
            Assert.That(FloatingPointNumber.Pi.IsZero, Is.False);

            Assert.That(FloatingPointNumber.Zero.IsNegative, Is.False);
            Assert.That(FloatingPointNumber.One.IsNegative, Is.False);
            Assert.That(FloatingPointNumber.MinusOne.IsNegative, Is.True);
            Assert.That(FloatingPointNumber.Ten.IsNegative, Is.False);
            Assert.That(FloatingPointNumber.Pi.IsNegative, Is.False);

            Assert.That(FloatingPointNumber.Zero.ToDouble(), Is.EqualTo(0));
            Assert.That(FloatingPointNumber.One.ToDouble(), Is.EqualTo(1));
            Assert.That(FloatingPointNumber.MinusOne.ToDouble(), Is.EqualTo(-1));
            Assert.That(FloatingPointNumber.Ten.ToDouble(), Is.EqualTo(10));
            Assert.That(FloatingPointNumber.Pi.ToDouble(), Is.EqualTo(3.14159265360));

            Assert.That(FloatingPointNumber.Zero.ToString(), Is.EqualTo("0.00000000000e0"));
            Assert.That(FloatingPointNumber.One.ToString(), Is.EqualTo("1.00000000000e0"));
            Assert.That(FloatingPointNumber.MinusOne.ToString(), Is.EqualTo("-1.00000000000e0"));
            Assert.That(FloatingPointNumber.Ten.ToString(), Is.EqualTo("1.00000000000e1"));
            Assert.That(FloatingPointNumber.Pi.ToString(), Is.EqualTo("3.14159265360e0"));
        }

    }
}