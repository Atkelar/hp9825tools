using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace CommandLineUtils
{
    public class ParameterHandler
    {
        public ParameterHandler(string commandName, bool ignoreCase = true, bool suppressSystemArgs = false)
        {
            CommandName = commandName;
            IgnoreCase = ignoreCase;
            // add system parameters...
            if (!suppressSystemArgs)
            {
                var thisProp = this.GetType().GetProperty(nameof(Verbosity));
                var pMap = new ParamReg() 
                { 
                    Definition = new ArgumentAttribute("v", "verbose") { DefaultValue = "normal", HelpText = "Set the verbosity of the command." }, 
                    LongName = "verbose", 
                    ShortName = "v", 
                    Positional = null, 
                    Property = thisProp, 
                    Target = this, 
                    Prefix = string.Empty, 
                    Validators = Array.Empty<ValidationAttribute>(),
                    IsList = false,
                    PropType = typeof(VerbosityLevel)
                };
                All.Add(pMap);
                this.ShortNames.Add("v", pMap);
                this.LongNames.Add("verbose", pMap);
            }
        }

        /// <summary>
        /// Provides the selected verbosity for the command exexution.
        /// </summary>
        public VerbosityLevel Verbosity { get; set; } = VerbosityLevel.Normal;

        /// <summary>
        /// True to allow the command host to react to the "help requested" flag, false to turn it into an error condition.
        /// </summary>
        public bool SupportsHelp { get; set; } = true;

        private string CommandName;
        private bool IgnoreCase;
        public T AddOptions<T>(string? prefix = null, T? overrideDefaults = null)
            where T : class, new()
        {
            T result = new T();

            IndexProps(typeof(T), prefix, result);

            if (overrideDefaults != null)
                CopyProps(typeof(T), overrideDefaults, result);
            _Registry.Add((Prefix: prefix, Props: result));
            return result;
        }

        /// <summary>
        /// Adds a file to read before the actual command line parsing takes place.
        /// </summary>
        /// <param name="path">The filename.</param>
        public void AddOptionalDefault(string path)
        {
            _OptionalDefaults ??= new List<string>();
            _OptionalDefaults.Add(PathUtils.ExpandPath(path));
        }

        private List<string>? _OptionalDefaults = null;

        /// <summary>
        /// Loads values from a text file.
        /// </summary>
        /// <param name="filename">The filename to read...</param>
        /// <param name="sectionNames">The section(s) of the file to read. Useful for multi-command executables. Note: the non-section (pre-section header) will always be read!</param>
        /// <returns>True if the file has been read, false if not.</returns>
        /// <exception cref="InvalidOperationException">The file had some serious errors...</exception>
        public async System.Threading.Tasks.Task<bool> LoadFrom(string filename, params string[] sectionNames)
        {
            if (!System.IO.File.Exists(filename))
            {
                return false;
            }
            using (var f = System.IO.File.OpenText(filename))
            {
                string? section = null; // non-section is active...
                string? line;
                int lineNumber = 0;
                while ((line = await f.ReadLineAsync()) != null)
                {
                    lineNumber++;
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                        continue;
                    if (line.StartsWith('[') && line.EndsWith(']'))
                    {
                        // new section!
                        section = line.Substring(1, line.Length - 2).Trim();
                        if (IgnoreCase)
                            section = section.ToLowerInvariant();
                        continue;   // ignore the section line...
                    }
                    // check if we should listen to the section...
                    if (section != null)
                    {
                        if (!sectionNames.Any(x=>x.Equals(section, StringComparison.InvariantCultureIgnoreCase)))
                            continue;   // ignore line...
                    }

                    int idx = line.IndexOf('=');
                    string pName;
                    string? pValue = null;
                    if (idx < 0)
                    {
                        // must be a name only...
                        if (IgnoreCase)
                            pName = line.ToLowerInvariant();
                        else
                            pName = line;
                    }
                    else
                    {
                        if (IgnoreCase)
                            pName = line.Substring(0, idx).Trim().ToLowerInvariant();
                        else
                            pName = line.Substring(0, idx).Trim();
                        pValue = line.Substring(idx + 1).Trim();
                        if (pValue.StartsWith('"'))
                        {
                            if (!pValue.EndsWith('"') || pValue.Length < 2)
                                throw new InvalidOperationException($"Line {lineNumber} in {filename} malformed: String is missing terminator: {pValue}");
                            pValue = HandleQuotedString(pValue.Substring(1, pValue.Length - 2));
                        }
                    }
                    // find parameter...
                    var par = All
                        .Where(x => x.LongName.Equals(pName, IgnoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture) || (x.ShortName != null && x.ShortName.Equals(pName, IgnoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture)))
                        .FirstOrDefault();
                    if (par == null)
                    {
                        throw new InvalidOperationException($"Configured value {pName} in file {filename}, line {lineNumber}{(section == null ? "" : $", Section {section}")} not found!");
                    }
                    if (par.HasValue)
                    {
                        if (pValue == null)
                            throw new InvalidOperationException($"Configured value {pName} in file {filename}, line {lineNumber}{(section == null ? "" : $", Section {section}")} requires a value but none was provided!");
                        par.SetValue(pValue);
                    }
                    else
                        par.SetSwitchPresent();
                }
            }
            return true;
        }

        private static string HandleQuotedString(string input)
        {
            return input.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        public async System.Threading.Tasks.Task ParseFrom(string[] args, params string[] configSections)
        {
            if(_OptionalDefaults !=null)
            {
                foreach(var fn in _OptionalDefaults)
                {
                    try
                    {
                        await LoadFrom(fn, configSections);
                    }
                    catch (ArgumentParsingException ex)
                    {
                        throw ReturnCode.ParseError.Happened(ex.ParameterName, string.Format("{0} (in defaults file {1})", ex.Message, fn));
                    }
                }
            }

            // prepare lists...
            Positional = Positional.OrderBy(x => x.Positional!.Value).ToList();

            int i = 0;
            while (i < args.Length)
            {
                string name;
                if (args[i].StartsWith('@'))
                {
                    var fn = args[i].Substring(1);
                    try
                    {
                        var b = await LoadFrom(fn, configSections);
                        if (!b)
                            throw ReturnCode.SettingsFileNotFound.Happened(args[i]);
                    }
                    catch (ArgumentParsingException ex)
                    {
                        throw ReturnCode.ParseError.Happened(ex.ParameterName, string.Format("{0} (in settings file {1})", ex.Message, fn));
                    }
                    Console.WriteLine("Loaded settings from {0}...", fn);
                }
                else
                {
                    try
                    {
                        if (args[i].StartsWith("--") && args[i].Length > 2)
                        {
                            // long name or help
                            name = args[i].Substring(2);
                            if (IgnoreCase)
                                name = name.ToLowerInvariant();
                            if (name == "help")
                            {
                                if (i+1 <args.Length)
                                {
                                    i++;
                                    HelpRequested = args[i];
                                }
                                else
                                    HelpRequested = string.Empty;
                            }
                            else
                            {
                                if (!LongNames.TryGetValue(name, out var pp))
                                    throw ReturnCode.ParseError.Happened(args[i], "Parameter name not found!");
                                if (pp.HasBeenSet)
                                    throw ReturnCode.ParseError.Happened(args[i], "Parameter defined multiple times!");
                                if (pp.HasValue)
                                {
                                    i++;
                                    if (i >= args.Length)
                                        throw ReturnCode.ParseError.Happened(args[i-1], "Parameter is missing the value!");
                                    pp.SetValue(args[i]);
                                }
                                else
                                {
                                    pp.SetSwitchPresent();
                                }
                            }
                        }
                        else
                        {
                            if (args[i].StartsWith('-') && args[i].Length > 1)
                            {
                                // short name...
                                name = args[i].Substring(1);
                                if (IgnoreCase)
                                    name = name.ToLowerInvariant();
                                if (name == "?")
                                {
                                    HelpRequested = string.Empty;
                                }
                                else
                                {
                                    if (!ShortNames.TryGetValue(name, out var pp))
                                        throw ReturnCode.ParseError.Happened(args[i], "Parameter was not found!");
                                    if (pp.HasBeenSet && !pp.IsList)
                                        throw ReturnCode.ParseError.Happened(args[i], "Parameter was defined multiple times!");
                                    if (pp.HasValue)
                                    {
                                        i++;
                                        if (i >= args.Length)
                                            throw ReturnCode.ParseError.Happened(args[i-1], "Parameter is missing the value!");
                                        pp.SetValue(args[i]);
                                    }
                                    else
                                    {
                                        pp.SetSwitchPresent();
                                    }
                                }
                            }
                            else
                            {
                                // positional...
                                var p = Positional.FirstOrDefault(x => !x.HasBeenSet);
                                if (p == null)
                                    throw ReturnCode.ParseError.Happened(args[i], "Parameter has no matching positional placeholder!");
                                p.SetValue(args[i]);
                            }
                        }
                    }
                    catch (ArgumentParsingException ex)
                    {
                        throw ReturnCode.ParseError.Happened(ex.ParameterName, ex.Message);
                    }
                }
                i++;
            }

            if (HelpRequested != null)
                return;

            bool ok = true;
            StringBuilder sb = new StringBuilder();
            foreach (var a in All)
            {
                if (a.Definition.Required && !a.HasBeenSet)
                {
                    if (sb.Length > 0)
                        sb.Append(", ");
                    sb.Append(a.Definition.LongName);
                    ok = false;
                }
            }
            ParsedOk = ok;
            if (sb.Length > 0)
                throw ReturnCode.ParseError.Happened(sb.ToString(), "Required parameters are missing!");
        }

        public void WriteHelpText(OutputHandlerBase target, string? commandPrefix = null)
        {
            target.WriteLine(VerbosityLevel.Normal);
            target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, "Call syntax: ");
            using(target.Indent(VerbosityLevel.Normal, 1))
            {
                target.WriteLine(VerbosityLevel.Normal);
           
                if (commandPrefix == null)
                {
                    target.Write(VerbosityLevel.Normal, SplitMode.None, CommandName);
                    target.WriteLine(VerbosityLevel.Normal, SplitMode.None, " help");
                    using(target.Indent( VerbosityLevel.Normal, 4))
                        target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, "Show complete help info.");
                    target.WriteLine(VerbosityLevel.Normal);
                }
                using(target.IndentFor(VerbosityLevel.Normal, CommandName))
                {
                    if (commandPrefix != null)
                    {
                        target.Write(VerbosityLevel.Normal, SplitMode.None, " " + commandPrefix);
                    }
                    foreach (var p in Positional)
                    {
                        target.Write(VerbosityLevel.Normal, SplitMode.None, " ");
                        if (!p.Definition.Required)
                            target.Write(VerbosityLevel.Normal, SplitMode.None, $" [{p.LongName}]");
                        else
                            target.Write(VerbosityLevel.Normal, SplitMode.None, $" {p.LongName}");
                    }

                    foreach (var p in All.Where(x => !x.Positional.HasValue).OrderBy(x => x.LongName))
                    {
                        string? value = p.GetCallSyntaxValueString();
                        value = value != null ? " " + value : null;
                        if (!p.Definition.Required)
                            target.Write(VerbosityLevel.Normal, SplitMode.None, $" [--{p.LongName}{value}]");
                        else
                            target.Write(VerbosityLevel.Normal, SplitMode.None, $" {p.LongName}{value}");
                        if (p.IsList)
                            target.Write(VerbosityLevel.Normal, SplitMode.None, " [,...]");
                    }
                }
            }
            target.WriteLine(VerbosityLevel.Normal);
            if (HelpRequested != null)
            {
                target.WriteLine(VerbosityLevel.Normal);
                target.WriteLine(VerbosityLevel.Normal, SplitMode.None, "Details:");
                target.WriteLine(VerbosityLevel.Normal);
                if (Positional.Any())
                {
                    int maxLen = Positional.Max(x => x.LongName.Length);

                    // append details...
                    foreach (var p in Positional)
                    {
                        using (target.IndentFor(VerbosityLevel.Normal, " " + p.LongName, maxLen + 3))
                        {
                            target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, $"{(p.Definition.Required ? "mandatory, " : "")}default: {p.Definition.DefaultValue ?? "<none>"}{(p.ShortName != null ? ", alias: " + p.ShortName : null)}");
                            target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, p.Definition.HelpText ?? "Sorry, no details provided in definition.");
                            var x = p.GetPrefixHelpString();
                            if (x != null)
                                target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, x);
                            target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, p.GetValueHelpString());
                            var samples = p.GetSampleValueStrings();
                            if (samples.Length > 0)
                            {
                                using (target.IndentFor(VerbosityLevel.Normal, "Example values: ", 2))
                                {
                                    bool first = true;
                                    foreach (var v in samples)
                                    {
                                        if (!first)
                                            target.Write(VerbosityLevel.Normal, SplitMode.None, ", ");
                                        target.Write(VerbosityLevel.Normal, SplitMode.None, v);
                                        first = false;
                                    }
                                    target.WriteLine(VerbosityLevel.Normal);
                                }
                            }
                            target.WriteLine(VerbosityLevel.Normal);
                        }
                    }
                }
                if (All.Any(x=>!x.Positional.HasValue))
                {
                    int maxLen = All.Where(x=>!x.Positional.HasValue).Max(x => x.LongName.Length);

                    // append details...
                    foreach (var p in All.Where(x=>!x.Positional.HasValue).OrderBy(x=>x.Prefix).ThenBy(x=>x.LongName))
                    {
                        using (target.IndentFor(VerbosityLevel.Normal, " " + p.LongName, maxLen + 3))
                        {
                            target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, $"{(p.Definition.Required ? "mandatory, " : "")}default: {p.Definition.DefaultValue ?? "<none>"}{(p.ShortName != null ? ", alias: " + p.ShortName : null)}");
                            target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, p.Definition.HelpText ?? "<no details>");
                            var x = p.GetPrefixHelpString();
                            if (x != null)
                                target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, x);
                            target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, p.GetValueHelpString());
                            var samples = p.GetSampleValueStrings();
                            if (samples.Length > 0)
                            {
                                using (target.IndentFor(VerbosityLevel.Normal, "Example values: ", 2))
                                {
                                    bool first = true;
                                    foreach (var v in samples)
                                    {
                                        if (!first)
                                            target.Write(VerbosityLevel.Normal, SplitMode.None, ", ");
                                        target.Write(VerbosityLevel.Normal, SplitMode.None, v);
                                        first = false;
                                    }
                                    target.WriteLine(VerbosityLevel.Normal);
                                }
                            }
                            target.WriteLine(VerbosityLevel.Normal);
                        }
                    }
                }
            }
            if (_OptionalDefaults != null && _OptionalDefaults.Count > 0)
            {
                target.WriteLine(VerbosityLevel.Normal);
                target.WriteLine(VerbosityLevel.Normal, SplitMode.None, "The command will look for the following configuration files, in order:");
                using(target.Indent(VerbosityLevel.Normal))
                {
                    foreach(var p in _OptionalDefaults)
                    {
                        target.WriteLine(VerbosityLevel.Normal, SplitMode.None, p);
                    }
                }
                target.WriteLine(VerbosityLevel.Normal);
                target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, "The config files are in basic 'ini' file syntax: name=value; use # for comment lines. Double quotes can be used to encode whitespaces in values, double-double quotes to escape them inside a string. Sections [xy] are supported too, their use depends on the command in question. The non-section before the first section is always applied, the others only if the program calls for it. The files are read in order and later ones replace values from earlier ones.");
                target.WriteLine(VerbosityLevel.Normal);
            }
        }

        public bool ParsedOk { get; private set; }

        public string? HelpRequested { get; internal set; }

        private class ParamReg
        {
            public ArgumentAttribute Definition { get; set; }
            public PropertyInfo Property { get; set; }
            public object Target { get; set; }
            public string? ShortName { get; set; }
            public string LongName { get; set; }
            public string Prefix { get; set; }
            public int? Positional { get; set; }
            public bool HasBeenSet { get; set; }

            public bool IsList {get; set;}

            public bool HasValue { get => Property.PropertyType != typeof(bool) || Definition.NoSwitch; }
            public ValidationAttribute[] Validators { get; set; } = Array.Empty<ValidationAttribute>();
            public Type PropType { get; internal set; }

            private static Dictionary<Type, string> _HelpStrings = new Dictionary<Type, string>()
            {
                {typeof (string), "A string."},
                {typeof(int), "A numeric integer value. It can have suffixes h/o/b added to indicate hex, oct and bin numbers."},
                {typeof(long), "A numeric integer value. It can have suffixes h/o/b added to indicate hex, oct and bin numbers."},
                {typeof(int?), "A numeric integer value. It can have suffixes h/o/b added to indicate hex, oct and bin numbers."},
                {typeof(long?), "A numeric integer value. It can have suffixes h/o/b added to indicate hex, oct and bin numbers."},
                {typeof(bool), "An on/off indicator. Can accept on, true, yes for on, or off, false, no for off." },
                {typeof(bool?), "An on/off/maybe indicator. Can accept on, true, yes, + for on, or off, false, no, - for off as well as maybe, semi, unknown, ? for maybe." },
                {typeof(double), "A floating point value."},
                {typeof(double?), "A floating point value."},
            };

            private static Dictionary<Type, string> _SyntaxStrings = new Dictionary<Type, string>()
            {
                {typeof(string), "AValue`'A value with spaces'"},
                {typeof(int), "0`123`-123`1Ah`177o`111000b" },
                {typeof(long), "0`123`-123`1Ah`177o`111000b"},
                {typeof(int?), "0`123`-123`1Ah`177o`111000b"},
                {typeof(long?), "0`123`-123`1Ah`177o`111000b"},
                {typeof(bool), "{yes|no|on|off|true|false|+|-}" },
                {typeof(bool?), "{yes|no|on|off|true|false|maybe|unknown|semi|+|-|x}" },
                {typeof(double), "0`-123`1.23`-12.3`1.23e-3" },
                {typeof(double?), "0`123`-123`1Ah`177o`111000b" },
            };

            public string[] GetSampleValueStrings()
            {
                if (!HasValue)
                {
                    return new string[] {"No value may be provided for this!"};
                }

                if (Definition.Syntax != null)
                    return Definition.Syntax.Split('`', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (_SyntaxStrings.TryGetValue(PropType, out var s))
                    return s.Split('`', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                if (PropType.IsEnum)
                {
                    // special case...
                    return PropType.GetEnumNames();
                }

                return new string[] {"?"};
            }

            public string? GetCallSyntaxValueString()
            {
                if (!HasValue)
                    return null;
                if (PropType.IsEnum)
                {
                    // special case...
                    return $"{{{string.Join('|', PropType.GetEnumNames())}}}";
                }
                return GetSampleValueStrings().FirstOrDefault();
            }

            public string GetValueHelpString()
            {
                if (!HasValue)
                {
                    return "This argument is a switch: it accepts no value.";
                }

                if (_HelpStrings.TryGetValue(PropType, out var s))
                    return s;
                
                if (PropType.IsEnum)
                {
                    StringBuilder sb = new StringBuilder();
                        sb.AppendLine("This is an enumerated set of values. Possible values are:");
                    foreach(var x in PropType.GetMembers(BindingFlags.Static | BindingFlags.Public))
                    {
                        sb.Append("  ");
                        sb.Append(x.Name);
                        var e = x.GetCustomAttributes<AliasNamesAttribute>();
                        bool first = true;
                        foreach(var e1 in e)
                        {
                            foreach(var als in e1.GetAliases())
                            {
                                if (first)
                                    sb.Append(" alias: ");
                                else
                                    sb.Append(", ");
                                first = false;
                                sb.Append(als);
                            }
                        }
                        sb.AppendLine();
                    }
                    return sb.ToString();
                }

                return "Unknown type?!";
            }

            public string? GetPrefixHelpString()
            {
                if (IsList)
                    return "This argument supports multiple values; it can be provided multiple times.";
                return null;
            }

            public void SetSwitchPresent()
            {
                HasBeenSet = true;
                Property.SetValue(Target, true);
            }

            private static readonly Dictionary<Type, Func<ParamReg, string, object?>> _Parsers = new Dictionary<Type, Func<ParamReg,string, object?>>()
            {
                { typeof(long), (p,x) => p.ParseLong(x) },
                { typeof(long?), (p,x) => p.ParseLong(x) },
                { typeof(int), (p,x) => p.ParseInt(x) },
                { typeof(int?), (p,x) => p.ParseInt(x) },
                { typeof(string), (p,x) => x},
                { typeof(double), (p,x) => double.Parse(x, CultureInfo.InvariantCulture)},
                { typeof(double?), (p,x) => double.Parse(x, CultureInfo.InvariantCulture)},
                { typeof(bool), (p,x) => p.ParseBool(x) },
                { typeof(bool?), (p,x) => p.ParseMaybeBool(x) }
            };

            private bool ParseBool(string input)
            {
                var b = ParseMaybeBool(input);
                if (!b.HasValue)
                    throw ParseException("The parameter doesn't support 'undefined' values: {0}", input);
                return b.Value;
            }
            private bool? ParseMaybeBool(string input)
            {
                if(_BoolValues.TryGetValue(input.ToLowerInvariant(), out var b))
                    return b;
                throw ParseException("The boolean value couldn't be parsed as a boolean: {0}", input);
            }

            private static Dictionary<string, bool?> _BoolValues = new Dictionary<string, bool?>()
            {
                {"+", true},
                {"true", true},
                {"on", true},
                {"yes", true},
                {"-", false},
                {"false", false},
                {"off", false},
                {"no", false},
                {"maybe", null},
                {"semi", null},
                {"unknown", null},
                {"?", null},
            };

            public void SetValue(string value)
            {
                HasBeenSet = true;
                object? parsedValue = null;

                if (_Parsers.TryGetValue(PropType, out var parser))
                {
                    parsedValue = parser(this, value);
                }
                else
                {
                    if (PropType.IsEnum)
                    {
                        // got to handle enums special...
                        if (PropType.GetEnumNames().Any(x=>x.Equals(value, StringComparison.InvariantCultureIgnoreCase)))
                        {
                            parsedValue = System.Enum.Parse(PropType, value, true);
                        }
                        else
                        {
                            foreach(var e1 in PropType.GetMembers(BindingFlags.Static | BindingFlags.Public))
                            {
                                foreach(var e2 in e1.GetCustomAttributes<AliasNamesAttribute>())
                                {
                                    if (e2.IsMatch(value, true))
                                    {
                                        parsedValue = System.Enum.Parse(PropType, e1.Name, true);
                                        break;
                                    }
                                }
                                if (parsedValue != null)
                                    break;
                            }
                        }
                        if (parsedValue == null)    
                            throw ParseException("Enumerated value {0} not found for {1}", value, Definition.LongName);
                        
                    }
                    else
                        throw new NotImplementedException($"The property type {PropType.FullName} is not implemented as parameter parser!");
                }

                foreach(var v in Validators)
                {
                    try
                    {
                        v.Validate(parsedValue, Definition.LongName);
                    }
                    catch(Exception ex)
                    {
                        throw ParseException("Validation failed: value {0}: {1}", parsedValue, ex.Message);
                    }
                }

                // if we get here, the parameter value was within spec.
                if (!IsList)
                {
                    Property.SetValue(Target, parsedValue);
                }
                else
                    AddListEntry(PropType, Property, Target, parsedValue);
            }

            private void AddListEntry(Type propType, PropertyInfo property, object target, object? parsedValue)
            {
                var tList = typeof(List<>).MakeGenericType(propType);
                object? val = property.GetValue(target);
                if (val == null)
                {
                    val = Activator.CreateInstance(tList);
                    property.SetValue(target, val);
                }
                else
                {
                    if (val.GetType() != tList)
                    {
                        val = Activator.CreateInstance(tList, false, val);  // replace with copy!
                        property.SetValue(target, val);
                    }
                }
                if (parsedValue != null)
                {
                    tList.GetMethod("Add", new Type[] {propType})!.Invoke(val, new object?[] { parsedValue });
                }
                else
                    tList.GetMethod("Clear")!.Invoke(target, Array.Empty<object?>());
                // Console.WriteLine("List: {0}, Prop: {2} value: {1}", tList.FullName, parsedValue, propType.FullName);
            }

            internal long ParseLong(string value)
            {
                try
                {
                if (value.EndsWith("o", StringComparison.InvariantCultureIgnoreCase))
                {
                    // octal!
                    return Convert.ToInt64(value.Substring(0,value.Length-1), 8);
                }
                else
                    if (value.EndsWith("h", StringComparison.InvariantCultureIgnoreCase))
                    {
                        // hex...
                        return Convert.ToInt64(value.Substring(0,value.Length-1), 16);
                    }
                    else
                        if (value.EndsWith("b", StringComparison.InvariantCultureIgnoreCase))
                        {
                            // binary
                            return Convert.ToInt64(value.Substring(0,value.Length-1), 2);
                        }
                        else
                        {
                            // decimal.
                            return Convert.ToInt64(value, 10);
                        }
                }
                catch (Exception ex)
                {
                    throw ParseException("Couldn't convert the provided value of '{0}' into an integer value: {1}", value, ex.Message);
                }
            }

            private Exception ParseException(string message, params object?[] args)
            {
                return new ArgumentParsingException(Definition.LongName, string.Format(message, args));
            }

            private int ParseInt(string value)
            {
                long l = ParseLong(value);
                if (l < (long)int.MinValue || l > (long)int.MaxValue)
                    throw ParseException("Integer out of range: {0}", l);
                return (int)l;
            }

        }

        private class ArgumentParsingException 
            : Exception
        {
            public string ParameterName {get; private set;}

            public ArgumentParsingException(string longName, string message)
                : base(message)
            {
                this.ParameterName = longName;
            }
        }

        private List<ParamReg> All = new List<ParamReg>();
        private List<ParamReg> Positional = new List<ParamReg>();
        private Dictionary<string, ParamReg> LongNames = new Dictionary<string, ParamReg>();
        private Dictionary<string, ParamReg> ShortNames = new Dictionary<string, ParamReg>();


        private void IndexProps(Type mappingFrom, string? prefix, object target)
        {
            int posBase = 0;

            if (Positional.Count > 0)
            {
                posBase = Positional.Max(x => x.Positional.GetValueOrDefault()) + 1;
            }

            foreach (var thisProp in mappingFrom.GetProperties())
            {
                if (!thisProp.CanWrite || !thisProp.CanRead)
                    continue;
                var ca = thisProp.GetCustomAttributes(typeof(ArgumentAttribute), true);
                if (ca.Length == 1)
                {
                    ArgumentAttribute attr = (ArgumentAttribute)ca[0];
                    int? position = null;
                    string? shortName = null;
                    string? longName = null;

                    if (attr.Positional > 0)
                    {
                        // positional parameter...
                        position = attr.Positional + posBase;
                    }
                    longName = prefix != null ? prefix + ":" + attr.LongName : attr.LongName;
                    if (attr.ShortName != null)
                        shortName = prefix != null ? prefix + ":" + attr.ShortName : attr.ShortName;
                    
                    Type pType = thisProp.PropertyType;
                    bool isList = false;
                    if (pType.IsInterface && pType.IsGenericType && pType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    {
                        pType = pType.GenericTypeArguments[0];
                        isList = true;
                    }

                    var valAttrs = thisProp.GetCustomAttributes<System.ComponentModel.DataAnnotations.ValidationAttribute>(true);

                    var pMap = new ParamReg() 
                    { 
                        Definition = attr, 
                        LongName = longName, 
                        ShortName = shortName, 
                        Positional = position, 
                        Property = thisProp, 
                        Target = target, 
                        Prefix = prefix ?? string.Empty, 
                        Validators = valAttrs.ToArray(),
                        IsList = isList,
                        PropType = pType
                    };
                    All.Add(pMap);
                    
                    if (IgnoreCase)
                    {
                        longName = longName.ToLowerInvariant();
                        shortName = shortName?.ToLowerInvariant();
                    }
                    if (position.HasValue)
                        Positional.Add(pMap);
                    LongNames.Add(longName, pMap);
                    if (shortName != null)
                        ShortNames.Add(shortName, pMap);
                }
            }
        }

        private void CopyProps(Type mappingFrom, object source, object target)
        {
            foreach (var p in mappingFrom.GetProperties())
            {
                if (!p.CanWrite || !p.CanRead)
                    continue;
                var ca = p.GetCustomAttributes(typeof(ArgumentAttribute), true);
                if (ca.Length == 1)
                {
                    // prop is present, copy over value...
                    object? o = p.GetValue(source);
                    p.SetValue(target, o);
                }
            }
        }

        private List<(string? Prefix, object Props)> _Registry = new List<(string Prefix, object Props)>();
    }
}