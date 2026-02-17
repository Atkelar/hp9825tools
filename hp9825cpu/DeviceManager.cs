using System;
using System.Collections.Generic;
using System.Linq;

namespace HP9825CPU
{
    /// <summary>
    /// Wraps all functionallity used to connect to the I/O side of a simulated CPU.
    /// </summary>
    public class DeviceManager
    {
        /// <summary>
        /// The CPU that is running the show; Note: this is for reference only, do not directly interact with the CPU from a device!
        /// </summary>
        public CpuSimulator? HostCpu { get; internal set; }

        /// <summary>
        /// "Plugs in" a device to the IO bus.
        /// </summary>
        /// <param name="selectCode">The select code of the device - matches the "PA" register for IO access. 0-15 only.</param>
        /// <param name="instance">The instance - simulated device - to add to the system.</param>
        /// <exception cref="InvalidOperationException">Either the selected code is already taken, or the device is already added elsewhere.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Select code invalid.</exception>
        public void Add(int selectCode, DeviceBase instance)
        {
            if(instance.System != null)
                throw new InvalidOperationException("Cannot hook up a device to more than one system!");
            if (_Devices.TryGetValue(selectCode, out var existing))
                throw new InvalidOperationException(string.Format("Duplicate select code for device {0}. {1} already in use by {2}!", instance.Name, selectCode, existing.Name));
            if (selectCode<0 || selectCode>15)
                throw new ArgumentOutOfRangeException(nameof(selectCode), selectCode, "Only 0-15 are allowed for device codes!");
            _Devices.Add(selectCode, instance);
            _BackIndex.Add(instance, selectCode);
            instance.System = this;
        }

        private Dictionary<int, DeviceBase> _Devices = new Dictionary<int, DeviceBase>();
        private Dictionary<DeviceBase, int> _BackIndex = new Dictionary<DeviceBase, int>();

        /// <summary>
        /// The (simulated) time that the hosting CPU has been running. Since the last reset. Can be used to coordinate timing based events.
        /// </summary>
        public TimeSpan RunTime  => (HostCpu?.UpTime) ?? TimeSpan.Zero;

        /// <summary>
        /// The number of "clock ticks" that the hosting CPU has consumed. Estimated based on executed commands.
        /// </summary>
        public long Ticks => HostCpu?.Ticks ?? -1;
        
        internal void WriteIORegister(int selectCode, int regIndex, int value)
        {
            if(_Devices.TryGetValue(selectCode & 0xF, out var dev))
            {
                dev.WriteIORegister(regIndex & 0x3, value & 0xFFFF);
            }
        }
        
        internal int ReadIORegister(int selectCode, int regIndex)
        {
            if(_Devices.TryGetValue(selectCode & 0xF, out var dev))
            {
                return dev.ReadIORegister(regIndex & 0x3) & 0xFFFF;
            }
            return 0;   // missing device will cause "pulled up" negative logic to take, resulting in 0 in the CPU...
        }

        internal void Tick()
        {
            foreach(var d in _Devices.Values)
                d.TickInternal();
        }

        internal void Reset()
        {
            foreach(var d in _Devices.Values)
                d.Reset();
        }

        private int InterruptRequestMask = 0;

        internal InterruptLevel PendingInterruptLevel {get => InterruptRequestMask == 0 ? InterruptLevel.None : (( InterruptRequestMask & 0xF0  ) != 0 ? InterruptLevel.High : InterruptLevel.Low ); }

        internal int? GetSelectCodeForInterruptAndConfirm(InterruptLevel levelToQuery)
        {
            if (InterruptRequestMask == 0)
                return null;
            int devCode = 16;
            int end = 16;
            switch(levelToQuery)
            {
                case InterruptLevel.Low:
                    devCode  = 8;
                    end = -1;
                    break;
                case InterruptLevel.High:
                    end = 7;
                    break;
                default:
                    return null;
            }

            while(devCode > end)
            {
                devCode--;

                if ((InterruptRequestMask & (1<<devCode))!=0)
                    break;
            }

            // we got a query, reset the request... TODO: IRL this is up to the device... but keeping the IRL/IRH lines pulled past the "INT" query seems bad practice...
            InterruptRequestMask = 0;

            if (devCode > end)
            {
                // device select code with highest interrupt priority within the requested level.
                _Devices[devCode].InterruptConfirmed();
                return devCode;
            }

            return null;
        }

        /// <summary>
        /// Request an interrupt from the device...
        /// </summary>
        /// <param name="which">The device object. Must be part of this system.</param>
        internal void RequestInterrupt(DeviceBase which)
        {
            if (_BackIndex.TryGetValue(which, out var index))
            {
                InterruptRequestMask |= (1 << index);
            }
        }

        public DeviceBase? GetAt(int selectCode)
        {
            if (!_Devices.TryGetValue(selectCode, out var x))
                return null;
            return x;
        }
    }
}