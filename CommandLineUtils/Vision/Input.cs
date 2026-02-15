using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CommandLineUtils.Visuals
{

    /// <summary>
    /// Input driver class.
    /// </summary>
    public abstract class Input
        : IDisposable
    {
        public void QueueMessage(string messageCode, Visual? sender, object? args = null)
        {
            _MessageQueue.Enqueue(new MessageEventData(messageCode, sender, args));
        }

        private Queue<EventData> _MessageQueue = new Queue<EventData>();

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
        }

        protected internal virtual void Start()
        {
            this.QueueMessage(MessageCodes.MessagePaint, null);
        }

        private int IdleCounter;

        /// <summary>
        /// runs the core "fetch next message" logic and also makes sure that the input devices get polled...
        /// </summary>
        /// <returns>An event if the application has to continue running, null if we have met an exit condition.</returns>
        public async Task<EventData?> WaitForEvent()
        {
            // TODO: possibly re-use event data instances... to avoid a slew of unnecessary objects...
            
            // strategy: we want to buffer any input events that might be pending...
            // problem: we want to make sure, that any derived classes that can "push" input
            // rather than poll it, can do so...
            await PollForInputs();
            if (_MessageQueue.Count > 0)
            {
                IdleCounter = 0;    // next idle is "immediate"...
                var msg = _MessageQueue.Dequeue();
                if (msg is MessageEventData me && me.Code == MessageCodes.MessageExit)
                {
                    return null;
                }
                return msg;
            }
            if (IdleCounter > 0)
            {
                // we are in a true idle condition; no more messages, no input, and the first idle has already been server.
                if (IdleCounter < 500)  // first five idle seconds is rather fast paced...
                {
                    await Task.Delay(10);
                    IdleCounter++;
                }
                else
                {
                    await Task.Delay(250);
                }
                // we just "waited"; make an attempt at getting more input...
                await PollForInputs();
                if (_MessageQueue.Count>0)
                {
                    IdleCounter=0;
                    return _MessageQueue.Dequeue();
                }
            }
            // no pending message in this round... 
            IdleCounter++;
            return IdleResult;
        }

        public async Task PostProcessEvent(EventData what)
        {
            switch (what)
            {
                case MessageEventData m:
                {
                    if (m.Code == MessageCodes.MessageQuit && !m.Cancel)
                    {
                        _MessageQueue.Clear();  // thorw out pending stuff...
                        QueueMessage(MessageCodes.MessageExit, null);
                    }
                    break;
                }
            }
        }

        private async Task PollForInputs()
        {
            var key = await GetPendingKeyboardInput();
            if(key != null)
                _MessageQueue.Enqueue(key);
            var mouse = await GetPendingMouseInput();
            if (mouse != null)
                _MessageQueue.Enqueue(mouse);
        }

        private static readonly IdleEventData IdleResult = new IdleEventData();

        public virtual bool SupportsRedirectedConsole { get => false; }

        protected abstract Task<KeyboardEventData?> GetPendingKeyboardInput();
        protected abstract Task<MouseEventData?> GetPendingMouseInput();

        protected internal virtual void Stop()
        {

        }
    }
}