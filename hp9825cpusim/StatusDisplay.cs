using System;
using CommandLineUtils.Visuals;
using HP9825CPU;

namespace HP9825CPUSimulator
{
    internal class StatusDisplay 
        : Container
    {
        private CpuSimulator _Simulator;

        public StatusDisplay(CpuSimulator simulator)
        {
            _Simulator = simulator;
            AddCpuDisplay(CpuRegister.A, 0);
            AddCpuDisplay(CpuRegister.B, 1);
            AddCpuDisplay(CpuRegister.C, 2);
            AddCpuDisplay(CpuRegister.D, 3);
            AddCpuDisplay(CpuRegister.IV, 4);
            AddCpuDisplay(CpuRegister.PA, 5);
            AddCpuDisplay(CpuRegister.W, 6);
            AddCpuDisplay(CpuRegister.P, 7);
            AddCpuDisplay(CpuRegister.R, 8);

            _MemViewer = new MemoryInspector(simulator.Memory, false);
            _MemViewer.DisplayOffset = _Simulator.PC;
            _MemViewer.Position = new Location(13,0);
            _MemViewer.Show();
            AddChild(_MemViewer);

            var mv = new MemoryInspector(simulator.Memory, false);
            mv.DisplayOffset = Convert.ToInt32("77533", 8);// 0x7e97;
            mv.Position = new Location(40,0);
            mv.Size = new Size(mv.Size.Width, 21);
            mv.Show();
            AddChild(mv);

            _CodeInspector = new CodeInspector(simulator);
            _CodeInspector.Position = new Location(13, 8);
            _CodeInspector.Show();
            AddChild(_CodeInspector);
            
            _Simulator.Ticked += CPU_Ticked;
            _Simulator.Resetted += CPU_Resetted;
            _Simulator.StateChanged += CPU_StateChanged;
        }

        private void CPU_StateChanged(object? sender, EventArgs e)
        {
            if (sender != _Simulator)   // shouldn't happen... leftover event registration?
                return;
            if (!IsRunning)
            {
                _MemViewer.DisplayOffset = _Simulator.PC;
                this.QueueMessage(UpdateDisplayMessage);
            }
        }

        private void CPU_Resetted(object? sender, EventArgs e)
        {
            if (sender != _Simulator)   // shouldn't happen... leftover event registration?
                return;
            if (!IsRunning)
            {
                _MemViewer.DisplayOffset = _Simulator.PC;
                this.QueueMessage(UpdateDisplayMessage);
            }
        }

        private void AddCpuDisplay(CpuRegister what, int y)
        {
            var disp = new RegisterContentDisplay(what, _Simulator);
            disp.Position = new Location(0, y);
            this.AddChild(disp, true);
            disp.Show();
        }

        public override void ParentSizeChanged(Size newSize)
        {
            // fill the parent...
            Size = newSize;
            base.ParentSizeChanged(newSize);
        }

        protected override bool HandleEvent(EventData latestEvent)
        {
            if (base.HandleEvent(latestEvent))
                return true;
            switch (latestEvent)
            {
                case MessageEventData md when md.Code == UpdateDisplayMessage:

                    break;

            }
            return false;
        }

        public const string UpdateDisplayMessage = "upddsp";
        private readonly MemoryInspector _MemViewer;
        private bool IsRunning => _Simulator?.IsFreeRunning ?? false; // true when we asked the simulator to free-run...
        private CodeInspector _CodeInspector;

        private void CPU_Ticked(object? sender, TickedEventArgs e)
        {
            if (sender != _Simulator)   // shouldn't happen... leftover event registration?
                return;
            if (!IsRunning)
            {
                _MemViewer.DisplayOffset = _Simulator.PC;
                this.QueueMessage(UpdateDisplayMessage);
            }
        }
    }
}