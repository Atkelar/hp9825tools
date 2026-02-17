using System;
using System.Collections.Generic;
using System.Formats.Asn1;
using System.Net.Mime;
using System.Security.Cryptography;

namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// A simple 
    /// </summary>
    public class Container
        : Visual
    {
        protected override void Paint(PaintContext p)
        {
            // TODO background maybe?
        }
        private List<Visual> _Children = new List<Visual>();

        protected char FillerChar {get; set;} = ' ';

        /// <summary>
        /// Paint the visual to the provided buffer...
        /// </summary>
        /// <param name="clipRectangle">The clip rectangle to use, within the current Visual.</param>
        /// <param name="parentOffset">The offset of the "0/0" coordinate for THIS visual in the absolute buffer.</param>
        /// <param name="targetBuffer">The output target.</param>
        internal override void HandlePaint(Rectangle clipRectangle, Location parentOffset, CharBuffer targetBuffer)
        {
            if (!IsVisible)
                return;
            // check if we care...
            var myRect = clipRectangle.Intersect(ClientArea);
            if (myRect.IsEmpty)
                return; // nope.
            //parentOffset = parentOffset.Move(Position);
            var childRect = clipRectangle.Intersect(ChildArea);
            if(!ChildArea.Equals(ClientArea))
            {
                // Handle border draws;  find areas around the cildren area that needs drawing...
                var borderParts = myRect.SplitAround(ChildArea);
                for(int i =0;i<borderParts.Length;i++)
                {
                    myRect = clipRectangle.Intersect(borderParts[i]);
                    if(!myRect.IsEmpty)
                    {   // got to paint a part of the border...
                        PaintContext thisContext = new PaintContext(Palette, myRect, parentOffset, targetBuffer);
                        Paint(thisContext); // local paint only relevant when we have borders...
                    }
                }
            }
            if (childRect.IsEmpty)
                return;
            var ofs = parentOffset.Move(ChildArea.Position);

            var pendingAreas = new List<Rectangle>();
            pendingAreas.Add(childRect);
            
            for (int i = _Children.Count-1; i>= 0; i--)
            {
                for(int j = pendingAreas.Count-1;j>=0;j--)
                {
                    var pj = pendingAreas[j];
                    if (!pj.IsEmpty && _Children[i].IsVisible)    // old leftover... go against child visual if visible...
                    {
                        var cr = _Children[i].ClientArea.Move(ChildrenScrollOffset);
                        myRect = cr.Intersect(pj);
                        if (!myRect.IsEmpty)    // we have a match...
                        {
                            _Children[i].HandlePaint(myRect.MoveBack(cr.Position), ofs.Move(cr.Position), targetBuffer);
                            pendingAreas.AddRange(pj.SplitAround(myRect));
                            pendingAreas[j] = Rectangle.Empty;  // no longer this one..
                        }
                    }
                }
            }

            foreach(var leftover in pendingAreas)
            {
                if (!leftover.IsEmpty)
                {
                    targetBuffer.Fill(leftover.Move(ofs), FillerChar);
                    targetBuffer.FillColor(leftover.Move(ofs), Palette?.ByIndex(0) ?? PaletteHandler.MonoPalette.ByIndex(0));
                    // cover area with background char...
                }
            }
        }

        protected internal override void InitializeEnvironment()
        {
            base.InitializeEnvironment();
            foreach(var c in _Children)
                c.InitializeEnvironment();
        }

        public void AddChild(Visual childVisual, bool putOnTop = true)
        {
            if(childVisual.Parent != null)
                throw new InvalidOperationException("Cannot insert visual twice!");
            if (putOnTop)
            {
                _Children.Add(childVisual);
            }
            else
            {
                _Children.Insert(0, childVisual);
            }
            childVisual.Hookup(this);
            if (childVisual.IsVisible)
                childVisual.Invalidate();
        }

        protected internal override bool HandleEvent(EventData latestEvent)
        {
            if (base.HandleEvent(latestEvent))
                return true;
            for (int i = _Children.Count-1; i>= 0; i--)
                if (_Children[i].HandleEvent(latestEvent))
                    return true;
            return false;
        }

        /// <summary>
        /// A location to move the entire children list around within the child area. Scroll-offset is added to the client coordinates for screen coordinates. To scroll the children "up", the X offset has to get negative!
        /// </summary>
        public Location ChildrenScrollOffset {get;set;}

        /// <summary>
        /// Gets the provided rectangle, as "child coordinate space" and translates it into "local space" while also trimming it to the visible part of the child space.
        /// </summary>
        /// <param name="r"></param>
        /// <returns></returns>
        public Rectangle FromChildRectangle(Rectangle r)
        {
            return ChildArea.Intersect(r.Move(ChildrenScrollOffset).Move(ChildArea.Position));
        }

        /// <summary>
        /// Invalidate the provided area for the child in question.
        /// </summary>
        /// <param name="child">The child element.</param>
        /// <param name="rectangle">The rectangle to invalidate, relative to the child.</param>
        public void InvalidateChild(Visual child, Rectangle? rectangle = null)
        {
            if (!IsVisible) // don't bother!
                return;
            Rectangle total;
            if (rectangle.HasValue)
            {
                if (!IsVisible || rectangle.Value.IsEmpty || child.Parent != this) // we aren't visible ourselves, never mind!
                    return;
                rectangle = rectangle.Value.Move(child.Position);
                total = child.ClientArea.Intersect(rectangle.Value);  // intersect with the visible part.
            }
            else
            {
                rectangle = total = child.ClientArea;
            }

            // check to see if we need to trim stuff...
            // we need to go through the children list from top to bottom, until we find the requested child.
            // if at any time until then, we have an overlap, trim and split as needed. If 100% overlap. Exit.
            if (total.IsEmpty)
                return;

            Queue<Rectangle> workQueue=new Queue<Rectangle>();
            workQueue.Enqueue(total);
            Queue<Rectangle> outputQueue= new Queue<Rectangle>();

            for (int i = _Children.Count-1; i>=0; i--)
            {
                var tc = _Children[i];
                if (tc == child)
                    break;
                if(!tc.IsVisible)   // ignore, is hidden...
                    continue;
                while (workQueue.Count>0)
                {
                    var rWork = workQueue.Dequeue();
                    if (tc.ClientArea.Covers(rWork))    // 100% covered.    this part is out.
                        continue;
                    if (!tc.ClientArea.Intersects(rWork)) // completely uncovered.
                    {
                        outputQueue.Enqueue(rWork);
                        continue;
                    }
                    // partial intersection...
                    Rectangle[] parts = rWork.SplitAround(tc.ClientArea);
                    for(int j = 0; j < parts.Length; j++)
                        outputQueue.Enqueue(parts[j]);
                }
                var x = workQueue; 
                workQueue = outputQueue;
                outputQueue = x;
            }

            // left over segment(s) go up the chain and to the host...
            while (workQueue.Count>0)
            {
                var rWork = workQueue.Dequeue();
                rWork = FromChildRectangle(rWork); // make "absolute" in our client view part.
                if (rWork.IsEmpty)
                    continue;

                if (Parent != null)
                {
                    Parent.InvalidateChild(this, rWork);
                }
                else
                {
                    Host?.QueueMessage(MessageCodes.Paint, this, rWork);
                }
            }
        }

        /// <summary>
        /// Gets the "inner" area, measured from the container itself. Windows will add the border here.
        /// </summary>
        public virtual Rectangle ChildArea => new Rectangle(Location.Origin, Size);

        /// <summary>
        /// Clips the client based area to the current "inner" area of the container.
        /// </summary>
        /// <param name="clientArea">The client are relative to the container.</param>
        /// <returns>The intersection of the visible area.</returns>
        internal Rectangle ClipClient(Rectangle clientArea)
        {
            return ChildArea.Intersect(clientArea);
        }
    }
}