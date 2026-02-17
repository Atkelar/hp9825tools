using System;
using System.Threading.Tasks;
using HP9825CPU;
using NUnit.Framework.Constraints;

namespace hp9825tools.tests
{
    public class TestSimulator
    {
        private CpuSimulator Prepare()
        {
            MemoryManager memory = new MemoryManager();
            memory.SetRam(new MemoryRange(0x7000, 0x7FFF));
            memory.SetRom(new MemoryRange(0, 0x2FFF));
            memory.BackingMemory[32] = 0xE821;  // JMP *+1,I
            memory.BackingMemory[33] = 0x1000;  // startup location...
            memory.BackingMemory[0x1000] = 0x7F;  // LDA KPA
            memory.BackingMemory[0x1001] = 0x300F;  // STA D
            memory.BackingMemory[0x1002] = 0x3009;  // STA PA
            memory.BackingMemory[0x1003] = 5;  // LDA R5

            memory.BackingMemory[0x2000] = 0xF020; // TCA
            memory.BackingMemory[0x2001] = 0xF020; // TCA

            return new CpuSimulator(memory, null);
        }
        [Test]
        public async Task Initialize()
        {
            var sim = Prepare();
            Assert.That(sim.State, Is.EqualTo(SimulatorState.Created));
        }

        [Test]
        public async Task Reset()
        {
            var sim = Prepare();
            sim.Reset();
            Assert.That(sim.State, Is.EqualTo(SimulatorState.Reset));
            Assert.That(sim.Ticks, Is.Zero);
            Assert.That(sim.PC, Is.EqualTo(32));
        }

        [Test]
        public async Task Step1()
        {
            var sim = Prepare();
            sim.Reset();
            sim.Tick();
            Assert.That(sim.State, Is.EqualTo(SimulatorState.Running));
            Assert.That(sim.Ticks, Is.Not.Zero);
            Assert.That(sim.PC, Is.EqualTo(0x1000));
        }

        static int Oct(int n)
        {
            return Convert.ToInt32(n.ToString(), 8);
        }

        [Test]
        public async Task MoveBytesOver()
        {
            // code from ROM @11520 seems to malfunction;
            var sim = Prepare();
            sim.Reset();
            sim.Register(CpuRegister.C, Oct(77072));
            sim.Register(CpuRegister.D, Oct(77072));
            sim.Memory.BackingMemory[Oct(77070)] = Oct(47522);
            sim.Memory.BackingMemory[Oct(77071)] = Oct(46104);
            sim.Memory.BackingMemory[Oct(77072)] = Oct(20442);
            sim.Memory.BackingMemory[Oct(11520)] = Oct(74761);
            sim.Memory.BackingMemory[Oct(11521)] = Oct(74751);
            sim.Memory.BackingMemory[Oct(11522)] = 0;
            sim.Memory.BackingMemory[Oct(11523)] = 0;
            sim.Memory.BackingMemory[Oct(11524)] = 0;
            sim.Memory.BackingMemory[Oct(11525)] = Oct(66520);

            sim.PC = Oct(11520);
            for(int i=0;i<4 * 6;i++)
                sim.Tick();
            for (int i = 0;i<3;i++)
                Console.WriteLine(Convert.ToString(sim.Memory.BackingMemory[i+ Oct(77070)], 8));
            Assert.That(sim.Memory.BackingMemory[Oct(77072)], Is.EqualTo(Oct(0)));
        }

        [Test]
        public async Task StepTCA()
        {
            var sim = Prepare();
            sim.Reset();
            sim.Register(CpuRegister.A, 1234);
            Assert.That(sim.Register(CpuRegister.A), Is.EqualTo(1234));
            sim.PC = 0x2000;
            sim.Tick();
            Assert.That(sim.Register(CpuRegister.A), Is.EqualTo((-1234 & 0xFFFF)));
            sim.Tick();
            Assert.That(sim.Register(CpuRegister.A), Is.EqualTo(1234));
        }

        [Test]
        public async Task Step4()
        {
            var sim = Prepare();
            sim.Reset();
            sim.Register(CpuRegister.D, 1234);
            sim.Register(CpuRegister.PA, 1234);
            Assert.That(sim.Register(CpuRegister.D), Is.Not.Zero);
            Assert.That(sim.Register(CpuRegister.PA), Is.Not.Zero);
            sim.Tick();
            sim.Tick();
            sim.Tick();
            sim.Tick();
            Assert.That(sim.State, Is.EqualTo(SimulatorState.Running));
            Assert.That(sim.Ticks, Is.Not.Zero);
            Assert.That(sim.PC, Is.EqualTo(0x1003));
            Assert.That(sim.Register(CpuRegister.D), Is.Zero);
            Assert.That(sim.Register(CpuRegister.PA), Is.Zero);
            Assert.That(sim.Disasssemble(), Is.EqualTo("      LDA R5"));
        }
    }
}