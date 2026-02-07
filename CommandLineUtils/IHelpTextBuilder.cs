using System.Text;

namespace CommandLineUtils
{
    internal interface IHelpTextBuilder
    {
        void CreatHelpText(StringBuilder sb, int width);
    }
}