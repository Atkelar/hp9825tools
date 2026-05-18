using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace HP9825CPU
{
    internal abstract class TracepointSpec
        : ITracepointInfo
    {
        private string _MessageTemplate;
        private string _Name;
        private string _Label;
        private TraceCategory _Category;

        public Func<CpuSimulator, object?[], object?>[] _ValuesFrom { get; private set; }

        public string Name => _Name;

        public string Label => _Label;

        public TraceCategory Category => _Category;

        public bool IsEnabled { get; set; }

        public int Count => _Count;

        public bool ShowCallStats { get; set; }

        private int _Count;

        public string Description { get; internal set; }
        internal object?[] DefaultParameters {  get; set; }

        protected TracepointSpec(string name, string label, TraceCategory category  = TraceCategory.Normal)
        {
            switch(category)
            {
                case TraceCategory.Normal:
                case TraceCategory.Performace:
                case TraceCategory.Warning:
                case TraceCategory.Diagnostics:
                case TraceCategory.Error:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(category), category, "Trace points must be defined in exactly ONE category!");
            }
            _Count = 0;
            _Name = name;
            _Label = label;
            _Category = category;
            Description = string.Empty;
            _MessageTemplate = label;
            DefaultParameters = Array.Empty<object?>();
            _ValuesFrom = Array.Empty<Func<CpuSimulator, object?[], object?>>();
        }

        internal void SetMessageTemplate(string template)
        {
            InitializeTemplate(template);
        }

        private void InitializeTemplate(string messageTemplate)
        {
            StringBuilder sb = new StringBuilder();
            List<Func<CpuSimulator, object?[], object?>> phpop = new List<Func<CpuSimulator, object?[], object?>>();
            int idx;
            while ((idx = messageTemplate.IndexOf('[')) >=0)
            {
                int idx2 = messageTemplate.IndexOf(']', idx);
                if (idx2<0)
                    break;
                sb.Append(messageTemplate.Substring(0,idx));
                string content = messageTemplate.Substring(idx+1, (idx2 - idx)-1).Trim();
                messageTemplate = messageTemplate.Substring(idx2+1);
                if (content.Length>0)
                {
                    string fmt;
                    var phGetter = CreateValueGetter(content, out fmt);
                    if (phGetter != null)
                    {
                        phpop.Add(phGetter);
                        sb.AppendFormat("{{{0}{1}}}", phpop.Count-1, fmt);
                    }
                }
            }
            if (messageTemplate.Length>0)
                sb.Append(messageTemplate);
            this._MessageTemplate = sb.ToString();
            this._ValuesFrom = phpop.ToArray();
        }

        private static readonly char[] FmtSepChars = ",:".ToCharArray();

        protected virtual Func<CpuSimulator, object?[], object?>? CreateValueGetter(string definition)
        {
            return null;   
        }

        protected virtual Func<CpuSimulator, object?[], object?>? CreateValueGetter(string definition, out string formatSpec)
        {
            int idx = definition.IndexOfAny(FmtSepChars);
            if (idx > 0)
            {
                formatSpec = definition.Substring(idx);
                definition = definition.Substring(0,idx);
            }
            else
                formatSpec = string.Empty;
            var temp = CreateValueGetter(definition);
            if (temp != null)
                return temp;
            if (definition.StartsWith('#')) // we want memory!
            {
                int address;
                definition = definition.Substring(1).Trim().ToLowerInvariant();
                if (definition.EndsWith('b'))
                    address = Convert.ToInt32(definition.Substring(0,definition.Length-1), 8);
                else
                    address = int.Parse(definition);
                if (address < 32 || address > 0xFFFF)
                    throw new ArgumentOutOfRangeException("address", address, "Memory reference in trace point invalid!");
                return (x,y) => x.Memory[address];
            }
            if (definition.StartsWith("*-"))
            {
                // call stack delta...
                int delta = int.Parse(definition.Substring(2).Trim()) - 1;  // one level up is delta 0!
                return (x,y) => Convert.ToString(x.Memory[x.ReadRegister(CpuRegister.R)-delta],8);
            }
            for(int i = 0;i<CpuConstants.RegisterNames.Length; i++)
            {
                if (CpuConstants.RegisterNames[i].Equals(definition))
                {
                    return (x,y) => x.ReadRegister((CpuRegister)i);
                }
            }
            switch (definition)
            {
                case "AR1":
                    return (x,y)=>x.ReadAR1();
                case "AR2":
                    return (x,y)=>x.ReadAR2();
                default:
                    if (int.TryParse(definition, out var argNum))
                    {
                        return (x,y)=>y[argNum];
                    }
                    break;
            }
            // TODO: add memory decoding address...
            return null;
        }

        protected abstract string LogLinePrefix();

        internal void Triggerred(CpuSimulator who, params object?[] messageArgs)
        {
            if (!IsEnabled)
                return;
            var now = who.UpTime;
            _Count++;
            string message = _MessageTemplate;
            if (_ValuesFrom.Length>0)
            {
                if (messageArgs.Length < DefaultParameters.Length)
                {
                    var temp = new object?[DefaultParameters.Length];
                    Array.Copy(messageArgs, 0, temp, 0, messageArgs.Length);
                    for (int i = messageArgs.Length; i < temp.Length; i++)
                        temp[i] = DefaultParameters[i];
                    messageArgs = temp;
                }
                object?[] args = new object[_ValuesFrom.Length];
                for(int i=0;i<args.Length;i++)
                    args[i] = _ValuesFrom[i](who, messageArgs);
                message = string.Format(message, args);
            }
            if (ShowCallStats)
            {
                if  (_LastTriggered.HasValue && now.HasValue)
                    who.LogDebugMessage(_Category, "{0} ({3}* D:{4:0.000}ms): {1} {2}",_Name, LogLinePrefix(), message, _Count, now.Value.Subtract(_LastTriggered.Value).TotalMilliseconds);
                else                
                    who.LogDebugMessage(_Category, "{0} ({3}*): {1} {2}",_Name, LogLinePrefix(), message, _Count);
            }
            else
                who.LogDebugMessage(_Category, "{0}: {1} {2}",_Name, LogLinePrefix(), message);
            _LastTriggered = now;
        }

        protected internal void SaveState(XmlElement te)
        {
            te.SetAttribute("enabled", IsEnabled ? "true" : "false");
            te.SetAttribute("count", Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
            te.SetAttribute("last", _LastTriggered.HasValue ? _LastTriggered.Value.TotalMicroseconds.ToString() : "-");
        }

        private TimeSpan? _LastTriggered;

        public void ResetCount()
        {
            _Count = 0;
            _LastTriggered = null;
        }
    }
}