using System;
using System.Collections.Generic;

namespace CommandLineUtils.Visuals
{
    public class HotkeyManager
    {
        private struct HotkeyRegistration
        {
            public EventType Type; // either command or message...
            public string Code; 
            public object? ArgFixed;
            public Func<object?>? ArgGen;
            public Func<object?, object?>? ArgGen2;
        }

        Dictionary<ConsoleModifiers, Dictionary<ConsoleKey, HotkeyRegistration>> _Keys = new Dictionary<ConsoleModifiers, Dictionary<ConsoleKey, HotkeyRegistration>>();

        private void Add(ConsoleKey key, ConsoleModifiers mods, HotkeyRegistration registration)
        {
            if (!_Keys.TryGetValue(mods, out var reg))
            {
                _Keys.Add(mods, reg = new Dictionary<ConsoleKey, HotkeyRegistration>());
            }
            if(reg.ContainsKey(key))
                throw new ApplicationStateException(string.Format("Duplicate hotkey registration for (Mod.: {0}) {1}", mods, key));
            reg.Add(key, registration);
        }

        public HotkeyManager AddMessage(string code, Func<object?, object?> argGen, object? arg, ConsoleKey key, ConsoleModifiers mods = ConsoleModifiers.None)
        {
            Add(key, mods, new HotkeyRegistration() { Type = EventType.Message, Code = code, ArgGen2 = argGen, ArgFixed = arg });
            return this;
        }

        public HotkeyManager AddMessage(string code, Func<object?> argGen, ConsoleKey key, ConsoleModifiers mods = ConsoleModifiers.None)
        {
            Add(key, mods, new HotkeyRegistration() { Type = EventType.Message, Code = code, ArgGen = argGen });
            return this;
        }

        public HotkeyManager AddMessage(string code, object? arg, ConsoleKey key, ConsoleModifiers mods = ConsoleModifiers.None)
        {
            Add(key, mods, new HotkeyRegistration() { Type = EventType.Message, Code = code, ArgFixed = arg });
            return this;
        }

        public HotkeyManager AddMessage(string code, ConsoleKey key, ConsoleModifiers mods = ConsoleModifiers.None)
        {
            Add(key, mods, new HotkeyRegistration() { Type = EventType.Message, Code = code });
            return this;
        }

        internal EventData Translate(EventData evt)
        {
            if (evt is KeyboardEventData kb)
            {
                if(_Keys.TryGetValue(kb.Modifiers, out var kc))
                {
                    if (kc.TryGetValue(kb.Key, out var reg))
                    {
                        // found hot key!
                        object? arg = reg.ArgFixed;
                        if (reg.ArgGen != null)
                        {
                            arg = reg.ArgGen();
                        }
                        if (reg.ArgGen2 != null)
                        {
                            arg = reg.ArgGen2(arg);
                        }
                        switch(reg.Type)
                        {
                            case EventType.Message:
                                return new MessageEventData(reg.Code, null, arg);
                            default:
                                throw new NotImplementedException();
                        }
                    }
                }
            }
            return evt;
        }
    }
}