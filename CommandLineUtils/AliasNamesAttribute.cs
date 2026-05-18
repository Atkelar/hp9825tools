using System;
using System.Collections.Generic;

namespace CommandLineUtils
{
    /// <summary>
    /// Provide alias names for enum member parsing.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false, Inherited = false)]
    public class AliasNamesAttribute
        : Attribute
    {
        public AliasNamesAttribute(string alias, params string[] others)
        {
            _Aliases = new string[1 + others.Length];
            _Aliases[0]= alias;
            for(int i=0;i<others.Length;i++)
                _Aliases[i+1] = others[i];
        }

        private string[] _Aliases;

        public bool IsMatch(string input, bool ignoreCase)
        {
            foreach(var x in _Aliases)
            {
                if (x.Equals(input, ignoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture))
                    return true;
            }
            return false;
        }

        internal IEnumerable<string> GetAliases()
        {
            return _Aliases;
        }
    }
}