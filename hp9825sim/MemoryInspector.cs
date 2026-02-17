using System;
using CommandLineUtils.Visuals;
using HP9825CPU;

namespace HP9825Simulator
{
    /// <summary>
    /// Visualize a section of memory, color coded by RAM/ROM/MISSING; tracking an address, and changes to the memory if requested.
    /// </summary>
    public class MemoryInspector
        : Visual
    {
        private readonly bool _HighlightChanges;
        private readonly MemoryManager _MemoryManager;
        private int _DisplayOffset;

        public MemoryInspector(MemoryManager source, bool highlightChanges)
            : base()
        {
            _HighlightChanges = highlightChanges;
            _MemoryManager = source;
            _DisplayOffset = 0;
            Size = new Size(15, 5);
        }

        public int DisplayOffset 
        {
            get => _DisplayOffset;
            set
            {
                if (value != _DisplayOffset)
                {
                    _DisplayOffset = value;
                    Invalidate();
                }
            }
        }

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
            Invalidate();
            bool needsUpdate = false;
            // TOOD: handle change detection... offset change is handled by property code...
        }

        protected override void Paint(PaintContext p)
        {
            int memAddress = DisplayOffset - Size.Height / 2;
            int yOffs = 0;
            while (yOffs < Size.Height)
            {
                if (memAddress < 32 || memAddress > _MemoryManager.BackingMemory.Length)
                {
                    // draw empty...
                    p.Repeat(new Location(0,yOffs), ' ', 15, 0);
                }
                else
                {
                    var type = _MemoryManager.GetTypeFor(memAddress);
                    var value = _MemoryManager[memAddress];
                    var strValue = Convert.ToString(value, 8).PadLeft(6);
                    var strAddress = Convert.ToString(memAddress, 8).PadLeft(6);
                    Location pos = new Location(0, yOffs);
                    p.DrawChar(pos, memAddress == DisplayOffset ? '▶' : ' ', 1);
                    pos = pos.Move(1, 0);
                    p.DrawString(pos, strAddress, 0);
                    pos=pos.Move(6,0);
                    p.DrawChar(pos, ' ',0);
                    pos=pos.Move(1,0);
                    p.DrawString(pos, strValue, type ==  MemoryType.Missing ? 4 : ( type == MemoryType.Ram ? 3 : 2 ));
                    pos = pos.Move(6, 0);
                    p.DrawChar(pos, memAddress == DisplayOffset ? '◀' : ' ', 1);
                }
                yOffs++;
                memAddress++;
            }
        }
    }
}