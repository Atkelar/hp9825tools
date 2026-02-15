using System;

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
        public DeviceBase(string deviceTypeName, string? deviceName)
        {
            Name = deviceName ?? deviceTypeName;
            Type = deviceTypeName;
        }
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
        /// Resets the simulated device. Will be called whenever the simulated CPU is reset, or when a device requests a reset via the device manager.
        /// </summary>
        protected internal virtual void Reset()
        {
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
    }
}