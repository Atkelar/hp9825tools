using System;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// Provides a base class for visual IO - inheritors provide the way to actually print to the screen.
    /// </summary>
    /// <remarks>
    /// <para>Life cycle is as follows: new() -> Initialize() -> n*( Start() -> Stop() ) -> Dispose()</para>
    /// </remarks>

    public abstract class Screen
        : IDisposable
    {
        /// <summary>
        /// Initializes the screen handler.
        /// </summary>
        protected Screen()
        {
        }

        public Size Minimum { get; private set; }
        public Size? Maximum { get; private set; }
        
        public Size CurrentSize { get; private set; }

        private Size VisibleSize;

        /// <summary>
        /// True to indicate that the screen handler is "running" - i.e. active.
        /// </summary>
        public bool IsActive { get; private set; }

        /// <summary>
        /// Call whenever the avaliable real estate changes. This has to be at least one across and one high!
        /// </summary>
        protected virtual void SizeChanged()
        {
            VisibleSize = GetCurrentSize();

            Size trimmed = VisibleSize.TrimTo(Minimum, Maximum);
            ResizeScreenBuffer(trimmed);
            CurrentSize = trimmed;

            RootVisual?.ParentSizeChanged(CurrentSize);

            if (IsActive)
                Invalidate();
        }

        // screen buffer is kept in a "char array" and a "color array" fashion to facilitate faster string operations with char-ranges if needed.

        CharBuffer? _ScreenBuffer;

        private const int LineBufferSnapSize = 5;
        private const int CharBufferSnapSize = 15;

        private void ResizeScreenBuffer(Size targetSize)
        {
            if (_ScreenBuffer == null)
                _ScreenBuffer = new CharBuffer(targetSize);
            else
            {
                // check to see if we need to resize the existing buffer at all...
                if (targetSize.Width > _ScreenBuffer.Size.Width || targetSize.Height > _ScreenBuffer.Size.Height)
                {
                    // yep, At least one side is larger...
                    var newBuf = new CharBuffer(new Size(targetSize.Width + CharBufferSnapSize, targetSize.Height + LineBufferSnapSize));
                    newBuf.CopyFrom(_ScreenBuffer, Location.Origin, Location.Origin, _ScreenBuffer.Size.TrimTo(newBuf.Size));
                    _ScreenBuffer = newBuf;
                }
                else
                {
                    if (targetSize.Height < _ScreenBuffer.Size.Height - LineBufferSnapSize || targetSize.Width < _ScreenBuffer.Size.Width - CharBufferSnapSize)
                    {
                        // yep. optimize for smaller buffer...
                        var newBuf = new CharBuffer(new Size(targetSize.Width + CharBufferSnapSize / 2, targetSize.Height + LineBufferSnapSize / 2));
                        newBuf.CopyFrom(_ScreenBuffer, Location.Origin, Location.Origin, _ScreenBuffer.Size.TrimTo(newBuf.Size));
                        _ScreenBuffer = newBuf;
                    }
                    else
                    {
                        // we can keep the current buffer.
                    }
                }
            }
        }


            // if (Lines != null)
            // {
            //     // we need to be a bit conservative. Resize physical buffer only if the
            //     // new size is less than half the target, and increase by a margin...
            //     LineBuffer[] newBuf;
            //     if (targetSize.Height < Lines.Length - LineBufferSnapSize)
            //     {
            //         // resize to target + a buffer...
            //         int h = Math.Max(targetSize.Height, Minimum.Height);
            //         newBuf = new LineBuffer[h];
            //     }
            //     else
            //     {
            //         if (targetSize.Height > Lines.Length)
            //         {
            //             int h = targetSize.Height + LineBufferSnapSize;
            //             newBuf = new LineBuffer[h];
            //         }
            //         else
            //         {
            //             newBuf = Lines; // no copy neede for the lines...
            //         }
            //     }
            //     // copy old lines to newBuf, keeping width changes in mind...

            //     int? resizeWidth;
            //     if (targetSize.Width < Lines[0]._CharBuffer.Length)


            //     Lines = newBuf;
            // }
            // else
            // {
            //     // no copy needed; fresh start!
            //     Lines = new LineBuffer[targetSize.Height];
            //     for(int i=0;i<targetSize.Height;i++)
            //     {
            //         Lines[i]._CharBuffer = new char[targetSize.Width];
            //         Lines[i]._ColorBuffer = new byte[targetSize.Width];
            //         Array.Fill(Lines[i]._CharBuffer, ' ');
            //     }
            // }

        protected abstract Size GetCurrentSize();

        /// <summary>
        /// Gets the internal data structures set up and connects global events or similar constructs.
        /// </summary>
        /// <param name="host">The hosting object to handle I/O and other important synchronization stuff.</param>
        /// <param name="minimiumSize">The absolute minimum size we can run on - anything smaller will be clipped!</param>
        /// <param name="maximumSize">The absolute maximum size we can support. Null to indicate no limit.</param>
        public void Initialize(IVisualHost host, Size minimiumSize, Size? maximumSize = null)
        {
            if (Initialized)
                throw new ApplicationStateException("Cannot re-initialize a screen object!");
            Host = host;
            Minimum = minimiumSize;
            Maximum = maximumSize;

            Initialized = true;

            // just in case...
            _Root?.SetHost(Host);
        }

        private Visual? _Root;

        public Visual? RootVisual {
            get 
            {
                return _Root;
            } 
            set
            {
                if (object.ReferenceEquals(_Root, value))
                    return;

                if (IsActive)
                    throw new ApplicationStateException("Cannot change the root visual during execution!");
                if (_Root != null)
                    _Root.SetHost(null);
                
                _Root = value;
                _Root?.SetHost(Host);
            }
        }

        public IVisualHost? Host { get; private set; }

        private bool Initialized = false;

        /// <summary>
        /// Forces a repaint of the provided area.
        /// </summary>
        /// <param name="rect">The area to repaint, null for "all of it"</param>
        public void Invalidate(Rectangle? rect = null)
        {
            Rectangle rDo = rect ?? new Rectangle(Location.Origin, VisibleSize);
            //RepaintArea = RepaintArea.HasValue ? RepaintArea.Value.Union(rDo) : rDo;
            Host?.QueueMessage(MessageCodes.Paint, null, rDo);
        }

        //protected Rectangle? RepaintArea { get; private set; }
        /// <summary>
        /// True to indicate that the driver supports a redirected "stdout". Defaults to false.
        /// </summary>
        public virtual bool SupportsRedirectedConsole { get => false; }

        /// <summary>
        /// Repaints any invalidated area, or does nothing if no area is in need of painting.
        /// </summary>
        public void Paint(Rectangle r)
        {
            if(!IsActive || _Root == null || _ScreenBuffer == null)
                return;

            _Root.HandlePaint(r, Location.Origin, _ScreenBuffer);
            FlushBuffer(_ScreenBuffer, r);
        }

        private void FlushBuffer(CharBuffer screenBuffer, Rectangle repaintArea)
        {
            if (screenBuffer.Lines==null)
                return;
            var br = repaintArea.BottomRight!.Value;
            for(int y = repaintArea.Position.Y; y <= br.Y;y++)
            {
                var l = screenBuffer.Lines[y];
                int start = repaintArea.Position.X;
                byte b = l._ColorBuffer[start];
                int count=0;
                for(int x = repaintArea.Position.X; x <= br.X; x++)
                {
                    if (l._ColorBuffer[x] != b)
                    {
                        BufferToScreen(start, y, b, l._CharBuffer, start, count);
                        start = x;
                        count = 0;
                        b = l._ColorBuffer[x];
                    }
                    count++;
                }
                if (count>0)
                {
                    BufferToScreen(start, y, b, l._CharBuffer, start, count);
                }
            }
        }

        protected virtual void BufferToScreen(int x, int y, byte colorCode, char[] buffer, int index, int count)
        {
            ConsoleColor f,b;
            Palette.Extract(colorCode, out f, out b);
            BufferToScreen(x,y, f,b, buffer, index, count);
        }

        protected abstract void BufferToScreen(int x, int y, ConsoleColor foreground, ConsoleColor background, char[] buffer, int index, int count);

        protected virtual void Starting()
        {}

        protected virtual void Stopping()
        {}

        /// <summary>
        /// Starts the screen handling and update process.
        /// </summary>
        public void Start()
        {
            EnsureAlive();
            if (IsActive)
                throw new ApplicationStateException("Start during run phase called!");
            if (!Initialized)
                throw new ApplicationStateException("Start called without Initialize!");
            if (_Root == null)
                throw new ApplicationStateException("No root visual defined!");

            SizeChanged();

            Starting();

            IsActive = true;

            Invalidate();
        }

        /// <summary>
        /// Stops the screen handling and update process.
        /// </summary>
        public void Stop()
        {
            EnsureAlive();
            if (!IsActive)
                throw new ApplicationStateException("Stop without start called!");
            
            Stopping();

            IsActive = false;
        }

        /// <summary>
        /// Makes sure we are still up and running.
        /// </summary>
        /// <exception cref="ObjectDisposedException">If the screen has already been disposed.</exception>
        protected void EnsureAlive()
        {
            ObjectDisposedException.ThrowIf(IsDisposed, this);
        }

        /// <summary>
        /// Override to implement "beep" message handling.
        /// </summary>
        public virtual void Beep()
        {}

        /// <summary>
        /// Release any external resources.
        /// </summary>
        /// <param name="isDisposing">If true, also call dispose on managed resources.</param>
        protected virtual void Dispose(bool isDisposing)
        {

        }

        private bool IsDisposed = false;

        /// <summary>
        /// Releases used resources.
        /// </summary>
        public void Dispose()
        {
            if (!IsDisposed)
                Dispose(true);
            IsDisposed = true;
        }

        internal void HandleEvent(EventData evt)
        {
            if (evt is MessageEventData md)
            {
                switch(md.Code)
                {
                    case MessageCodes.Beep:
                        Beep();
                        return;
                    case MessageCodes.Paint:
                        if (md.Args != null)
                            Paint((Rectangle)md.Args);
                        else
                            Paint(new Rectangle(Location.Origin, this.CurrentSize));
                        return;
                }
            }
            if (RootVisual != null)
                RootVisual.HandleEvent(evt);
        }
    }
}