namespace XbrlTool
{
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;

    public static class Extensions
    {
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
    }
}