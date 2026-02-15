using System;
using System.Diagnostics.CodeAnalysis;

namespace CommandLineUtils.Visuals
{
    public abstract class Visual
    {
        public Location Position { get; set; }

        public Size Size { get; set; }

        /// <summary>
        /// Returns the are covered by THIS visual, respecitve to its parent coordinate system.
        /// </summary>
        public Rectangle ClientArea { get => new Rectangle(Position, Size); }

        public Container? Parent { get; private set; }

        public bool IsVisible   => _Shown && (Parent == null || (Parent.IsVisible && !Parent.ClipClient(ClientArea).IsEmpty));

        private bool _Shown;
        public virtual void Show()
        {
            _Shown = true;
        }

        public virtual void Hide()
        {
            _Shown = false;
            Parent?.InvalidateChild(this);
        }

        private IVisualHost? _ExplicitHost;

        protected IVisualHost? Host 
        {
            get => _ExplicitHost ?? Parent?.Host;
        }

        internal void SetHost(IVisualHost? host)
        {
            bool triggerInit = _ExplicitHost == null && host != null;
            _ExplicitHost = host;
            if (triggerInit)
                InitializeEnvironment();
        }

        /// <summary>
        /// Called when the visual enters an "alive" environment.
        /// </summary>
        protected internal virtual void InitializeEnvironment()
        {
            Palette  = Host?.PaletteFor(this.GetType());
        }

        protected bool IsActive {get; private set;}

        protected Palette? Palette {get; private set;}

        internal void Hookup(Container? parent)
        {
            bool initialize = Parent == null && parent?.Host != null;
            Parent = parent;
            if (initialize)
                InitializeEnvironment();
        }

        /// <summary>
        /// Convert the provided location from global coordinates to local based coordinates.
        /// </summary>
        /// <param name="globalLocation">The global position on the screen.</param>
        /// <returns>A localized version - might be out of bounds!</returns>
        public Location GlobalToLocal(Location globalLocation)
        {
            var pos = GetAbsolutePosition();
            return globalLocation.RelateiveTo(pos);
        }

        /// <summary>
        /// Gets the position of the visual in absolute screen (ultimate parent!) coordinates.
        /// </summary>
        /// <returns>The corrected, global position.</returns>
        public Location GetAbsolutePosition()
        {
            if (Parent != null)
                return Parent.GetAbsolutePosition().Move(Position);
            return Position;
        }

        /// <summary>
        /// Convert the provided location from global coordinates to local based coordinates.
        /// </summary>
        /// <param name="globalLocation">The global position on the screen.</param>
        /// <returns>A localized version - or null if it is out of bounds!</returns>
        public Location? GlobalToLocalCLipped(Location globalLocation)
        {
            var pos = GlobalToLocal(globalLocation);
            if (pos.X < 0 || pos.Y < 0 || pos.X>=Size.Width || pos.Y >= Size.Height)    // out of bounds.
                return null;
            return pos;
        }

        /// <summary>
        /// Will be called whenever the parent visual has changed its size.
        /// </summary>
        /// <param name="newSize">The new size available for this visual.</param>
        public virtual void ParentSizeChanged(Size newSize)
        {
            // attached borders would be handled here...
        }

        /// <summary>
        /// Implements the actual painting code.
        /// </summary>
        /// <param name="p">The output context. Note that all operations are within the current client range; 0/0 is the first character of THIS visual.</param>
        protected abstract void Paint(PaintContext p);

        internal virtual void HandlePaint(Rectangle clipRectangle, Location parentOffset, CharBuffer targetBuffer)
        {
            // check if we care...
            var myRect = clipRectangle.Intersect(new Rectangle(Location.Origin, Size));
            if (myRect.IsEmpty)
                return; // nope.
            //parentOffset = parentOffset.Move(Position);  .MoveBack(ClientArea.Position)
            PaintContext thisContext = new PaintContext(Palette, myRect, parentOffset, targetBuffer);
            Paint(thisContext);
        }

        protected void QueueMessage(string code, object? parameters=null)
        {
            Host?.QueueMessage(code, this, parameters);
        }

        protected internal virtual bool HandleEvent(EventData latestEvent)
        {
            return false;
        }

        /// <summary>
        /// Invalidate the visual, triggering a "paint" event.
        /// </summary>
        /// <param name="area">The area inside the visual to invalidate, null for "all".</param>
        protected internal void Invalidate(Rectangle? area = null)
        {
            if (!_Shown || Parent == null)
                return;
            
            area ??= new Rectangle(Location.Origin, Size);
            Parent.InvalidateChild(this, area.Value);
        }
    }
}