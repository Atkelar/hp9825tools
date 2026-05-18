using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection.Metadata;
using System.Threading.Tasks;
using System.Transactions;
using CommandLineUtils;
using CommandLineUtils.Visuals;
using HP9825CPU;

namespace HP9825Simulator
{
    [Process("CPUSim", HelpMessage = "Simulates the pure CPU of an HP9825 machine. CPU, RAM and ROM as well as any added IO Devices.")]
    public class CpuSimulatorProcess
        : VisualProcessBase
    {
        public CpuSimulatorProcess()
        {
        }

        public const string ResetCommand = "reset";
        public const string StepCommand = "step";
        public const string RunCommand = "run";
        public const string ExportDiagLogCommand = "log-html";
        public const string ExportPrinterCommand = "prt-html";
        public const string SaveCurrentTapeCommand = "save-tape";

        public const string RunningState = "running";

        protected override void BuildApplicationStates(IApplicationStateBuilder app)
        {
            base.BuildApplicationStates(app);
            app.AddState(RunningState, x=>x.HasCommands());
        }

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
                        case ExportDiagLogCommand:
                            Simulator?.SaveDiagnosticLog($"private/diag-{DateTime.Now:MMdd-HHmmss}.html", LogExportFormat.Html, "Hard coded test...", false).Wait();
                            return true;
                        case RunCommand:
                            Simulator?.Run(true, 10 * Simulator.ClockFrequency);    // 10 "second" timeout...
                            return true;
                        case ExportPrinterCommand:
                            this.QueueCommand(PrinterOutput.ExportToHtmlCommand, "private/test-prt.html");
                            return true;
                        case SaveCurrentTapeCommand:
                            this.QueueCommand(TapeStatus.SaveTapeCommand, "private/test2.tape");
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
            hotkeyManager.AddMessage(ExportPrinterCommand, ConsoleKey.P, ConsoleModifiers.Alt);
            hotkeyManager.AddMessage(SaveCurrentTapeCommand, ConsoleKey.S, ConsoleModifiers.Control);
            hotkeyManager.AddMessage(ExportDiagLogCommand, ConsoleKey.L, ConsoleModifiers.Control);
            
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
            reg.Register<TapeStatus>("Tape Status",
                x=>x.Color("Normal", ConsoleColor.Black, ConsoleColor.DarkGray)
                    .Color("Indicator", ConsoleColor.Yellow)
                    .Color("Label", ConsoleColor.White)
                    .Color("Empty", ConsoleColor.DarkGray));
            base.RegisterPalette(reg);
        }

