namespace XbrlTool
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
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

        private static Dictionary<string, Action<XDocument, IEnumerable<string>>> DocumentListActions =
            new Dictionary<string, Action<XDocument, IEnumerable<string>>>()
            {
                ["remove-datapoints"] = RemoveDatapoints,
            };

        private static void RemoveDatapoints(XDocument document, IEnumerable<string> list)
        {
            var remove = list.ToHashSet();

            document.
                Metrics().
                Where(m => remove.Contains($"{m.Name.LocalName} {m.Context()}")).
                Remove();
        }

        static void Main(string[] args)
        {
            if (args.Length < 2 || args.Any(a => a.Equals("help", StringComparison.OrdinalIgnoreCase)))
            {
                Console.WriteLine($"xbrl-tool\navailable actions:\n{OutputActions.Keys.Concat(DocumentActions.Keys).Join("\n")}");
                return;
            }

            var action = args.First();
            var inputFile = args.Skip(1).First();
            var document = XDocument.Load(inputFile);

            if (OutputActions.TryGetValue(action, out var outputAction))
            {
                outputAction(document, Console.Out);
            }
            else if (DocumentActions.TryGetValue(action, out var documentAction))
            {
                documentAction(document);
                document.Save($"{inputFile}.clean");
            }
            else if (DocumentListActions.TryGetValue(action, out var listAction))
            {
                var list = File.ReadAllLines(args.Skip(2).First());
                listAction(document, list);
                document.Save($"{inputFile}.clean");
            }

        }

        private static void ShowUnusedUnits(XDocument document, TextWriter output)
        {
            foreach (var unit in FindUnusedUnits(document))
                output.WriteLine(unit.ToString());
        }

        private static void RemoveUnusedUnits(XDocument document)
        {
            FindUnusedUnits(document).Remove();
        }

        private static IEnumerable<XElement> FindUnusedUnits(XDocument document)
        {
            var used = FindUsedUnitIds(document);

            return document.
                Units().
                Where(u => !used.Contains(u.Id()));
        }

        private static HashSet<string> FindUsedUnitIds(XDocument document)
        => document.
            Metrics().
            Select(e => e.Attribute("unitRef")?.Value).
            Where(id => !id.IsNullOrEmpty()).
            ToHashSet();

        private static void ShowDuplicateContexts(XDocument document, TextWriter output)
        {
            foreach (var duplicate in FindDuplicateContexts(document))
                output.WriteLine($"{duplicate.Select(d => d.Id()).Join(", ")}\t{duplicate.Key}");
        }

        private static void ShowDuplicateUnits(XDocument document, TextWriter output)
        {
            foreach (var duplicate in FindDuplicateUnits(document))
                output.WriteLine(duplicate.Key);
        }

        private static void RemoveDuplicateUnits(XDocument document)
        {
            var duplicates = FindDuplicateUnits(document);

            foreach (var duplicate in duplicates)
            {
                var id = duplicate.First().Id();
                foreach (var d in duplicate.Skip(1))
                {
                    var duplicateId = d.Id();
                    foreach (var f in document.Root.Elements().Where(e => e.Attribute("unitId")?.Value == duplicateId))
                        f.SetAttributeValue("unitId", id);
                    d.Remove();
                }
            }
        }

        private static IEnumerable<IGrouping<string, XElement>> FindDuplicateUnits(XDocument document)
        => document.
            Units().
            GroupBy(u => u.Element(xbrli + "measure")?.Value).
            Where(g => g.Count() > 1);

        private static void ShowUnusedContexts(XDocument document, TextWriter output)
        {
            foreach (var context in FindUnusedContexts(document))
                output.WriteLine(context.Id());
        }

        private static void RemoveUnusedContexts(XDocument document)
        {
            FindUnusedContexts(document).Remove();
        }

        private static IEnumerable<XElement> FindUnusedContexts(XDocument document)
        => FindUnusedContexts(document, FindUsedContextIds(document));

        private static IEnumerable<XElement> FindUnusedContexts(XDocument document, HashSet<string> usedContextIds)
        => document.
            Contexts().
            Where(c => !usedContextIds.Contains(c.Id()));

        private static HashSet<string> FindUsedContextIds(XDocument document)
        => document.Root.
            Descendants().
            Select(e => e.Attribute("contextRef")?.Value).
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
                Select(e => MemberComparisonValue(e)).
                Join(",");

        private static string MemberComparisonValue(XElement e)
        => $"{e.Attribute("dimension").Value.Split(':').Last()}={e.Value}";
    }
}