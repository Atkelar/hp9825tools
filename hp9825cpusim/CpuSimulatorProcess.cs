using System;
using System.IO;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using CommandLineUtils;
using CommandLineUtils.Visuals;
using HP9825CPU;

namespace HP9825CPUSimulator
{
    [Process("CPUSim", HelpMessage = "Simulates the pure CPU of an HP9825 machine. No IO Devices, just CPU, RAM and ROM.")]
    public class CpuSimulatorProcess
        : VisualProcessBase
    {
        public CpuSimulatorProcess()
        {
        }

        public const string ResetCommand = "reset";
        public const string StepCommand = "step";
        public const string RunCommand = "run";

        protected override bool HandleEvent(EventData evt)
        {
            if(base.HandleEvent(evt))
                return true;
            switch(evt)
            {
                case MessageEventData md:
                    switch(md.Code)
                    {
                        case ResetCommand:
                            Simulator?.Reset();
                            return true;
                        case StepCommand:
                            Simulator?.Tick();
                            return true;
                        case RunCommand:
                            Simulator?.Run();
                            return true;
                    }
                    break;
            }
            return false;
        }

        protected override void RegisterHotKeys(HotkeyManager hotkeyManager)
        {
            base.RegisterStandardHotkeys(hotkeyManager, true);
            hotkeyManager.AddMessage(ResetCommand, ConsoleKey.R, ConsoleModifiers.Control);
            hotkeyManager.AddMessage(StepCommand, ConsoleKey.Spacebar);
            hotkeyManager.AddMessage(StepCommand, ConsoleKey.F11);
            hotkeyManager.AddMessage(RunCommand, ConsoleKey.F5);
            base.RegisterHotKeys(hotkeyManager);
        }

        protected override void RegisterPalette(PaletteHandler reg)
        {
            reg.Register<RegisterContentDisplay>("Register display", 
                x=> x.Color("Label", System.ConsoleColor.Blue,  System.ConsoleColor.DarkBlue)
                    .Color("Normal", System.ConsoleColor.Green)
                    .Color("Changed", System.ConsoleColor.Red));
            reg.Register<StatusDisplay>("Status display", 
                x=>x.Color("Background", System.ConsoleColor.Black, System.ConsoleColor.DarkGray));
            reg.Register<MemoryInspector>("Memory inspector", 
                x=>x.Color("Address", System.ConsoleColor.Black, System.ConsoleColor.DarkCyan)
                    .Color("Indicators", System.ConsoleColor.Cyan)
                    .Color("ROM", System.ConsoleColor.Yellow)
                    .Color("RAM", System.ConsoleColor.Blue)
                    .Color("Missing", System.ConsoleColor.DarkGray)
                    .Color("Changed", System.ConsoleColor.Red));
            reg.Register<CodeInspector>("Code inspector",
                x=>x.Color("Normal", System.ConsoleColor.White, System.ConsoleColor.DarkBlue));
            base.RegisterPalette(reg);
        }

        private class DummyKDP
            : DeviceBase
        {
            private bool _HotReset;

            public DummyKDP()
                : base("KDP", null)
            {
                _HotReset = false;
            }

            protected override int ReadIORegister(int regIndex)
            {
                sbOut.AppendFormat("R-{0}", regIndex);
                sbOut.AppendLine();
                switch(regIndex)
                {
                    case 1:     // system status...
                        int flag = 0;
                        if (!_HotReset)
                            flag |= 8;
                        return flag;
                }
                return 0;
            }

            protected override void Tick()
            {
                
            }

            System.Text.StringBuilder sbOut = new System.Text.StringBuilder();
            protected override void WriteIORegister(int regIndex, int value)
            {
                sbOut.AppendFormat("W-{0}: {1}", regIndex, value);
                sbOut.AppendLine();
            }
        }

        protected override async Task RunNow()
        {
            MemoryManager memory = new MemoryManager();
            memory.SetRam(new MemoryRange(0x7000, 0x7FFF));
            memory.SetRom(new MemoryRange(0, 12288));

            // TODO: this is just test code...
            using (var fhigh=File.OpenRead("private/hp9825a-system-high.bin"))
            {
                using(var flow=File.OpenRead("private/hp9825a-system-low.bin"))
                {
                    memory.BackingMemory.LoadDual8Bit(new BinaryReader(flow), new BinaryReader(fhigh), 0, 12288);
                }
            }
            var devices = new DeviceManager();
            devices.Add(0, new DummyKDP());

            // memory.BackingMemory[32] = 0xE821;  // JMP *+1,I
            // memory.BackingMemory[33] = 0x1000;  // startup location...
            // memory.BackingMemory[0x1000] = 0x7F;  // LDA KPA
            // memory.BackingMemory[0x1001] = 0x300F;  // STA D
            // memory.BackingMemory[0x1002] = 0x3009;  // STA PA
            // memory.BackingMemory[0x1003] = 5;  // LDA R5

            // memory.BackingMemory[0x2000] = 0xF020; // TCA
            // memory.BackingMemory[0x2001] = 0xF020; // TCA
            // build the simulator...
            Simulator = new CpuSimulator(memory, devices);

            //Simulator.SetBreakPoint(Convert.ToInt32("10031", 8));   // post RAM check...
            Simulator.SetBreakPoint(Convert.ToInt32("10060", 8));   // pre-"turn on display"...
            Simulator.SetBreakPoint(Convert.ToInt32("11405", 8));   // pre-"turn on display"...
            Simulator.SetBreakPoint(Convert.ToInt32("13430", 8));   // pre-"turn on display"...


            Simulator.SetMemoryBreakpoint(Convert.ToInt32("77533", 8)); // break on all access to address buffers...
            // Simulator.SetMemoryBreakpoint(Convert.ToInt32("77351", 8));
            // Simulator.SetMemoryBreakpoint(Convert.ToInt32("77352", 8));
            // Simulator.SetMemoryBreakpoint(Convert.ToInt32("77353", 8));

            await base.RunNow();

            // save state?!
        }

        private CpuSimulator? Simulator;

        protected override Size MinSize => new Size(80,20);

        protected override Visual CreateRootVisual()
        {
            return new StatusDisplay(Simulator);
        }
    }
}