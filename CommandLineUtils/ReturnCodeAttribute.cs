using System;
namespace CommandLineUtils
{
    /// <summary>
    /// Marks an enum member as an official return code.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class ReturnCodeAttribute
        : Attribute
    {
        /// <summary>
        /// Initializes a return code.
        /// </summary>
        /// <param name="errorMessageTemplate">The template string (see <see cref="string.Format(string, object?[])"/>) for the error message.</param>
        public ReturnCodeAttribute(string errorMessageTemplate)
        {
            ErrorMessageTemplate = errorMessageTemplate;
        }

        /// <summary>
        /// Set this to true if you want to treat the result code as "non error" code, i.e. "succes, but..." condition. Will suppress error output.
        /// </summary>
        public bool IsNonError { get; set; }

        /// <summary>
        /// Defines the - long text - help message for command line help generation.
        /// </summary>
        public string? HelpMessage { get; set; }

        /// <summary>
        /// The error message template (see <see cref="string.Format(string, object?[])"/>) for this error.
        /// </summary>
        public string ErrorMessageTemplate { get; private set; }
    }
}