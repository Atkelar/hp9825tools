# Vision - EXPERIMENTAL!

"Vision" is my version of "Turbo Vision", a DOS based "gui" style framework.
It was used in quite a few applications around that era, recognizable by the
gray/blue color scheme with green buttons.

Note that these days I work in Linux, which does complicate things a bit on 
the console window front. Windows console is pretty much like a screen buffer
in a window, with direct access to the character cells, while Linux simulates
a line printer by default pretty much, and adds "escape sequences" to do
cursor positioning and color codes.

## "Drivers"

The framework uses an internal draw buffer that is similar to the screen buffer
of the Windows Console window: one (UTF-16) character and one attribute byte per
character cell. Upon update, these are sent off to the "driver" of the running
app. The driver can be considered a dependency injected OS depending implementation.

Currently, there is an implementation for the Linux console via the "Console" class
of .NET; this driver should work on just about every paltform that is supported
by .NET, but is untested outside my own (Linux Mint) environment.


## TODO's...

### Add true DI

DI is a nifty way of hiding platform specific details; would be nice to have
DI in the running process, and for creating new "visuals" when needed. Should
be simple enough, but you know...

### Mouse supprot

Like the character support, the mouse input translation in Windwos works
completely different than in Linux terminals. Since the mouse is considered
optional anyway, It's a lower priority TODO.

### Clipboard support

Most "Linux Clipboard libraries for .NET" I find use a "shell execute" with
a clipboard command line utility; which is quite a long way around IMHO.

The Idea would be to hook up copy/paste to the true OS clipboard, thus
enabling at least text data exchange between the running app and other
apps. Ideally also file/directory lists via the "file browser" of the 
OS ("Explorer equivalent")...


A rather direct X11 clipboard hookup:
 https://github.com/AvaloniaUI/Avalonia/tree/master/src/Avalonia.X11/Clipboard

