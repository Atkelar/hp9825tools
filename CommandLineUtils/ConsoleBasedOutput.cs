using System;
using System.IO;
using System.Runtime.CompilerServices;
namespace CommandLineUtils
{
    public class ConsoleBasedOutput
        : OutputHandlerBase
    {
        public ConsoleBasedOutput(bool warnInError = false)
            : base()
        {
            WarningsInError = warnInError;
            _OriginalColor = Console.ForegroundColor;
        }

        private ConsoleColor _OriginalColor;

        protected override void Dispose(bool isDisposing)
        {
            base.Dispose(isDisposing);
            Console.ForegroundColor = _OriginalColor;
        }

        private class FileBasedSpec
            : DisplaySpec
        {
            private TextWriter _Target;

            public FileBasedSpec(System.IO.TextWriter target)
                : base()
            {
                _Target = target;
            }
            public override void WriteLine(string ouptut)
            {
                _Target.WriteLine(ouptut);
            }
            public override void Flush()
            {
                _Target.Flush();
            }
            public override int? DisplayWidth => null;
        }

        private class ConsoleSpec
            : DisplaySpec
        {
            public ConsoleSpec(ConsoleColor color)
            {   
                _Color = color;
            }
            public override void WriteLine(string ouptut)
            {
                Console.ForegroundColor = _Color;
                Console.WriteLine(ouptut);
            }
            private ConsoleColor _Color;
            public override int? DisplayWidth => Console.BufferWidth;
        }

        private bool WarningsInError;
        protected override DisplaySpec CreateFor(VerbosityLevel level)
        {
            switch(level)
            {
                case VerbosityLevel.Errors:
                    if (Console.IsErrorRedirected)
                        return new FileBasedSpec(Console.Error);
                    else
                        return new ConsoleSpec(ConsoleColor.Red);
                case VerbosityLevel.Warnings:
                    if (WarningsInError && Console.IsErrorRedirected)
                        return new FileBasedSpec(Console.Error);
                    else
                        return new ConsoleSpec(ConsoleColor.Yellow);
                case VerbosityLevel.Normal:
                    if (Console.IsOutputRedirected)
                        return new FileBasedSpec(Console.Out);
                    else
                        return new ConsoleSpec(Console.ForegroundColor);
                case VerbosityLevel.Verbose:
                    return new ConsoleSpec(ConsoleColor.DarkCyan);
                case VerbosityLevel.Trace:
                    return new ConsoleSpec(ConsoleColor.DarkGray);
                default:
                    throw new NotImplementedException();                   
            }
        }
    }

}