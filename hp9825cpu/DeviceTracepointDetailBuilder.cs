namespace HP9825CPU
{
    internal class DeviceTracepointDetailBuilder
        : ITracePointDetailBuilder
    {
        private DeviceTracepointSpec spec;

        public DeviceTracepointDetailBuilder(DeviceTracepointSpec spec)
        {
            this.spec = spec;
        }

        public ITracePointDetailBuilder Describe(string text)
        {
            spec.Description = text;
            return this;
        }

        public ITracePointTrigger Make()
        {
            return new TriggerDeviceTrace(spec);
        }

        public ITracePointDetailBuilder MessageTemplate(string template)
        {
            spec.SetMessageTemplate(template);
            return this;
        }

        public ITracePointDetailBuilder ParameterDefaults(params object?[] defaults)
        {
            spec.DefaultParameters = defaults;
            return this;
        }
        private class TriggerDeviceTrace
            : ITracePointTrigger
        {
            private DeviceTracepointSpec spec;

            public TriggerDeviceTrace(DeviceTracepointSpec spec)
            {
                this.spec = spec;
            }

            public void Invoke(params object?[] args)
            {
                spec.TriggerForDevice(args);
            }
        }
    }

}