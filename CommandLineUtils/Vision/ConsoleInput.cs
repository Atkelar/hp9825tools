using System.Threading.Tasks;
using System;
using System.Runtime.InteropServices;
using System.ComponentModel.DataAnnotations;

namespace CommandLineUtils.Visuals
{
    internal class ConsoleInput
        : Input
    {
        public ConsoleInput()
        {
            
        }

        private void HandleCancelKey(object? sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            QueueMessage(MessageCodes.MessageQuit, null);
        }

        protected internal override void Start()
        {
            // TODO: hook up the posix "bg/fg" events...
            base.Start();
            Console.TreatControlCAsInput = true;        
            Console.CancelKeyPress += HandleCancelKey;  // go to 100% to make sure we get everything...
        }
        protected internal override void Stop()
        {
            Console.CancelKeyPress += HandleCancelKey;
            Console.TreatControlCAsInput = false;
            base.Stop();
        }

        protected override Task<KeyboardEventData?> GetPendingKeyboardInput()
        {
            if (Console.KeyAvailable)
            { 
                return Task.FromResult<KeyboardEventData?>(new KeyboardEventData(Console.ReadKey(true)));
            }
            return Task.FromResult<KeyboardEventData?>(null);
        }

        protected override Task<MouseEventData?> GetPendingMouseInput()
        {
            return Task.FromResult<MouseEventData?>(null);
        }

    }
}