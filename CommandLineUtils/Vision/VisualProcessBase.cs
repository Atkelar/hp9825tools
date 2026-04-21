using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Tracing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// Inheritors implement a visual (console UI) application.
    /// </summary>
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

        public const string DefaultApplicationState = "default";

        protected virtual void BuildApplicationStates(IApplicationStateBuilder app)
        {
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
        private Dictionary<string, object>? _AppStates = null;
        private string? _StartupState = null;
        private string? _CurrentState = null;

        protected override async Task RunNow()
        {
            try
            {
                _Palette = new PaletteHandler();
                RegisterPalette(_Palette);

                InitializeApplicationStates();

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

                    if (!TransitionState(GetStartupState(_StartupState ?? throw new ApplicationStateException("Initiailization didn't yield a default startup state name!"))))
                        throw new ApplicationStateException($"The application's initial state couldn't be enabled!");

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

        private bool TransitionState(string? nextState)
        {
            if (_CurrentState == nextState) // no change? just done.
                return true;

            if (CurrentState != null)
            {
                var e = new ApplicationStateChangingEventArgs(CurrentState, nextState ?? string.Empty);
                OnStateChanging(e);
                if (e.Cancel)
                    return false;
            }
            var oldState = CurrentState;
            _CurrentState = nextState;
            OnStateChanged(new ApplicationStateChangedEventArgs(oldState ?? string.Empty, nextState ?? string.Empty));
            return true;
        }

        protected virtual void OnStateChanged(ApplicationStateChangedEventArgs e)
        {
            if (StateChanged != null)
                StateChanged(this, e);
        }

        public event EventHandler<ApplicationStateChangedEventArgs> StateChanged;
        public event EventHandler<ApplicationStateChangingEventArgs> StateChanging;

        protected virtual void OnStateChanging(ApplicationStateChangingEventArgs e)
        {
            if (StateChanging != null)
                StateChanging(this, e);
        }

        private void InitializeApplicationStates()
        {
            var app = new ApplicationStateBuilder();
            app.AddState(DefaultApplicationState, x=>x.HasCommands());
            _AppStates = app._States;
            _StartupState = app.StartupState;
        }

        /// <summary>
        /// The key (name) of the current application state.
        /// </summary>
        public string? CurrentState { get => _CurrentState; }

        /// <summary>
        /// Override to get a dynamic resolution of the application startup state.
        /// </summary>
        /// <param name="requestedState">The original requested startup state, as defined via the application state builder.</param>
        /// <returns>The state to use. Must have been registered at startup!</returns>
        protected virtual string GetStartupState(string requestedState)
        {
            return requestedState;
        }

        protected virtual bool HandleEvent(EventData evt)
        {
            return false;
        }
    }

    public class ApplicationStateChangedEventArgs
    {
        public ApplicationStateChangedEventArgs(string oldState, string nextState)
        {
            PreviousState = oldState;
            CurrentState = nextState;
        }

        public string PreviousState { get; private set; }
        public string CurrentState { get; private set; }
    }

    public class ApplicationStateChangingEventArgs
        : EventArgs
    {
        public ApplicationStateChangingEventArgs(string currentState, string nextState)
        {
            CurrentState = currentState;
            RequestedNextState = nextState;
            Cancel = false;
        }

        /// <summary>
        /// Current applicatoin state.
        /// </summary>
        public string CurrentState { get; private set; }
        /// <summary>
        /// The new state, as requested.
        /// </summary>
        public string RequestedNextState { get; private set; }
        /// <summary>
        /// Set to true to cancel (abort) the state change.
        /// </summary>
        public bool Cancel { get; set; }
    }

    internal class ApplicationStateBuilder
        : IApplicationStateBuilder
    {
        public ApplicationStateBuilder()
        {
        }

        internal readonly Dictionary<string, object> _States = new Dictionary<string, object>();

        private string _StartupState = string.Empty;

        public IApplicationStateBuilder AddState(string newStateKey, Action<IApplicationStateConfigBuilder> config)
        {
            if (_States.ContainsKey(newStateKey))
                throw new InvalidOperationException($"Cannot re-register same state key: {newStateKey} already registered!");
            _States.Add(newStateKey, new object());
            if(_States.Count == 1)
                _StartupState = newStateKey;

            ApplicationStateConfigBuilder b = new ApplicationStateConfigBuilder(this);
            config(b);

            return this;
        }

        public string StartupState { get => _StartupState;  }

        public IApplicationStateBuilder HasStartupState(string stateKey)
        {
            if (!_States.ContainsKey(stateKey)) 
                throw new InvalidOperationException($"The state {stateKey} is not registered!"); 
            _StartupState = stateKey;
            return this;
        }

        public IApplicationStateBuilder HasGlobalCommands(params string[] commandKey)
        {
            return this;
        }
    }

    internal class ApplicationStateConfigBuilder
        : IApplicationStateConfigBuilder
    {
        private ApplicationStateBuilder parent;
        private List<Action>? EntryActions = null;
        private List<Action>? ExitActions = null;

        public ApplicationStateConfigBuilder(ApplicationStateBuilder applicationStateBuilder)
        {
            parent = applicationStateBuilder;
        }

        public IApplicationStateConfigBuilder HasCommands(params string[] commandKey)
        {
            return this;
        }

        public IApplicationStateConfigBuilder OnEnter(Action runThis)
        {
            EntryActions ??= new List<Action>();
            EntryActions.Add(runThis);
            return this;
        }

        public IApplicationStateConfigBuilder OnLeave(Action runThis)
        {
            ExitActions ??= new List<Action>();
            ExitActions.Add(runThis);
            return this;
        }
    }
}