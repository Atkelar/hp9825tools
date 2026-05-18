using System;
using System.Xml;

namespace HP9825CPU
{
    /// <summary>
    /// Some helper functions, mostly extensions for the serialization state saving process via the XML DOM...
    /// </summary>
    public static class Utils
    {
        public static void SetAttribute(this XmlElement target, string name, int? value)
        {
            if (value.HasValue)
                target.SetAttribute(name, value.Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
            else
                target.RemoveAttribute(name);
        }
        public static void SetAttribute(this XmlElement target, string name, double? value)
        {
            if (value.HasValue)
                target.SetAttribute(name, value.Value.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
            else
                target.RemoveAttribute(name);
        }
        public static void SetAttribute(this XmlElement target, string name, TimeSpan? value)
        {
            if (value.HasValue)
                target.SetAttribute(name, "T-" + value.Value.TotalMicroseconds.ToString(global::System.Globalization.CultureInfo.InvariantCulture));
            else
                target.RemoveAttribute(name);
        }
        public static void SetAttribute(this XmlElement target, string name, DateTime? value)
        {
            if (value.HasValue)
                target.SetAttribute(name, value.Value.ToString("O"));
            else
                target.RemoveAttribute(name);
        }
        public static void SetAttribute(this XmlElement target, string name, bool? value)
        {
            if (value.HasValue)
                target.SetAttribute(name, value.Value ? "true" : "false");
            else
                target.RemoveAttribute(name);
        }

        public static void SetAttribute<T>(this XmlElement target, string name, T value) where T : System.Enum
        {
            target.SetAttribute(name, value.ToString());
        }
    }
}