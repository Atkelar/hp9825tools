using System;
using System.Reflection.Metadata;
using CommandLineUtils.Visuals;
using HP9825CPU;

namespace HP9825Simulator
{
    public class TapeStatus
        : Visual
    {
        private TapeDrive _HookTo;

        public TapeStatus(TapeDrive tape)
        {
            this._HookTo = tape;
            tape.ActivityChanged += HandleStatusUpdate;
            tape.CartridgeChanged += HandleNewCartridge;
            Size = new Size(36, 1);
            CartridgeName = tape.Cartridge?.Label;
        }

        private string? CartridgeName = null;
        private bool IsMoving = false;
        public const string SaveTapeCommand = "save-tape-now";

        protected override bool HandleEvent(EventData latestEvent)
        {
            if (latestEvent is MessageEventData md)
            {
                if (md.Code == SaveTapeCommand)
                {
                    string? file = md.Args as string;
                    if (file !=null && _HookTo?.Cartridge != null)
                    {
                        _HookTo.Cartridge.Save(file, false, true).Wait();
                    }
                    return true;
                }
            }
            return base.HandleEvent(latestEvent);
        }

        private void HandleStatusUpdate(object sender, EventArgs e)
        {
            if (object.ReferenceEquals(_HookTo, sender))
            {
                IsMoving = _HookTo.ActivityLight;
                Invalidate();
            }
        }

        private void HandleNewCartridge(object sender, EventArgs e)
        {
            if (object.ReferenceEquals(_HookTo, sender))
            {
                CartridgeName = _HookTo.Cartridge?.Label;
                Invalidate();
            }
        }

        protected override void Paint(PaintContext p)
        {
            p.DrawString(Location.Origin, IsMoving ? " ◉ " : " ○ ", 1);
            p.DrawString(new Location(3,0), (CartridgeName ?? "[empty]").PadRight(Size.Width - 3), CartridgeName != null ? 2 : 3);
        }
    }
}