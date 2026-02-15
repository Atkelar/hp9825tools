using System;

namespace CommandLineUtils.Visuals
{
    public class KeyboardEventData
        : EventData
    {
        private ConsoleKeyInfo consoleKeyInfo;

        internal KeyboardEventData(ConsoleKeyInfo details)
            : base(EventType.KeyInput)
        {
            Key = details.Key;
            Char = details.KeyChar;
            Modifiers = details.Modifiers;
        }

        public KeyboardEventData(ConsoleKey key, ConsoleModifiers mod, char c)
            : base(EventType.KeyInput)
        {
            Modifiers=mod;
            Key = key;
            Char = c;
        }

        public ConsoleModifiers Modifiers { get; private set; }
        public ConsoleKey Key { get; private set; }
        public char Char { get; private set; }
    }
}