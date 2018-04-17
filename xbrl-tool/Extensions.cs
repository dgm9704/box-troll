namespace XbrlTool
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;

    public static class Extensions
    {
        private static XNamespace xbrli = "http://www.xbrl.org/2003/instance";
        private static XNamespace metric = "http://www.eba.europa.eu/xbrl/crr/dict/met";

        public static string Join(this IEnumerable<string> values, string separator)
        => string.Join(separator, values);

        public static bool IsNullOrEmpty(this string value)
        => string.IsNullOrEmpty(value);

        public static string Id(this XElement element)
        => element?.Attribute("id")?.Value
        ?? string.Empty;

        public static string Context(this XElement element)
        => element.Attribute("contextRef")?.Value
        ?? string.Empty;

        public static IEnumerable<XElement> ElementsByContext(this XDocument document, string contextId)
        => document.Root.
            Elements().
            Where(e => e.Context() == contextId);

        public static IEnumerable<XElement> Metrics(this XDocument document)
        => document.Root.
            Elements().
            Where(e => e.Name.Namespace == metric);

        public static IEnumerable<XElement> Units(this XDocument document)
        => document.Root.
            Elements(xbrli + "unit");

        public static IEnumerable<XElement> Contexts(this XDocument document)
        => document.Root.
            Elements(xbrli + "context");
    }
}