        protected override async Task RunNow()
        {
            MemoryManager memory = new MemoryManager();
            memory.SetRamConfiguration(RamConfiguration.Ram16k);

            // TODO: this is just test code...
            using (var fhigh=File.OpenRead("private/hp9825a-system-high.bin"))
            {
                using(var flow=File.OpenRead("private/hp9825a-system-low.bin"))
                {
            // using (var fhigh=File.OpenRead("private/RAMChecker-2.high.bin"))
            // {
            //     using(var flow=File.OpenRead("private/RAMChecker-2.low.bin"))
            //     {
                    memory.LoadSystemRomImage(new BinaryReader(flow), new BinaryReader(fhigh));
                }
            }
            using (var f=File.OpenRead("private/STRING_T.BIN")) // plug in strings ROM...
            {
                memory.LoadOptionPack(OptionRom.Strings, new BinaryReader(f));
            }
            using (var f=File.OpenRead("private/ADVPGM_T.BIN")) // plug in advanced programming ROM...
            {
                memory.LoadOptionPack(OptionRom.AdvancedProgramming, new BinaryReader(f));
            }
            using (var f=File.OpenRead("private/GENIO_T.BIN")) // plug in general IO ROM...
            {
                memory.LoadOptionPack(OptionRom.GeneralIO, new BinaryReader(f));
            }
            // using (var f=File.OpenRead("private/EXTIO_T.BIN")) // plug in general IO ROM...
            // {
            //     memory.LoadOptionPack(OptionRom.ExtendedIO, new BinaryReader(f));
            // }

            // using (var f=File.OpenRead("private/GENIO_T.BIN")) // plug in general IO ROM...
            // {
            //     memory.LoadOptionPack(OptionRom.GeneralIO, new BinaryReader(f));
            // }
            
            var devices = new DeviceManager();
            var kdp = new KeyboardDisplayPrinterDevice();

            //kdp.PutKeyPress(HP9825Key.PrintAll, false); // request printout!

            var rtc = new RTCDevice();
            devices.Add(rtc);
            
            //TestHellorld(kdp);
            //TestCalc(kdp);
            //TestCalc2(kdp);
            //TestCalcVars(kdp);
            //TestProgram(kdp);
            //TestCat(kdp);
            //TestFunctionKeys(kdp);
            //TestStrings(kdp);
            //TestMandelbrot(kdp); // needs strings and adv. prog.
            //TestRTCSetClock(kdp);
            //TestRTCGetClock(kdp);
            //TestRTCEvent(kdp);

            var tape = new TapeDrive();

            foreach(var item in tape.Tracepoints)
            {
                item.IsEnabled = false;
            }
            tape.Tracepoints.First(x=>x.Name=="GAPNOW").IsEnabled=true;
            tape.Tracepoints.First(x=>x.Name=="GAPNOT").IsEnabled=true;
            tape.Tracepoints.First(x=>x.Name=="CMDCHG").IsEnabled=true;

            // tape.InsertCartridge(TapeCartridge.Create("Testing2"));
            // TestTapeDrive1(kdp);
            tape.InsertCartridge(await TapeCartridge.Load("private/test2.tape"));
            //TestTapeDriveList(kdp);
            //TestTapeDrive2(kdp);
            //TestTapeDrive3(kdp);
            //TestTapeDrive4(kdp);
            //TestTapeDrive5(kdp);
            //TestTapeDrive6(kdp);
            TestTapeDrive7(kdp);

            // TestMandelbrot(kdp, false);
            // kdp.PutKeyPresses("list", TimeSpan.FromSeconds(1));
            // kdp.PutKeyPress(HP9825Key.Execute);


            devices.Add(kdp);

            devices.Add(tape);


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

            // Simulator.Memory.AddFault(Convert.ToInt32("76000", 8), Convert.ToInt32("77777", 8), 
            //     0b1000_0000_0100_0000, MemoryFaultMode.Toggle);
            // Simulator.Memory.AddFault(Convert.ToInt32("56000", 8), Convert.ToInt32("56123", 8), 
            //     0b0000_0001_1000_0000, MemoryFaultMode.StuckOff);

            Simulator.DebugBinaryCode = true;
            // TAPE drive diagnostic stuff in system ROM
            //Simulator.SetTracepoint(Convert.ToInt32("20001", 8), "Start Read Header [*-1] [*-2]");
            Simulator.SetTracepoint(Convert.ToInt32("20243", 8), "Read Word");
            Simulator.SetTracepoint(Convert.ToInt32("20247", 8), "Got Error (Gap)");
            Simulator.SetTracepoint(Convert.ToInt32("20260", 8), "Got Word: [B]");
            Simulator.SetTracepoint(Convert.ToInt32("20031", 8), "Expected Checksum of Record Header: [B], got [#77721b]"); // checksum for read header; B vs. T9

            Simulator.SetTracepoint(Convert.ToInt32("20301", 8), "Start Write Header");
            Simulator.SetTracepoint(Convert.ToInt32("20334", 8), "Checksum for header: [B]");
            Simulator.SetTracepoint(Convert.ToInt32("20421", 8), "Write Word [B]");
            Simulator.SetTracepoint(Convert.ToInt32("20525", 8), "WAIT -[A] tac pulses");
            Simulator.SetTracepoint(Convert.ToInt32("20535", 8), "WAIT DONE");

            Simulator.SetTracepoint(Convert.ToInt32("20431", 8), "WGAP started, [B] pulses");
            Simulator.SetTracepoint(Convert.ToInt32("20437", 8), "WGAP done");

            Simulator.SetTracepoint(Convert.ToInt32("20103", 8), "Expected Checksum of partition header: [B], got [#77721b]"); // checksum for read header; B vs. T9
            Simulator.SetTracepoint(Convert.ToInt32("20170", 8), "Expected Checksum of partition body: [B], got [#77721b]"); // checksum for read header; B vs. T9

            // Simulator.SetTracepoint(Convert.ToInt32("10070", 8), "Process     [*-1]");   // main loop key distributor call...
            // Simulator.SetTracepoint(Convert.ToInt32("10071", 8), "Display Key [*-1]");   // main loop key distributor call...
            Simulator.SetTracepoint(Convert.ToInt32("17042", 8), "LDP1 - from [*-1] [*-2]");

            // Simulator.SetBreakPoint(Convert.ToInt32("17042", 8));      // LDP1
            // Simulator.SetBreakPoint(Convert.ToInt32("17206", 8));      // LDP
            // Simulator.SetBreakPoint(Convert.ToInt32("17270", 8));      // PRGLD
            // Simulator.SetBreakPoint(Convert.ToInt32("17310", 8));      // PRGLD; PRG13
            // Simulator.SetBreakPoint(Convert.ToInt32("17642", 8));      // RDREC->RDBDY
            // Simulator.SetBreakPoint(Convert.ToInt32("20036", 8));      // RDBDY...
            //Simulator.SetBreakPoint(Convert.ToInt32("20156", 8));      // EXE A (verify or store!)

            
            // Simulator.SetBreakPoint(Convert.ToInt32("20001", 8));      // RDHED
            // Simulator.SetBreakPoint(Convert.ToInt32("20004", 8));      // call to CMDW
            // Simulator.SetBreakPoint(Convert.ToInt32("20007", 8));      // call to INGAP
            // Simulator.SetBreakPoint(Convert.ToInt32("20010", 8));      // call to INDTA
            // Simulator.SetBreakPoint(Convert.ToInt32("20011", 8));      // call to RSTHD
            // Simulator.SetBreakPoint(Convert.ToInt32("20012", 8));      // call to PAMBL
            //Simulator.SetBreakPoint(Convert.ToInt32("20234", 8));      // PAMBL - read to first "1" bit to sync with tape...
            

            //Simulator.SetBreakPoint(Convert.ToInt32("20001", 8));   // RDHED - Read header...
//            Simulator.SetBreakPoint(Convert.ToInt32("21032", 8));   // MRK - dead zone marker - should be 213 words of all ones...
            //Simulator.SetBreakPoint(Convert.ToInt32("20421", 8));   // PUTWD - write word to tape...

            //Simulator.SetBreakPoint(Convert.ToInt32("21143", 8));   // Rewind, blank track?
            //Simulator.SetBreakPoint(Convert.ToInt32("21150", 8));   // Rewind, blank track?

            //Simulator.SetBreakPoint(Convert.ToInt32("20605", 8));   // Tape, HOLE method.

            //Simulator.SetBreakPoint(Convert.ToInt32("11467", 8));   // keyboard table branch!
            //Simulator.SetBreakPoint(Convert.ToInt32("6327", 8));   // stack parser...
            //Simulator.SetBreakPoint(Convert.ToInt32("12004", 8));   // keyboard code decode...


            //Simulator.SetBreakPoint(Convert.ToInt32("10031", 8));   // post RAM check...
            // Simulator.SetBreakPoint(Convert.ToInt32("510", 8));   // pre-"turn on display"...
            //Simulator.SetBreakPoint(Convert.ToInt32("11512", 8));   // "shift buffer to right for insert char..."
            //Simulator.SetBreakPoint(Convert.ToInt32("10070", 8));   // "process the key" call
            // Simulator.SetBreakPoint(Convert.ToInt32("11405", 8));   // pre-"turn on display"...
            // Simulator.SetBreakPoint(Convert.ToInt32("13430", 8));   // pre-"turn on display"...
            // Simulator.SetBreakPoint(Convert.ToInt32("14265", 8));   // Start of FDV code...
            //Simulator.SetBreakPoint(Convert.ToInt32("4703", 8));   // real number output?...
            //Simulator.SetBreakPoint(Convert.ToInt32("15073", 8));   // Error (LDB ...-> EXE B)
            //Simulator.SetBreakPoint(Convert.ToInt32("06564", 8));   // Call AADD,I  -> "+" operation in interpreter.
            //Simulator.SetBreakPoint(Convert.ToInt32("15041", 8));   // exponent update post operation...

            //Simulator.SetMemoryBreakpoint(Convert.ToInt32("00563", 8)); // handler address
            //Simulator.SetMemoryBreakpoint(Convert.ToInt32("11613", 8)); // break on all access to ".WMOD" flags...
            
            // Simulator.SetMemoryBreakpoint(Convert.ToInt32("77533", 8)); // break on all access to address buffers...
            // Simulator.SetMemoryBreakpoint(Convert.ToInt32("77351", 8));
            // Simulator.SetMemoryBreakpoint(Convert.ToInt32("77352", 8));
            // Simulator.SetMemoryBreakpoint(Convert.ToInt32("77353", 8));

            await base.RunNow();

            // save state?!
        }

