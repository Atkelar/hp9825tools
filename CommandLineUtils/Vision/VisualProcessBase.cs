using System;
using System.Diagnostics.Tracing;
using System.IO;
using System.Threading.Tasks;

namespace CommandLineUtils.Visuals
{
    public abstract partial class VisualProcessBase
        : ProcessBase
    {
        private PaletteHandler _Palette;

        public VisualProcessParameters Options { get; private set; }

        protected VisualProcessBase()
            : base()
        {}

        protected override bool BuildReturnCodes(ReturnCodeHandler reg)
        {
            Errors = reg.Register<VisualProcessError>();
            return base.BuildReturnCodes(reg);
        }

        protected virtual Size MinSize { get; }

        public ReturnCodeGroup<VisualProcessError> Errors { get; private set; }

        protected virtual ScreenDriver CreateDriver()
        {
            ScreenDriver result = new ScreenDriver();

            result.Configure(MinSize, Options, _Palette);

            return result;
        }

        protected virtual void RegisterPalette(PaletteHandler reg)
        {
            reg.Register<Desktop>("Desktop", "Pattern", ConsoleColor.Gray, ConsoleColor.DarkBlue);
        }

        protected override void BuildArguments(ParameterHandler builder)
        {
            Options = builder.AddOptions<VisualProcessParameters>();
        }

        protected abstract Visual CreateRootVisual();

        private Size MinimumSize()
        {
            var s = Size.Parse(Options?.MinimumSize ?? "40x15");
            if (s.Width < 40 || s.Height<15)
                throw new ArgumentOutOfRangeException("The provided size was invalid. Minimum of 40x15 requred!");
            return s;
        }

        private Size? MaximumSize()
        {
            if (string.IsNullOrWhiteSpace(Options?.MaximumSize))
                return null;

            return Size.Parse(Options.MaximumSize, MinimumSize(), null);
        }

        protected void RegisterStandardHotkeys(HotkeyManager hotkeyManager, bool exit = true)
        {
            if (exit) hotkeyManager.AddMessage(MessageCodes.Quit, ConsoleKey.X, ConsoleModifiers.Alt);
        }
        
        protected virtual void RegisterHotKeys(HotkeyManager hotkeyManager)
        {}

        protected void QueueCommand(string code, object? args)
        {
            this._RunningInput?.QueueMessage(code, null, args);
        }

        private Input? _RunningInput;

        protected override async Task RunNow()
        {
            try
            {
                _Palette = new PaletteHandler();
                RegisterPalette(_Palette);

                var hotkeyManager = new HotkeyManager();

                RegisterHotKeys(hotkeyManager);

                using (var driver = CreateDriver())
                {
                    var screen = driver.Screen ?? throw new ApplicationStateException("Missign screen driver!");
                    var input = driver.Input ?? throw new ApplicationStateException("Missing input driver!");

                    if (!screen.SupportsRedirectedConsole && Console.IsOutputRedirected)
                        throw Errors.Happened(VisualProcessError.RedirectionNotSupported);
                    if (!input.SupportsRedirectedConsole && Console.IsInputRedirected)
                        throw Errors.Happened(VisualProcessError.RedirectionNotSupported);

                    _RunningInput = input;
                    screen.Initialize(driver, MinimumSize(), MaximumSize());
                    screen.RootVisual = CreateRootVisual();
                    screen.RootVisual.Show();
                    screen.Start();
                    input.Start();

                    while (true)
                    {
                        // main application loop...
                        var evt = await input.WaitForEvent();
                        if (evt != null)
                        {
                            evt = hotkeyManager.Translate(evt);
                            if(!this.HandleEvent(evt))
                                screen.HandleEvent(evt);
                            await input.PostProcessEvent(evt);
                        }
                        else
                            break;
                    }
                    input.Stop();
                    screen.Stop();
                }
            }
            catch(ApplicationStateException ex)
            {
                throw Errors.Happened(VisualProcessError.InternalProcessingError, ex);
            }
            finally
            {
                _RunningInput = null;
            }
        }

        protected virtual bool HandleEvent(EventData evt)
        {
            return false;
        }
    }
}