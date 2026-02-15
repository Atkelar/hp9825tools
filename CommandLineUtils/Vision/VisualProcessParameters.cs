namespace CommandLineUtils.Visuals
{
    public class VisualProcessParameters
    {
        [Argument("mono", "ForceMonochrome", HelpText = "Forces the app to use monochrome output mode. I.e. no colors at all.")]
        public bool ForceMonochrome {get;set;}

        [Argument("minsize", "MinimumSize", HelpText = "Specifies the minium 'virtual viewport' size of the application. Defaults to 40x15, format is 'width x height'.")]
        public string MinimumSize {get;set;} = "40x15";

        [Argument("maxsize", "MaximumSize", HelpText = "Specifies the maximum 'virtual viewport' size of the application. There is no default, which allows to grow up to the console window.")]
        public string? MaximumSize {get;set;}
    }
}