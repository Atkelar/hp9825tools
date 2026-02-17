using System;
using CommandLineUtils.Visuals;
using HP9825CPU;

namespace HP9825Simulator
{
    public class CodeInspector
        : Visual
    {
        public CodeInspector(CpuSimulator simulator)
        {
            _Simulator = simulator;
            Size = new Size(20, 1);
            _Value = "     ---";
        }

        public CpuSimulator _Simulator;
        private string _Value;

        protected override bool HandleEvent(EventData latestEvent)
        {
            if (base.HandleEvent(latestEvent))
                return true;
            switch (latestEvent)
            {
                case MessageEventData md when md.Code == StatusDisplay.UpdateDisplayMessage:
                    HandleUpdateValue();
                    break;
            }
            return false;
        }

        private void HandleUpdateValue()
        {
            var vNew = _Simulator.Disasssemble();
            if (_Value != vNew)
            {
                _Value = vNew;
                Invalidate();
            }
        }

        protected override void Paint(PaintContext p)
        {
            p.DrawString(Location.Origin, _Value, 0);
            p.Repeat(Location.Origin.Move(_Value.Length, 0), ' ', Size.Width - _Value.Length, 0);
        }
    }
}