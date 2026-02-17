using System;
using System.Diagnostics;
using System.IO;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Transactions;
using CommandLineUtils;
using CommandLineUtils.Visuals;
using HP9825CPU;

namespace HP9825Simulator
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
                            Simulator?.Run(true, 10 * Simulator.ClockFrequency);    // 10 "second" timeout...
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
            reg.Register<DisplayOutput>("Display Emulator",
                x=>x.Color("Normal", ConsoleColor.Red, ConsoleColor.Black)  // red on dark red would be nice, but too close on Linux to use...
                    .Color("Run Indicator", ConsoleColor.Red));
            reg.Register<PrinterOutput>("Printer Output", 
                x=>x.Color("Normal", ConsoleColor.Black, ConsoleColor.White)
                    .Color("Tear mark", ConsoleColor.DarkRed));
            base.RegisterPalette(reg);
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
            // using (var fhigh=File.OpenRead("private/RAMChecker.asm.high.bin"))
            // {
            //     using(var flow=File.OpenRead("private/RAMChecker.asm.low.bin"))
            //     {
                    memory.BackingMemory.LoadDual8Bit(new BinaryReader(flow), new BinaryReader(fhigh), 0, 12288);
                }
            }
            var devices = new DeviceManager();
            var kdp = new KeyboardDisplayPrinterDevice();
            // TODO: KDP visual...

            kdp.PutKeyPress(HP9825Key.PrintAll, false); // request printout!
            //TestHellorld(kdp);
            TestCalc(kdp);
            //TestCalc2(kdp);
            //TestProgram(kdp);

            kdp.DisplayChanged += DebugDisplayChanged;
            
            devices.Add(0, kdp);

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

            //Simulator.SetBreakPoint(Convert.ToInt32("11467", 8));   // keyboard table branch!
            //Simulator.SetBreakPoint(Convert.ToInt32("6327", 8));   // stack parser...
            //Simulator.SetBreakPoint(Convert.ToInt32("12004", 8));   // keyboard code decode...


            //Simulator.SetBreakPoint(Convert.ToInt32("10031", 8));   // post RAM check...
            // Simulator.SetBreakPoint(Convert.ToInt32("510", 8));   // pre-"turn on display"...
            //Simulator.SetBreakPoint(Convert.ToInt32("11512", 8));   // "shift buffer to right for insert char..."
            //Simulator.SetBreakPoint(Convert.ToInt32("10070", 8));   // "process the key" call
            // Simulator.SetBreakPoint(Convert.ToInt32("11405", 8));   // pre-"turn on display"...
            // Simulator.SetBreakPoint(Convert.ToInt32("13430", 8));   // pre-"turn on display"...

            Simulator.SetMemoryBreakpoint(Convert.ToInt32("00563", 8)); // handler address
            //Simulator.SetMemoryBreakpoint(Convert.ToInt32("11613", 8)); // break on all access to ".WMOD" flags...
            
            // Simulator.SetMemoryBreakpoint(Convert.ToInt32("77533", 8)); // break on all access to address buffers...
            // Simulator.SetMemoryBreakpoint(Convert.ToInt32("77351", 8));
            // Simulator.SetMemoryBreakpoint(Convert.ToInt32("77352", 8));
            // Simulator.SetMemoryBreakpoint(Convert.ToInt32("77353", 8));

            await base.RunNow();

            // save state?!
        }

        private void TestProgram(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPresses("dsp\"starting...\"", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("spc 1;prt\"starting...\"", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("spc 1;prt\"running...\"", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("wait 500;gto 2", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("list", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute);

            kdp.PutKeyPress(HP9825Key.Run, false, TimeSpan.FromSeconds(15));
        }

        private void TestCalc2(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPress(HP9825Key.Number1, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Plus);
            kdp.PutKeyPress(HP9825Key.Number1);
            kdp.PutKeyPress(HP9825Key.Execute);
        }

        private void TestCalc(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPresses("fxd 5", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Execute);
            kdp.PutKeyPress(HP9825Key.Number5, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Asterisk);
            kdp.PutKeyPress(HP9825Key.Pi, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Execute);
        }

        private void TestHellorld(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPress(HP9825Key.D, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.S, false);
            kdp.PutKeyPress(HP9825Key.P, false);
            kdp.PutKeyPress(HP9825Key.Text2, true);
            kdp.PutKeyPress(HP9825Key.H, true);
            kdp.PutKeyPress(HP9825Key.E, true);
            kdp.PutKeyPress(HP9825Key.L, true);
            kdp.PutKeyPress(HP9825Key.L, true);
            kdp.PutKeyPress(HP9825Key.O, true);
            kdp.PutKeyPress(HP9825Key.R, true);
            kdp.PutKeyPress(HP9825Key.L, true);
            kdp.PutKeyPress(HP9825Key.D, true);
            kdp.PutKeyPress(HP9825Key.Text1, true);
            kdp.PutKeyPress(HP9825Key.Text2, true);
            kdp.PutKeyPress(HP9825Key.CharacterBack, false, TimeSpan.FromSeconds(4));
            kdp.PutKeyPress(HP9825Key.CharacterBack, false);
            kdp.PutKeyPress(HP9825Key.CharacterBack, false);
            kdp.PutKeyPress(HP9825Key.CharacterBack, false);
            kdp.PutKeyPress(HP9825Key.CharacterBack, false);
            kdp.PutKeyPress(HP9825Key.InsertReplace, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Space, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.W, true);
            kdp.PutKeyPress(HP9825Key.O, true);

            kdp.PutKeyPress(HP9825Key.Execute, false);
        }

        private void DebugDisplayChanged(object? sender, EventArgs e)
        {
            var dev = sender as KeyboardDisplayPrinterDevice;
            if (dev==null)
                return;
            //Debug.WriteLine("{2,18} Display Update   [{0}] [{1}]", dev.RunLight ? '*' : ' ', dev.Display, dev.System?.RunTime);
        }

        private CpuSimulator? Simulator;

        protected override Size MinSize => new Size(80,20);

        protected override Visual CreateRootVisual()
        {
            return new StatusDisplay(Simulator);
        }
    }
}