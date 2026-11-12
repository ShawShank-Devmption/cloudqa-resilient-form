using System.Reflection;
using System.Text;
using System.Text.Json;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;

namespace ResilientForm.Finding;

/// <summary>One copy of the form, wherever it lives: Location reads like "iframe#x › shadow×2 › form#y".</summary>
public sealed record FormCopy(string Location, IReadOnlyList<IWebElement> FramePath, IReadOnlyList<Resolution> Fields)
{
    public Resolution this[FieldSpec field] => Fields.Single(f => f.Field == field);

    /// <summary>Switch the driver into the frame this copy lives in.</summary>
    public void Enter(IWebDriver driver)
    {
        driver.SwitchTo().DefaultContent();
        foreach (var frame in FramePath)
            driver.SwitchTo().Frame(frame);
    }
}

/// <summary>
/// Finds every form copy on the loaded page: runs collect.js in the top frame,
/// then switches into each iframe it reports and repeats.
/// </summary>
public static class PageScanner
{
    /// <summary>Guards against pages that embed themselves.</summary>
    public const int MaxFrameDepth = 5;

    static readonly string Collect = ReadScript();
    static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    sealed record RawCandidate(string Kind, string Scope, string[] Labels, string Near, string Placeholder,
        string Aria, string[] Attributes, string[] Options, int[] Elements, string[]? Locators);
    sealed record RawFrame(string Label, int Element);
    sealed record RawScan(RawCandidate[] Candidates, RawFrame[] Frames);

    /// <summary>Waits until at least one copy is found (pages may render late), then returns them all.</summary>
    public static IReadOnlyList<FormCopy> Scan(IWebDriver driver)
    {
        IReadOnlyList<FormCopy> copies = [];
        try
        {
            new WebDriverWait(driver, Settings.Timeout).Until(_ =>
            {
                driver.SwitchTo().DefaultContent();
                WaitForLoad(driver);
                copies = ScanFrame(driver, [], "top", 0);
                return copies.Count > 0;
            });
        }
        catch (WebDriverTimeoutException)
        {
            // Nothing found: callers decide whether that is a failure.
        }
        driver.SwitchTo().DefaultContent();
        return copies;
    }

    static List<FormCopy> ScanFrame(IWebDriver driver, IReadOnlyList<IWebElement> path, string location, int depth)
    {
        var result = (IReadOnlyList<object>)((IJavaScriptExecutor)driver).ExecuteScript(Collect)!;
        var scan = JsonSerializer.Deserialize<RawScan>((string)result[0], Json)!;
        var elements = ((IReadOnlyList<object>)result[1]).Cast<IWebElement>().ToList();

        var candidates = scan.Candidates.Select(c => new Candidate(
            c.Kind switch { "text" => FieldKind.Text, "radio" => FieldKind.RadioGroup, _ => FieldKind.Select },
            c.Scope,
            new Clues(c.Labels, c.Near, c.Placeholder, c.Aria, c.Attributes, c.Options),
            [.. c.Elements.Select(i => elements[i])],
            c.Locators ?? []));

        var copies = Resolver.Resolve(candidates)
            .Select(s => new FormCopy($"{location} › {s.Scope}", path, s.Fields))
            .ToList();

        if (depth >= MaxFrameDepth) return copies;
        foreach (var frame in scan.Frames)
        {
            var element = elements[frame.Element];
            try
            {
                driver.SwitchTo().Frame(element);
                WaitForLoad(driver);
                copies.AddRange(ScanFrame(driver, [.. path, element], $"{location} › {frame.Label}", depth + 1));
            }
            catch (WebDriverException)
            {
                // A frame that cannot be entered or read (detached, still navigating) holds no copies we can use.
            }
            finally
            {
                driver.SwitchTo().DefaultContent();
                foreach (var f in path) driver.SwitchTo().Frame(f);
            }
        }
        return copies;
    }

    static void WaitForLoad(IWebDriver driver) =>
        new WebDriverWait(driver, Settings.Timeout).Until(d =>
            ((IJavaScriptExecutor)d).ExecuteScript("return document.readyState") as string == "complete");

    static string ReadScript()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("collect.js")!;
        return new StreamReader(stream).ReadToEnd();
    }

    /// <summary>
    /// The scoreboard: for each copy and field, the winner and the closest runners-up, with reasons
    /// and the locator of each candidate (the first radio, for a group).
    /// </summary>
    public static string Report(IReadOnlyList<FormCopy> copies)
    {
        var report = new StringBuilder($"{copies.Count} form cop{(copies.Count == 1 ? "y" : "ies")} found\n");
        for (var i = 0; i < copies.Count; i++)
        {
            report.Append($"\n[{i + 1}] {copies[i].Location}\n");
            foreach (var field in copies[i].Fields)
            {
                var verdict = field.Ambiguous ? "AMBIGUOUS" : field.Winner is null ? "not found" : "picked";
                report.Append($"  {field.Field.Name,-10} {verdict}\n");
                foreach (var ranked in field.Ranking.Take(3))
                {
                    var mark = ranked == field.Winner ? "✔" : " ";
                    var why = ranked.Score.Reasons.Count > 0 ? string.Join(", ", ranked.Score.Reasons) : "no matching clues";
                    report.Append($"    {mark} {ranked.Score.Points,4}  {Describe(ranked.Candidate)}: {why}\n");
                }
            }
        }
        return report.ToString();
    }

    static string Describe(Candidate candidate)
    {
        var c = candidate.Clues;
        var name = c.Labels.FirstOrDefault() ?? (c.Near.Length > 0 ? c.Near : null) ?? c.Attributes.FirstOrDefault() ?? c.Placeholder;
        return $"{candidate.Kind} \"{(name.Length > 30 ? name[..30] + "…" : name)}\" [{candidate.Locators.FirstOrDefault()}]";
    }
}
