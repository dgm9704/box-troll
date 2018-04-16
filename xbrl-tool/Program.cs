namespace XbrlTool
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Xml.Linq;

    class Program
    {
        private static XNamespace xbrli = "http://www.xbrl.org/2003/instance";
        private static XNamespace metric = "http://www.eba.europa.eu/xbrl/crr/dict/met";

        private static Dictionary<string, Func<XDocument, XDocument>> Actions =
            new Dictionary<string, Func<XDocument, XDocument>>()
            {
                ["show-unused-contexts"] = ShowUnusedContexts,
                ["show-unused-units"] = ShowUnusedUnits,
                ["show-duplicate-contexts"] = ShowDuplicateContexts,
                ["show-duplicate-units"] = ShowDuplicateUnits,
                ["remove-unused-contexts"] = RemoveUnusedContexts,
                ["remove-unused-units"] = RemoveUnusedUnits,
                ["remove-duplicate-contexts"] = RemoveDuplicateContexts,
                ["remove-duplicate-units"] = RemoveDuplicateUnits,
            };

        static void Main(string[] args)
        {
            var inputFile = args.Last();
            var document = XDocument.Load(inputFile);

            if (Actions.TryGetValue(args.First(), out var action))
            {
                var result = action(document);
                if (result != null)
                    result.Save($"{inputFile}.clean");
            }
            else
            {
                Console.WriteLine($"xbrl-tool\navailable actions:\n{Actions.Keys.Join("\n")}");
            }
        }

        private static XDocument ShowUnusedUnits(XDocument document)
        {
            foreach (var unit in FindUnusedUnits(document))
                Console.WriteLine(unit.ToString());

            return null;
        }

        private static XDocument RemoveUnusedUnits(XDocument document)
        {
            FindUnusedUnits(document).Remove();
            return document;
        }

        private static IEnumerable<XElement> FindUnusedUnits(XDocument document)
        {
            var used = FindUsedUnitIds(document);

            return document.Root.
                Elements(xbrli + "unit").
                Where(u => !used.Contains(u.Id()));
        }

        private static HashSet<string> FindUsedUnitIds(XDocument document)
        => document.Root.
            Elements().
            Where(e => e.Name.Namespace == metric).
            Select(e => e.Attribute("unitRef")?.Value).
            Where(id => !id.IsNullOrEmpty()).
            ToHashSet();

        private static XDocument ShowDuplicateContexts(XDocument document)
        {
            var duplicates = FindDuplicateContexts(document);

            foreach (var duplicate in duplicates)
            {
                Console.WriteLine(duplicate.Select(d => d.Id()).Join(", "));
                Console.WriteLine(duplicate.Key);
            }

            return document;
        }

        private static XDocument ShowDuplicateUnits(XDocument document)
        {
            foreach (var duplicate in FindDuplicateUnits(document))
                Console.WriteLine(duplicate.Key);

            return null;
        }

        private static XDocument RemoveDuplicateUnits(XDocument document)
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

            return document;
        }

        private static IEnumerable<IGrouping<string, XElement>> FindDuplicateUnits(XDocument document)
        => document.Root.
            Elements(xbrli + "unit").
            GroupBy(u => u.Element(xbrli + "measure")?.Value).
            Where(g => g.Count() > 1);

        private static XDocument ShowUnusedContexts(XDocument document)
        {
            foreach (var context in FindUnusedContexts(document))
                Console.WriteLine(context.Id());

            return null;
        }

        private static XDocument RemoveUnusedContexts(XDocument document)
        {
            FindUnusedContexts(document).Remove();
            return document;
        }

        private static IEnumerable<XElement> FindUnusedContexts(XDocument document)
        => FindUnusedContexts(document, FindUsedContextIds(document));

        private static IEnumerable<XElement> FindUnusedContexts(XDocument document, HashSet<string> usedContextIds)
        => document.Root.
            Elements(xbrli + "context").
            Where(c => !usedContextIds.Contains(c.Id()));

        private static HashSet<string> FindUsedContextIds(XDocument document)
        => document.Root.
            Descendants().
            Select(e => e.Attribute("contextRef")?.Value).
            Where(r => !r.IsNullOrEmpty()).
            ToHashSet();

        private static XDocument RemoveDuplicateContexts(XDocument document)
        => RemoveDuplicateContexts(document, FindDuplicateContexts(document));

        private static XDocument RemoveDuplicateContexts(XDocument document, IEnumerable<IGrouping<string, XElement>> duplicates)
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

            return document;
        }

        private static IEnumerable<IGrouping<string, XElement>> FindDuplicateContexts(XDocument document)
        => document.Root.
            Elements(xbrli + "context").
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