        private void TestTapeDriveList(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPress(HP9825Key.Rewind, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPresses("tlist", TimeSpan.FromSeconds(5));
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(1));
        }

        private void TestTapeDrive5(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPress(HP9825Key.Rewind, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPresses("dim A[4,5]", TimeSpan.FromSeconds(5));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("dim S$[10]");
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("\"Hello?\"→S$");
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("0→X", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPress(HP9825Key.Power, true);
            kdp.PutKeyPresses("2→X", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPress(HP9825Key.Pi, false);
            kdp.PutKeyPresses("→I", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("3→A[1,1]", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("4→A[4,1]", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("5→A[1,2]", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("6→A[4,5]", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("rcf 2,A[*],S$,X,I", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
        }

        private void TestTapeDrive7(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPresses("trk 1", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Rewind, false, TimeSpan.FromSeconds(1));
            kdp.PutKeyPresses("fdf 20", TimeSpan.FromSeconds(5));
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(1));
            kdp.PutKeyPresses("mrk 1,32000", TimeSpan.FromSeconds(10));
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(1));
            kdp.PutKeyPresses("rcm 20", TimeSpan.FromSeconds(20));
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(1));
        }

        private void TestTapeDrive6(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPress(HP9825Key.Rewind, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Fetch, false, TimeSpan.FromSeconds(3));
            kdp.PutKeyPress(HP9825Key.Function0, false);
            kdp.PutKeyPresses("prt");
            kdp.PutKeyPress(HP9825Key.Store, false);
            kdp.PutKeyPress(HP9825Key.Fetch, false, TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Function1, false);
            kdp.PutKeyPresses("*prt \"π\",π");
            kdp.PutKeyPress(HP9825Key.Store, false);
            kdp.PutKeyPress(HP9825Key.Fetch, false, TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Function2, false);
            kdp.PutKeyPresses("/2.71828182846");
            kdp.PutKeyPress(HP9825Key.Store, false);
            kdp.PutKeyPress(HP9825Key.Fetch, false, TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Function3, false);
            kdp.PutKeyPresses("*→R;dspR,\"in.=\",2.54R,\"cm.\"");
            kdp.PutKeyPress(HP9825Key.Store, false);
            kdp.PutKeyPresses("list k");
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(5));
            kdp.PutKeyPresses("trk 1", TimeSpan.FromSeconds(5));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPress(HP9825Key.Rewind, false);
            kdp.PutKeyPresses("mrk 20,1024", TimeSpan.FromSeconds(5));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("rck 1", TimeSpan.FromSeconds(20));
            kdp.PutKeyPress(HP9825Key.Execute, false);
        }

        private void TestTapeDrive4(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPress(HP9825Key.Rewind, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPresses("dim A[4,5]", TimeSpan.FromSeconds(3));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("0→X", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPress(HP9825Key.Power, true);
            kdp.PutKeyPresses("2→X", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPress(HP9825Key.Pi, false);
            kdp.PutKeyPresses("→I", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("3→A[1,1]", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("4→A[4,1]", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("5→A[1,2]", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("6→A[4,5]", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
            kdp.PutKeyPresses("rcf 1,A[*],X,I", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false);
        }

        private void TestTapeDrive3(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPress(HP9825Key.Rewind, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPresses("ldp 0", TimeSpan.FromSeconds(3));
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Clear, false, TimeSpan.FromSeconds(10));
            kdp.PutKeyPresses("list", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(1));

            //kdp.PutKeyPress(HP9825Key.Run, false, TimeSpan.FromSeconds(10));
        }

        private void TestTapeDrive2(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPress(HP9825Key.Rewind, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPresses(" ", TimeSpan.FromSeconds(5));
            TestMandelbrot(kdp, false);
            kdp.PutKeyPresses("rcf 0", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(1));
        }

        private void TestTapeDrive1(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPress(HP9825Key.Rewind, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPresses("mrk 3,1024", TimeSpan.FromSeconds(10));
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPresses("rew", TimeSpan.FromSeconds(20));
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPresses("tlist", TimeSpan.FromSeconds(10));
            kdp.PutKeyPress(HP9825Key.Execute, false, TimeSpan.FromSeconds(2));
        }

        private void TestRTCSetClock(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPresses("wrt 9,\"S06 18 12 01 00\"", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Execute);
        }

        private void TestRTCGetClock(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPresses("dim T$[30];wrt 9,\"R\"; red 9,T$; prt T$", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Execute);
            kdp.PutKeyPresses("wrt 9,\"R\"; red 9,A,B,C,D,E; prt A,B,C,D,E", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Execute);
        }

        private void TestRTCEvent(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPresses("wrt 9,\"U1=O2\";wrt 9,\"U1P500\"", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Execute);
            kdp.PutKeyPresses("wrt 9,\"U1G\"", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Execute);
        }

        private void TestMandelbrot(KeyboardDisplayPrinterDevice kdp, bool run = true)
        {
            PutProgramLine(kdp, "dim L$[16]");
            PutProgramLine(kdp, "prt \"starting...\"");
            PutProgramLine(kdp, "spc 2");
            PutProgramLine(kdp, "for Y = -7 to 7");
            PutProgramLine(kdp, "for X = -7 to 8");
            PutProgramLine(kdp, "X*.1618 → D");
            PutProgramLine(kdp, "Y*.1429 → E");
            PutProgramLine(kdp, "D → A");
            PutProgramLine(kdp, "E → B");
            PutProgramLine(kdp, "for I=0 to 15");
            PutProgramLine(kdp, "AA-BB+D → T");
            PutProgramLine(kdp, "2AB+E → B");
            PutProgramLine(kdp, "T → A");
            PutProgramLine(kdp, "if (AA+BB) <= 4; gto \"cont\"");
            PutProgramLine(kdp, "if I>9; I+7 → I");
            PutProgramLine(kdp, "L$ & char(48+I) → L$");
            PutProgramLine(kdp, "20 → I");
            PutProgramLine(kdp, "\"cont\": next I");
            PutProgramLine(kdp, "if I<17; L$ & \" \" → L$");
            PutProgramLine(kdp, "next X");
            PutProgramLine(kdp, "prt L$");
            PutProgramLine(kdp, "\"\" → L$");
            PutProgramLine(kdp, "next Y");

            PutProgramLine(kdp, "spc 2");
            PutProgramLine(kdp, "prt \"Done!\"");

            if (run) kdp.PutKeyPress(HP9825Key.Run);
        }

        private void PutProgramLine(KeyboardDisplayPrinterDevice kdp, string v)
        {
            kdp.PutKeyPresses(v);
            kdp.PutKeyPress(HP9825Key.Store);
        }

        private void TestStrings(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPresses("dim A$[20];\"hello\"→A$;dsp cap(A$)", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Execute);
        }

        private void TestFunctionKeys(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPress(HP9825Key.Fetch, false, TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Function0);
            kdp.PutKeyPresses("*→R; dspR,\"in.=\",2.54R,\"cm.\"");
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("12", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Function0);
        }

        private void TestCat(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPresses("spc 2", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPress(HP9825Key.Run, false, TimeSpan.FromSeconds(15));
            kdp.PutKeyPresses("dsp\"starting...\"", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Store);

            kdp.PutKeyPresses("prt\"   '\"", TimeSpan.FromSeconds(0.7));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("prt\" .'|_\"", TimeSpan.FromSeconds(0.7));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("prt\"/ o  '-__,\"", TimeSpan.FromSeconds(0.7));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("prt\";  - o  .'\"", TimeSpan.FromSeconds(0.7));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("prt\"|    -._(;\"", TimeSpan.FromSeconds(0.7));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("prt\"| | | / )/\"", TimeSpan.FromSeconds(0.7));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("prt\"'-'-''--'\"", TimeSpan.FromSeconds(0.7));
            kdp.PutKeyPress(HP9825Key.Store);
            kdp.PutKeyPresses("spc 2", TimeSpan.FromSeconds(1));
            kdp.PutKeyPress(HP9825Key.Store);
            // kdp.PutKeyPresses("list", TimeSpan.FromSeconds(1));
            // kdp.PutKeyPress(HP9825Key.Execute);

            kdp.PutKeyPress(HP9825Key.Run, false, TimeSpan.FromSeconds(1));
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
        }

        private void TestCalcVars(KeyboardDisplayPrinterDevice kdp)
        {
            kdp.PutKeyPresses("fxd 8", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Execute);
            // kdp.PutKeyPresses("123.345" , TimeSpan.FromSeconds(2));
            // kdp.PutKeyPress(HP9825Key.Execute);
            // kdp.PutKeyPresses("123.345e1" , TimeSpan.FromSeconds(2));
            // kdp.PutKeyPress(HP9825Key.Execute);
            // kdp.PutKeyPresses("123.345e-1" , TimeSpan.FromSeconds(2));
            // kdp.PutKeyPress(HP9825Key.Execute);
            // kdp.PutKeyPresses("-123.345" , TimeSpan.FromSeconds(2));
            // kdp.PutKeyPress(HP9825Key.Execute);
            // kdp.PutKeyPresses("-123.345e1" , TimeSpan.FromSeconds(2));
            // kdp.PutKeyPress(HP9825Key.Execute);
            // kdp.PutKeyPresses("0.1234+0.9" , TimeSpan.FromSeconds(2));
            //kdp.PutKeyPresses("-12.345e-2" , TimeSpan.FromSeconds(2));
            kdp.PutKeyPresses("-12.345e-2→B; 34e-1→C" , TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Execute);
            //kdp.PutKeyPresses("-1.234e2→B" , TimeSpan.FromSeconds(2));
            //kdp.PutKeyPress(HP9825Key.Execute);
            //kdp.PutKeyPress(HP9825Key.A, true, TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Power, true, TimeSpan.FromSeconds(2)); // root.
            //kdp.PutKeyPresses("(BB+CC)");
            kdp.PutKeyPresses("(CC+BB)", TimeSpan.FromSeconds(2));
            kdp.PutKeyPress(HP9825Key.Execute);
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
            kdp.PutKeyPress(HP9825Key.Slash);
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

        private CpuSimulator? Simulator;

        protected override Size MinSize => new Size(80,20);

        protected override Visual CreateRootVisual()
        {
            return new StatusDisplay(Simulator);
        }
    }
}