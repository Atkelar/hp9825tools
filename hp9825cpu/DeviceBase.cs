using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace HP9825CPU
{
    /// <summary>
    /// Base class for "IO Device" simulations. Derive and handle "ticks" to add the implementation as a device to the CPU simulator.
    /// </summary>
    public abstract class DeviceBase
    {
        /// <summary>
        /// Initializes the device with a specific name for logging and state saving / restoring.
        /// </summary>
        /// <param name="deviceTypeName">The device type name, usually fixed for a specific implementation. Could be "" or "".</param>
        /// <param name="deviceName">The specific device name. Should be unique for the device type within a simulation.</param>
        protected DeviceBase(int defaultSelectCode, string deviceTypeName, string? deviceName)
        {
            _TracePoints = new Dictionary<string, DeviceTracepointSpec>();  // empty list as default to shut up compiler...
            Name = deviceName ?? deviceTypeName;
            Type = deviceTypeName;
            if(defaultSelectCode < 0 || defaultSelectCode>15)
                throw new ArgumentOutOfRangeException(nameof(defaultSelectCode), defaultSelectCode, "Select codes can only range from zero to 15!");
            DefaultSelectCode = defaultSelectCode;
            PrepareTracepoints();
        }

        private void PrepareTracepoints()
        {
            var tpBuilder = new DeviceTracepointBuilder(this);
            InitializeTracePoints(tpBuilder);
            _TracePoints = tpBuilder._Collected;
        }

        private Dictionary<string, DeviceTracepointSpec> _TracePoints;

        /// <summary>
        /// The list of tracepoints that this device supports.
        /// </summary>
        public IEnumerable<ITracepointInfo> Tracepoints { get => _TracePoints.Values; }


        /// <summary>
        /// The default selection code for the device, based on the type.
        /// </summary>
        public int DefaultSelectCode { get; private set; }

        /// <summary>
        /// The display name of the device. For logging/saving...
        /// </summary>
        public string Name { get; private set; }
        /// <summary>
        /// The type name of the device.
        /// </summary>
        public string Type { get; private set; }

        /// <summary>
        /// The Hosting device manager for the simulation. Any communication with the simulated system runs across this object.
        /// </summary>
        public DeviceManager? System { get; internal set; }
        /// <summary>
        /// The "FLG" line signal (=true is "signalled", in hardware = pulled down.)
        /// </summary>
        public virtual bool Flag { get; protected set; }
        /// <summary>
        /// The "STS" line signal (=true is "signalled", in hardware = pulled down.)
        /// </summary>
        public virtual bool Status { get; protected set; }
        /// <summary>
        /// The acutally used select code. Is only set once the device is inserted into a host.
        /// </summary>
        public int? SelectCode { get; internal set; }

        /// <summary>
        /// Resets the simulated device. Will be called whenever the simulated CPU is reset, or when a device requests a reset via the device manager.
        /// </summary>
        protected internal virtual void Reset()
        {
            TracePoint("RESET");
        }

        /// <summary>
        /// Initialize the trace messages for the device that can be enabled/disabled by a debugger host.
        /// </summary>
        protected virtual void InitializeTracePoints(ITracePointBuilder builder)
        {
            builder.Create("HWERR", "Hardware error", TraceCategory.Error)
                .Describe("Occures when the hardware is accessed in a non-defined way; indicates an error in the firmware code - or - an error in the simulated haredware.")
                .MessageTemplate("Hardware error reported: [0]")
                .ParameterDefaults("<unspecified>");
            builder.Create("RESET", "Hardware reset", TraceCategory.Normal)
                .Describe("Occures when the hardware is getting a reset signal from the hosting CPU unit.")
                .MessageTemplate("Reset.");
        }

        /// <summary>
        /// Triggers a registered trace point.
        /// </summary>
        /// <param name="name">The name of the tracepoint.</param>
        /// <param name="args">The arguments for the message template.</param>
        protected void TracePoint(string name, params object?[] args)
        {
            if (_TracePoints.TryGetValue(name, out var tp))
            {
                tp.TriggerForDevice(this, args);
            }
            else
            {
                this.System?.HostCpu?.LogDebugMessage(TraceCategory.Error, "Trace point {0} requested but not defined by device {1}!", name, this.Name);
            }
        }

        /// <summary>
        /// Report diagnostic message and/or trigger a breakpoint in the simulation, caused by an error in HW access strategy.
        /// </summary>
        /// <param name="message">The informational message to display.</param>
        /// <param name="args">Placeholders for the message.</param>
        protected void ReportHardwareAccessError(string message, params object?[] args)
        {
            ReportHardwareAccessError(string.Format(message, args));
        }

        /// <summary>
        /// Report diagnostic message and/or trigger a breakpoint in the simulation, caused by an error in HW access strategy.
        /// </summary>
        /// <param name="message">The informational message to display.</param>
        protected void ReportHardwareAccessError(string message)
        {
            TracePoint("HWERR", message);
            if (System != null)
                System.ReportHardwareAccessError(this, message);
            else
                Debug.WriteLine("Unconnected device {0} ({1}) reproted hardware controlling issue: {2}", this.Name, this.Type, message);
        }


        /// <summary>
        /// Pushes the buttons for an interrupt request. Note that this is a multi-step process that may also fail... Should be called during any "Tick" code and will trigger the interrupt handling on the next tick, as per spec.
        /// </summary>
        protected void RequestInterrupt()
        {
            System?.RequestInterrupt(this);
        }

        /// <summary>
        /// Called by the distribution agent, handles interrupt and DMA stuff, then calls "Tick".
        /// </summary>
        internal void TickInternal()
        {
            Tick();
        }

        /// <summary>
        /// Called from the device manager, whenever the CPU writes to an IO register (R4-R7). NOTE: the regIndex will be in "device number space", i.e. 0-3, reflecting the two IO lines.
        /// </summary>
        /// <param name="regIndex">The index of the register to write (0-3).</param>
        /// <param name="value">The value to write (0-0xFFFF)</param>
        protected internal abstract void WriteIORegister(int regIndex, int value);
        /// <summary>
        /// Called from the device manager, whenever the CPU reads an IO register (R4-R7). NOTE: the regIndex will be in "device number space", i.e. 0-3, reflecting the two IO lines.
        /// </summary>
        /// <param name="regIndex">The index of the register to read (0-3)</param>
        /// <returns>The read value (0-0xFFFF)</returns>
        protected internal abstract int ReadIORegister(int regIndex);

        /// <summary>
        /// Called upon every "clock" tick (=CPU instruction) - use this to track the passage of time and queue interrupts or DMA requests as needed.
        /// </summary>
        protected internal abstract void Tick();

        /// <summary>
        /// Called from the device manager, when the CPU has granted the interrupt and is about to execute the matching service routine.
        /// </summary>
        protected internal virtual void InterruptConfirmed()
        {
        }

        private class DeviceTracepointBuilder
            : ITracePointBuilder
        {
            private readonly DeviceBase _Parent;
            public Dictionary<string, DeviceTracepointSpec> _Collected = new Dictionary<string, DeviceTracepointSpec>();
            public DeviceTracepointBuilder(DeviceBase parent)
            {
                _Parent = parent;
            }

            public ITracePointDetailBuilder Create(string name, string label, TraceCategory category = TraceCategory.Normal)
            {
                if (_Collected.ContainsKey(name))
                    throw new ArgumentOutOfRangeException(nameof(name), name, "Already defined!");
                DeviceTracepointSpec spec = new DeviceTracepointSpec(_Parent, name, label, category);
                this._Collected.Add(name, spec);
                return new DeviceTracepointDetailBuilder(spec);
            }
        }
    }

}