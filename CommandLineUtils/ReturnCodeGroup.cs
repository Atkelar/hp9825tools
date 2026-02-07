using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CommandLineUtils
{

    /// <summary>
    /// Wraps one return code registration for a single enum/base code definition. Obtained via <see cref="ReturnCodeHandler.Register{T}(int, bool)"/> 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ReturnCodeGroup<T>
        : IHelpTextBuilder
        where T : System.Enum
    {
        internal ReturnCodeGroup(int baseCode)
        {
            BuildIndex(baseCode);
        }

        private void BuildIndex(int baseCode)
        {
            Type t = typeof(T);
            // we need: all the enum members
            foreach(var m in t.GetFields(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public))
            {
                var attrs = m.GetCustomAttributes(typeof(ReturnCodeAttribute), false);
                // ...ignore any without the attribute (could be the __value field...)
                if (attrs.Length == 1)
                {
                    ReturnCodeAttribute a = (ReturnCodeAttribute)attrs[0];
                    // add to the registry
                    T key = (T)m.GetValue(null)!;
                    int code = baseCode + Convert.ToInt32(key);
                    if (Index.ContainsKey(key))
                        throw new InvalidOperationException($"Duplicate value for return type {t.FullName}: {key} ({Convert.ToInt32(key)})");
                    Index.Add(key, new CodeRegistration()
                    {
                        Code = code, 
                        ErrorMessageTemplate = a.ErrorMessageTemplate,
                        HelpMessage = a.HelpMessage,
                        IsNonError = a.IsNonError
                    });
                }
            }

            if (Index.Count ==0 )
                throw new InvalidOperationException($"Cannot use type {t.FullName} as exit code. No members are tagged as ReturnCode!");
        }

        private Dictionary<T, CodeRegistration> Index = new Dictionary<T, CodeRegistration>();

        private class CodeRegistration
        {
            public int Code { get; set; }
            public bool IsNonError {get;set;}
            public string ErrorMessageTemplate { get; set; } = string.Empty;
            public string? HelpMessage { get; set; }
        }

        internal IEnumerable<int> GetAllCodes()
        {
            return Index.Values.Select(x=>x.Code);
        }
        
        /// <summary>
        /// Throws the result code exception for the provided code here and now.
        /// </summary>
        /// <param name="code">The error code, as per enum.</param>
        /// <param name="args">The parameters to add to the error message.</param>
        /// <exception cref="ReturnCodeException">At any case.</exception>
        public void Throw(T code, params object?[] args)
        {
            throw Happened(code, args);
        }

        /// <summary>
        /// Returns a ready-to-go exception based on the provided error code.
        /// </summary>
        /// <param name="code">The error code, as per enum.</param>
        /// <param name="args">The parameters to add to the error message.</param>
        /// <returns>The exception. Can be used to "thorw x.Happened(..)" to keep the compiler warnings at bay.</returns>
        public Exception Happened(T code, params object?[] args)
        {
            if (!Index.TryGetValue(code, out var reg))
                return new InvalidOperationException(string.Format("Tried to cause unregisetered exit code: {0} with {1} arguments in the message!", code, args.Length));
            // 
            string msg;
            try
            {
                msg = string.Format(reg.ErrorMessageTemplate, args);
            }
            catch (FormatException)
            {
                // assume we had too little args and retry with plenty... but only once.
                // this is a fallback, since the message format string and the use of that string 
                // are rather disconnected from one another and might diverge over time.
                object?[] alt = new object[Math.Max(args.Length* 2, 128)];
                Array.Copy(args, 0, alt, 0, args.Length);
                for(int i = args.Length; i<alt.Length;i++)
                    alt[i]= $"<missing ph: {i}>";
                msg = string.Format(reg.ErrorMessageTemplate, alt);
            }
            return new ReturnCodeException(reg.Code, msg, reg.IsNonError);
        }

        /// <summary>
        /// Get a wrapper object for a provided code.
        /// </summary>
        /// <param name="code">The code to retreive.</param>
        /// <returns>A wrapper object to keep around.</returns>
        /// <exception cref="InvalidOperationException">If the code is not found.</exception>
        public ReturnCode For(T code)
        {
            if (!Index.TryGetValue(code, out var reg))
                throw new InvalidOperationException(string.Format("Tried to retreive unregisetered exit code: {0}", code));

            return new ReturnCode(reg.Code, reg.IsNonError, reg.ErrorMessageTemplate, reg.HelpMessage);
        }

        void IHelpTextBuilder.WriteHelpText(OutputHandlerBase target)
        {
            foreach(var c in Index.Values.OrderBy(x=>x.Code))
            {
                using(target.IndentFor(VerbosityLevel.Normal, $"  {c.Code,3} - "))
                {
                    target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, c.HelpMessage ?? "<no details provided>");
                }
            }
        }

    }
}