using System.Text.RegularExpressions;

namespace ResilientForm.Finding;

/// <summary>Everything the page says about one candidate control, as gathered by collect.js.</summary>
public sealed record Clues(
    string[] Labels,      // linked <label>s, aria-labelledby, <legend>
    string Near,          // closest caption text before the control in its own container
    string Placeholder,
    string AriaLabel,
    string[] Attributes,  // id, name, autocomplete
    string[] Options);    // radio option texts (empty for other kinds)

public sealed record Score(int Points, IReadOnlyList<string> Reasons);

/// <summary>
/// Turns clues into points. Weights follow how stable each clue is: what a user
/// reads (labels) rarely changes; what a developer names (ids) changes often.
/// </summary>
public static partial class Scorer
{
    const int Label = 10, Near = 6, Hint = 4, Attribute = 3, Option = 2, Conflict = -8;

    public static Score Rate(FieldSpec spec, Clues clues)
    {
        var points = 0;
        var reasons = new List<string>();
        void Add(int value, string why) { points += value; reasons.Add($"{value:+0;-0} {why}"); }

        if (clues.Labels.FirstOrDefault(l => HasPhrase(l, spec.Phrases)) is { } label)
            Add(Label, $"label '{Clean(label)}'");
        if (HasPhrase(clues.Near, spec.Phrases))
            Add(Near, $"nearby text '{Clean(clues.Near)}'");
        if (HasPhrase(clues.Placeholder, spec.Phrases))
            Add(Hint, $"placeholder '{Clean(clues.Placeholder)}'");
        else if (HasPhrase(clues.AriaLabel, spec.Phrases))
            Add(Hint, $"aria-label '{Clean(clues.AriaLabel)}'");

        var attribute = clues.Attributes.FirstOrDefault(a => spec.AttributeHints.Any(Squash(a).Contains));
        if (attribute is not null)
            Add(Attribute, $"attribute '{attribute}'");

        string[] visible = [.. clues.Labels, clues.Near, clues.Placeholder, clues.AriaLabel];
        foreach (var word in spec.Conflicts)
            if (visible.FirstOrDefault(t => HasPhrase(t, [word])) is { } text)
            {
                Add(Conflict, $"conflicting word '{word}' in '{Clean(text)}'");
                break;
            }

        foreach (var expected in spec.ExpectedOptions)
            if (clues.Options.FirstOrDefault(o => HasPhrase(o, [expected])) is { } option)
                Add(Option, $"option '{Clean(option)}'");

        return new Score(points, reasons);
    }

    static bool HasPhrase(string text, string[] phrases)
    {
        var words = $" {Words(text)} ";
        return phrases.Any(p => words.Contains($" {p} "));
    }

    static string Clean(string text) => Whitespace().Replace(text, " ").Trim();
    static string Words(string text) => NonWord().Replace(text.ToLowerInvariant(), " ").Trim();
    static string Squash(string text) => NonWord().Replace(text.ToLowerInvariant(), "");

    [GeneratedRegex(@"\s+")] private static partial Regex Whitespace();
    [GeneratedRegex(@"[^\p{L}\p{N}]+")] private static partial Regex NonWord();
}
