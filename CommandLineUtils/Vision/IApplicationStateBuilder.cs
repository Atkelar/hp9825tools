using System;

namespace CommandLineUtils.Visuals
{
    /// <summary>
    /// 
    /// </summary>
    public interface IApplicationStateBuilder
    {
        IApplicationStateBuilder AddState(string newStateKey, Action<IApplicationStateConfigBuilder> config);

        IApplicationStateBuilder HasStartupState(string stateKey);

        IApplicationStateBuilder HasGlobalCommands(params string[] commandKey);
    }
}