using System;

namespace HP9825CPU
{
    internal class DeviceTracepointSpec
        : TracepointSpec
    {
        private string name;
        private string label;
        private TraceCategory category;
        private readonly DeviceBase _Parent;

        public DeviceTracepointSpec(DeviceBase parent, string name, string label, TraceCategory category)
            : base(name, label, category)
        {
            this.name = name;
            this.label = label;
            this.category = category;
            this._Parent = parent;
        }

        protected override string LogLinePrefix()
        {
            return string.Format("{0} ({1}) ", _Parent.Name, _Parent.SelectCode);
        }

        protected override Func<CpuSimulator, object?[], object?>? CreateValueGetter(string definition)
        {
            switch(definition)
            {
                case "FLG": 
                    return (x,y) => _Parent.Flag ? "FLAG ON" : "FLAG OFF";
                case "STS":
                    return (x,y) => _Parent.Status ? "STAT ON" : "STAT OFF";

            }
            return null;
        }

        internal void TriggerForDevice(params object?[] messageArgs)
        {
            if(_Parent.System?.HostCpu != null)
            {
                base.Triggerred(_Parent.System.HostCpu, messageArgs);
            }
        }
    }
}