namespace XbrlTool
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;

    class Program
    {

        private static Dictionary<string, Action<string>> Actions =
            new Dictionary<string, Action<string>>()
            {
                ["show-orphans"] = ShowOrphans,
                ["show-duplicates"] = ShowDuplicates,
                ["remove-orphans"] = RemoveOrphans,
                ["remove-duplicates"] = RemoveDuplicates,
            };

        private static XNamespace xbrli = "http://www.xbrl.org/2003/instance";

        static void Main(string[] args)
        {
            if (Actions.TryGetValue(args.First(), out var action))
                action(args.Last());
            else
                Console.WriteLine($"fdc\navailable actions:\n{Actions.Keys.Join("\n")}");
        }

        private static void ShowOrphans(string file)
        {
            var document = XDocument.Load(file);

            var used = new HashSet<string>(
                document.Root.Descendants().
                Select(e => e.Attribute("contextRef")?.Value).
                Where(r => !string.IsNullOrEmpty(r)));

            var orphanIds = document.Root.
                Elements(xbrli + "context").
                Select(c => c.Attribute("id")?.Value).
                Where(id => !used.Contains(id));

            foreach (var orphanId in orphanIds)
            {
                Console.WriteLine(orphanId);
            }
        }

        private static void RemoveOrphans(string file)
        {
            var document = XDocument.Load(file);

            var used = new HashSet<string>(
                document.Root.Descendants().
                Select(e => e.Attribute("contextRef")?.Value).
                Where(r => !string.IsNullOrEmpty(r)));

            document.Root.
                Elements(xbrli + "context").
                Where(e => !used.Contains(e.Attribute("id")?.Value)).
                Remove();

            document.Save($"{file}.clean");
        }

        private static void RemoveDuplicates(string file)
        {
            var document = XDocument.Load(file);

            var duplicates = document.Root.
                Elements(xbrli + "context").
                GroupBy(s => GetScenarioComparisonValue(s.Element(xbrli + "scenario"))).
                Where(g => g.Count() > 1);

            foreach (var duplicate in duplicates)
            {
                var id = duplicate.First().Attribute("id")?.Value;
                foreach (var d in duplicate.Skip(1))
                {
                    var duplicateId = d.Attribute("id").Value;
                    foreach (var f in document.Root.Elements().Where(e => e.Attribute("contextRef")?.Value == duplicateId))
                        f.SetAttributeValue("contextRef", id);
                    d.Remove();
                }

            }

            document.Save($"{file}.clean");
        }

        private static string GetScenarioComparisonValue(XElement element)
            => element == null
                ? string.Empty
                : element.
                    Elements().
                    OrderBy(e => e.Attribute("dimension").Value).
                    Select(e => MemberComparisonValue(e)).
                    Join(",");

        private static string MemberComparisonValue(XElement e)
            => $"{e.Attribute("dimension").Value.Split(':').Last()}={e.Value}";

        private static void ShowDuplicates(string file)
        {
            var document = XDocument.Load(file);

            var duplicates = document.Root.
                Descendants(xbrli + "scenario").
                GroupBy(s => s.ToString()).
                Where(g => g.Count() > 1);

            foreach (var duplicate in duplicates)
            {
                Console.WriteLine(duplicate.Select(d => d.Parent.Attribute("id").Value).Join(", "));
                Console.WriteLine(duplicate.Key);
            }
        }
    }
}