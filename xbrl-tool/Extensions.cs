namespace XbrlTool
{
    using System.Collections.Generic;
    
    public static class  Extensions
    {
        public static string Join(this IEnumerable<string> values, string separator)
        => string.Join(separator, values);
    }
}