using System.Text;

namespace CommandLineUtils
{
    internal interface IHelpTextBuilder
    {
        void WriteHelpText(OutputHandlerBase target);
    }
}