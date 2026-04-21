using System;

namespace CommandLineUtils.Visuals
{
    public interface IApplicationStateConfigBuilder
    {
        public IApplicationStateConfigBuilder OnEnter(Action runThis);
        public IApplicationStateConfigBuilder OnLeave(Action runThis);
        public IApplicationStateConfigBuilder HasCommands(params string[] commandKey);
    }
}