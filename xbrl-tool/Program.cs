namespace XbrlTool
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Xml.Linq;

    class Program
    {

        private static XNamespace xbrli = "http://www.xbrl.org/2003/instance";

        private static Dictionary<string, Action<XDocument, TextWriter>> OutputActions =
            new Dictionary<string, Action<XDocument, TextWriter>>
            {
                ["show-unused-contexts"] = ShowUnusedContexts,
                ["show-unused-units"] = ShowUnusedUnits,
                ["show-duplicate-contexts"] = ShowDuplicateContexts,
                ["show-duplicate-units"] = ShowDuplicateUnits,
            };

        private static Dictionary<string, Action<XDocument>> DocumentActions =
            new Dictionary<string, Action<XDocument>>()
            {
                ["remove-unused-contexts"] = RemoveUnusedContexts,
                ["remove-unused-units"] = RemoveUnusedUnits,
                ["remove-duplicate-contexts"] = RemoveDuplicateContexts,
                ["remove-duplicate-units"] = RemoveDuplicateUnits,
            };

        private static Dictionary<string, Action<XDocument, HashSet<string>>> DocumentListActions =
            new Dictionary<string, Action<XDocument, HashSet<string>>>()
            {
                ["remove-list-datapoints"] = RemoveDatapoints,
            };

        private static void RemoveDatapoints(XDocument document, HashSet<string> list)
        => document.
            Metrics().
            Where(m => list.Contains($"{m.Name.LocalName} {m.Context()}")).
            Remove();

        static void Main(string[] args)
        {
            if (args.Length < 1 || args.Any(a => a.Equals("help", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"xbrl-tool\navailable actions:\n{OutputActions.Keys.Concat(DocumentActions.Keys).Concat(DocumentListActions.Keys).Join("\n")}");
                return;
            }

            var action = args.First();
            string inputFile = null;

            XDocument document = null;

            if (Console.IsInputRedirected)
            {
                inputFile = null;
                document = XDocument.Load(Console.OpenStandardInput());
            }
            else
            {
                inputFile = args.Skip(1).First();
                document = XDocument.Load(inputFile);
            }

            Console.OutputEncoding = Encoding.UTF8;

            if (OutputActions.TryGetValue(action, out var outputAction))
            {
                outputAction(document, Console.Out);
            }
            else if (DocumentActions.TryGetValue(action, out var documentAction))
            {
                documentAction(document);
                document.Save(Console.Out);
            }
            else if (DocumentListActions.TryGetValue(action, out var listAction))
            {
                var listFile = inputFile == null ? args.Skip(1).First() : args.Skip(2).First();
                var list = File.ReadAllLines(listFile).ToHashSet();
                listAction(document, list);
                document.Save(Console.Out);
            }
        }

        private static void ShowUnusedUnits(XDocument document, TextWriter output)
        => FindUnusedUnits(document).
            Select(u => u.ToString()).
            Join("\n").
            WriteLine(output);

        private static void RemoveUnusedUnits(XDocument document)
         => FindUnusedUnits(document).Remove();

        private static IEnumerable<XElement> FindUnusedUnits(XDocument document)
        => FindUnusedUnits(document, FindUsedUnitIds(document));

        private static IEnumerable<XElement> FindUnusedUnits(XDocument document, HashSet<string> used)
        => document.
            Units().
            Where(u => !used.Contains(u.Id()));

        private static HashSet<string> FindUsedUnitIds(XDocument document)
        => document.
            Metrics().
            Select(e => e.Attribute("unitRef")?.Value).
            Where(id => !id.IsNullOrEmpty()).
            ToHashSet();

        private static void ShowDuplicateContexts(XDocument document, TextWriter output)
        => FindDuplicateContexts(document).
            Select(duplicate => $"{duplicate.Select(d => d.Id()).Join(", ")}\t{duplicate.Key}").
            Join("\n").
            WriteLine(output);

        private static void ShowDuplicateUnits(XDocument document, TextWriter output)
        => FindDuplicateUnits(document).
            Select(duplicate => duplicate.Key).
            Join("\n").
            WriteLine(output);

        private static void RemoveDuplicateUnits(XDocument document)
        => RemoveDuplicateUnits(document, FindDuplicateUnits(document));

        private static void RemoveDuplicateUnits(XDocument document, IEnumerable<IGrouping<string, XElement>> duplicates)
        {
            foreach (var duplicate in duplicates)
            {
                ReplaceUnits(document, duplicate.Skip(1).Select(d => d.Id()), duplicate.First().Id());
            }
        }

        private static void ReplaceUnits(XDocument document, IEnumerable<string> oldIds, string newId)
        => oldIds.
            ToList().ForEach(oldId =>
                ReplaceUnit(document, oldId, newId));

        private static void ReplaceUnit(XDocument document, string oldUnit, string newUnit)
        => document.Root.
            Elements().
            Where(e => e.Attribute("unitId")?.Value == oldUnit).
            ToList().ForEach(e =>
                e.SetAttributeValue("unitId", newUnit));

        private static IEnumerable<IGrouping<string, XElement>> FindDuplicateUnits(XDocument document)
        => document.
            Units().
            GroupBy(u => u.Element(xbrli + "measure")?.Value).
            Where(g => g.Count() > 1);

        private static void ShowUnusedContexts(XDocument document, TextWriter output)
        => FindUnusedContexts(document).
            Select(context => context.Id()).
            Join("\n").
            WriteLine(output);

        private static void RemoveUnusedContexts(XDocument document)
        => FindUnusedContexts(document).
            Remove();

        private static IEnumerable<XElement> FindUnusedContexts(XDocument document)
        => FindUnusedContexts(document, FindUsedContextIds(document));

        private static IEnumerable<XElement> FindUnusedContexts(XDocument document, HashSet<string> usedContextIds)
        => document.
            Contexts().
            Where(c => !usedContextIds.Contains(c.Id()));

        private static HashSet<string> FindUsedContextIds(XDocument document)
        => document.Root.
            Descendants().
            Select(e => e.Context()).
            Where(r => !r.IsNullOrEmpty()).
            ToHashSet();

        private static void RemoveDuplicateContexts(XDocument document)
        => RemoveDuplicateContexts(document, FindDuplicateContexts(document));

        private static void RemoveDuplicateContexts(XDocument document, IEnumerable<IGrouping<string, XElement>> duplicates)
        {
            foreach (var duplicate in duplicates)
            {
                var id = duplicate.First().Id();
                foreach (var d in duplicate.Skip(1))
                {
                    foreach (var f in document.ElementsByContext(d.Id()))
                        f.SetAttributeValue("contextRef", id);
                    d.Remove();
                }
            }
        }

        private static IEnumerable<IGrouping<string, XElement>> FindDuplicateContexts(XDocument document)
        => document.
            Contexts().
            GroupBy(s => GetScenarioComparisonValue(s.Element(xbrli + "scenario"))).
            Where(g => g.Count() > 1);

        private static string GetScenarioComparisonValue(XElement element)
        => element == null
            ? string.Empty
            : element.
                Elements().
                OrderBy(e => e.Attribute("dimension").Value).
                Select(e => e.MemberComparisonValue()).
                Join(",");
    }
}