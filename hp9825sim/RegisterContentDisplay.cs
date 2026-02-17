using System;
using CommandLineUtils.Visuals;
using HP9825CPU;

namespace HP9825Simulator
{
    internal class RegisterContentDisplay
        : Visual
    {
        private CpuRegister _Register;
        private CpuSimulator _Simulator;
        private string _Label;

        public RegisterContentDisplay(CpuRegister which, CpuSimulator from)
        {
            _Simulator = from;
            _Register = which;
            Size = new Size(11,1);
            currentValue = oldValue = "------";
            _Label = which.ToString().PadRight(5);
        }

        private void HandleUpdateValue()
        {
            var value = _Simulator.Register(_Register);
            // TODO: adhere to app setting for base!
            var strValue = Convert.ToString(value, 8).PadLeft(6);
            bool needsUpdate = (oldValue != currentValue) || (currentValue != strValue);
            oldValue = currentValue;
            currentValue = strValue;
            if (needsUpdate)
                this.Invalidate();
        }

        private string currentValue, oldValue;
       
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

        protected override void Paint(PaintContext p)
        {
            int colorIndex = currentValue != oldValue ? 2 : 1;
            p.DrawString(Location.Origin, _Label, 0);
            p.DrawString(new Location(5,0), currentValue, colorIndex);
        }
    }
}