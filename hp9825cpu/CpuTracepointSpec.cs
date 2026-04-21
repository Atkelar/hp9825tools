using System;

namespace HP9825CPU
{
    internal class CpuTracepointSpec
        : TracepointSpec
    {
        private int _Address;

        public CpuTracepointSpec(int address, string name, string label, string messageTemplate, TraceCategory category  = TraceCategory.Normal) 
            : base(name, label, category)
        {
            _Address = address;
            _Prefix = Convert.ToString(_Address, 8) + " ";
            this.SetMessageTemplate(messageTemplate);
        }

        private string _Prefix;

        protected override string LogLinePrefix()
        {
            return _Prefix;
        }
    }
}