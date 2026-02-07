using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace CommandLineUtils
{
    /// <summary>
    /// Createas a list of posisble return codes for the main execution function of a CLI app.
    /// </summary>
    /// <remarks>
    /// <param>
    /// The registration and use of return codes via this handler class makes it possible to 
    /// list them in help output and also try/catch throw them and abort the code in an error condition
    /// without having to "return 0/if" all the time.
    /// </param>
    /// </remarks>
    public class ReturnCodeHandler
    {
        /// <summary>
        /// Create a new return code handler.
        /// </summary>
        public ReturnCodeHandler(bool includeParsingErorrs = true)
        {
            if (includeParsingErorrs)
                AddExisting(Success, ParseError, SettingsFileNotFound);
            else
                AddExisting(Success);
        }

        private void AddExisting(params ReturnCode[] codes)
        {
            foreach(var code in codes)
                _DefinedCodes.Add(code.Code);
            _HelpList.Add(new ObjectBasedHelpBuilder(codes));
        }

        private class ObjectBasedHelpBuilder
            : IHelpTextBuilder
        {
            public ObjectBasedHelpBuilder(ReturnCode[] input)
            {
                list = input;
            }

            private ReturnCode[] list;

            public void CreatHelpText(StringBuilder sb, int width)
            {
                foreach(var c in list)
                {
                    sb.AppendFormat("  {0} - {1}", c.Code, c.HelpMessage ?? "<no details provided>");
                    sb.AppendLine();
                }
                sb.AppendLine();
            }
        }

        /// <summary>
        /// Convenience property to access the <see cref="ReturnCode.Success"/> field.
        /// </summary>
        public ReturnCode Success => ReturnCode.Success;
        /// <summary>
        /// Convenience property to access the <see cref="ReturnCode.ParseError"/> field.
        /// </summary>
        public ReturnCode ParseError => ReturnCode.ParseError;
        /// <summary>
        /// Convenience property to access the <see cref="ReturnCode.SettingsFileNotFound"/> field.
        /// </summary>
        public ReturnCode SettingsFileNotFound = ReturnCode.SettingsFileNotFound;

        private List<int> _DefinedCodes = new List<int>();

        private List<IHelpTextBuilder> _HelpList = new List<IHelpTextBuilder>();

        /// <summary>
        /// Registers a return code enumeration.
        /// </summary>
        /// <typeparam name="T">The enum type; values are regisered as return codes, the </typeparam>
        /// <param name="baseCode"></param>
        /// <param name="ignoreDuplicates"></param>
        /// <returns></returns>
        public ReturnCodeGroup<T> Register<T>(int baseCode = 0,  bool ignoreDuplicates = false) 
            where T : System.Enum
        {
            var reg = new ReturnCodeGroup<T>(baseCode);
            if (!ignoreDuplicates)
            {
                foreach(var code in reg.GetAllCodes())
                {
                    if (_DefinedCodes.Contains(code))
                        throw new InvalidOperationException($"Duplicate return code {code} via {typeof(T).FullName}.");
                    _DefinedCodes.Add(code);
                }
            }
            else
            {
                _DefinedCodes.AddRange(reg.GetAllCodes());
            }
            _HelpList.Add(reg);
            return reg;
        }

        /// <summary>
        /// Create the full help text for the list of exit codes.
        /// </summary>
        /// <param name="width">The output format width.</param>
        /// <returns>The formatted text message.</returns>
        public string GetHelpText(int width = 80)
        {
            StringBuilder sb = new StringBuilder();
            {
                foreach(var c in _HelpList)
                    c.CreatHelpText(sb, width);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Helper function: Runs the provided callback and converts any <see cref="ReturnCodeException"/>s into the proper return code. Useful to make use of the exit code exceptions in non-hosted apps.
        /// </summary>
        /// <param name="callback">The code to execute.</param>
        /// <param name="catchAll">True converts any unhandled exceptions of other types into return code.</param>
        /// <param name="printErrors">True if the code should print any error messages.</param>
        /// <param name="printSuccess">True if the code should print any non-error messages.</param>
        /// <returns>The OS return code.</returns>
        public static async Task<int> Run(Func<Task> callback, bool catchAll = false, bool printErrors = true, bool printSuccess = false)
        {
            try
            {
                await callback();
            }
            catch (ReturnCodeException ex)
            {
                if (ex.IsNonError && printSuccess)
                    Console.WriteLine(ex.Message);
                if (!ex.IsNonError && printErrors)
                    Console.WriteLine(ex.Message);
                return ex.Code;
            }
            catch(Exception ex)
            {
                if (!catchAll)  // forward...
                    throw;
                if (printErrors)
                {
                    Console.WriteLine(ReturnCode.UhandledError.ErrorMessageTemplate, ex.Message);
                }
                return ReturnCode.UhandledError.Code;
            }
            return ReturnCode.Success.Code; 
        }
    }
}