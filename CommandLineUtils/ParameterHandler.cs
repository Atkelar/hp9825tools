using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;

namespace CommandLineUtils
{
    public class ParameterHandler
    {
        public ParameterHandler(string commandName, bool ignoreCase = true)
        {
            CommandName = commandName;
            IgnoreCase = ignoreCase;
        }

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
        /// <param name="filename">The filename.</param>
        public void AddOptionalDefault(string filename)
        {
            _OptionalDefaults ??= new List<string>();
            _OptionalDefaults.Add(filename);
        }

        private List<string>? _OptionalDefaults = null;

        public async System.Threading.Tasks.Task<bool> LoadFrom(string filename)
        {
            if (!System.IO.File.Exists(filename))
            {
                return false;
            }
            using (var f = System.IO.File.OpenText(filename))
            {
                string? line;
                while ((line = await f.ReadLineAsync()) != null)
                {
                    line = line.Trim();
                    if (line.Length == 0 || line.StartsWith('#'))
                        continue;
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
                                throw new InvalidOperationException($"Line in {filename} malformed: String is missing terminator: {pValue}");
                            pValue = HandleQuotedString(pValue.Substring(1, pValue.Length - 2));
                        }
                    }
                    // find parameter...
                    var par = All
                        .Where(x => x.LongName.Equals(pName, IgnoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture) || (x.ShortName != null && x.ShortName.Equals(pName, IgnoreCase ? StringComparison.InvariantCultureIgnoreCase : StringComparison.InvariantCulture)))
                        .FirstOrDefault();
                    if (par == null)
                        throw new InvalidOperationException($"Configured value {pName} in file {filename} not found!");
                    if (par.HasValue)
                    {
                        if (pValue == null)
                            throw new InvalidOperationException($"Configured value {pName} in file {filename} requires a value but none was provided!");
                        par.SetValue(pValue);
                    }
                    else
                        par.SetNoValue();
                }
            }
            return true;
        }

        private static string HandleQuotedString(string input)
        {
            return input.Replace("\\n", "\n").Replace("\\\"", "\"").Replace("\\\\", "\\");
        }

        public async System.Threading.Tasks.Task ParseFrom(string[] args)
        {
            if(_OptionalDefaults !=null)
            {
                foreach(var fn in _OptionalDefaults)
                {
                    await LoadFrom(fn);
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
                    var b = await LoadFrom(fn);
                    if (!b)
                        throw ReturnCode.SettingsFileNotFound.Happened(args[i]);
                    Console.WriteLine("Loaded settings from {0}...", fn);
                }
                else
                {
                    if (args[i].StartsWith("--"))
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
                                throw ReturnCode.ParseError.Happened(args[i], "Paramaeter name not found!");
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
                                pp.SetNoValue();
                            }
                        }
                    }
                    else
                    {
                        if (args[i].StartsWith('-'))
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
                                if (pp.HasBeenSet)
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
                                    pp.SetNoValue();
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
            using(target.Indent( VerbosityLevel.Normal, 1))
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
                        string value = string.Empty;
                        if (p.Property.PropertyType == typeof(bool))
                        {
                            // bool, done. flag argument.
                        }
                        else
                        {
                            if (p.Property.PropertyType == typeof(int))
                            {
                                value = " 123";
                            }
                            else
                            {
                                value = " \"str\"";
                            }
                        }
                            if (!p.Definition.Required)
                                target.Write(VerbosityLevel.Normal, SplitMode.None, $" [--{p.LongName}{value}]");
                            else
                                target.Write(VerbosityLevel.Normal, SplitMode.None, $" {p.LongName}{value}");
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
                        using (target.IndentFor( VerbosityLevel.Normal, " " + p.LongName, maxLen + 3))
                        {
                            target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, $"{(p.Definition.Required ? "mandatory, " : "")}default: {p.Definition.DefaultValue ?? "<none>"}{(p.ShortName != null ? ", alias: " + p.ShortName : null)}");
                            target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, p.Definition.HelpText ?? "<no details>");
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
                        using (target.IndentFor( VerbosityLevel.Normal, " " + p.LongName, maxLen + 3))
                        {
                            target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, $"{(p.Definition.Required ? "mandatory, " : "")}default: {p.Definition.DefaultValue ?? "<none>"}{(p.ShortName != null ? ", alias: " + p.ShortName : null)}");
                            target.WriteLine(VerbosityLevel.Normal, SplitMode.Word, p.Definition.HelpText ?? "<no details>");
                            target.WriteLine(VerbosityLevel.Normal);
                        }
                    }
                }
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

            public bool HasValue { get => Property.PropertyType != typeof(bool); }

            public string GetValueHelpString()
            {
                if (!HasValue)
                {
                    return "This argument is a switch: it accepts no value.";
                }
                if (Property.PropertyType == typeof(string))
                {
                    return "This argument accepts a string.";
                }
                else
                {
                    if (Property.PropertyType == typeof(int))
                    {
                        return "This argument accepts a numeric value.";
                    }
                    else
                        return "??";
                }
            }

            public void SetNoValue()
            {
                HasBeenSet = true;
                Property.SetValue(Target, true);
            }

            public void SetValue(string value)
            {
                HasBeenSet = true;
                if (Property.PropertyType == typeof(int))
                {
                    Property.SetValue(Target, int.Parse(value));
                }
                else
                {
                    if (Property.PropertyType == typeof(string))
                    {
                        Property.SetValue(Target, value);
                    }
                    else
                        throw new NotImplementedException();
                }
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

            foreach (var p in mappingFrom.GetProperties())
            {
                if (!p.CanWrite || !p.CanRead)
                    continue;
                var ca = p.GetCustomAttributes(typeof(ArgumentAttribute), true);
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

                    var pMap = new ParamReg() { Definition = attr, LongName = longName, ShortName = shortName, Positional = position, Property = p, Target = target, Prefix = prefix ?? string.Empty };
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