using OpenQA.Selenium;

namespace ResilientForm.Finding;

/// <summary>
/// A control found on the page. Elements: the control, or every radio in a group. Locators: for each
/// element, a CSS selector that matches only it within its own document or shadow root, prefixed
/// with "shadow×N › " when it sits N shadow roots deep.
/// </summary>
public sealed record Candidate(FieldKind Kind, string Scope, Clues Clues, IReadOnlyList<IWebElement> Elements,
    IReadOnlyList<string> Locators);

public sealed record Ranked(Candidate Candidate, Score Score);

public sealed record Resolution(FieldSpec Field, IReadOnlyList<Ranked> Ranking)
{
    /// <summary>A second credible candidate too close to the first to tell apart.</summary>
    public bool Ambiguous => Ranking is [var first, var second, ..]
        && second.Score.Points >= Resolver.MinimumScore
        && first.Score.Points - second.Score.Points < Resolver.MinimumMargin;

    public Ranked? Winner => Ranking is [var first, ..] && first.Score.Points >= Resolver.MinimumScore && !Ambiguous
        ? first : null;

    /// <summary>Something credible was found, even if it could not be told apart.</summary>
    public bool Seen => Ranking is [var first, ..] && first.Score.Points >= Resolver.MinimumScore;
}

/// <summary>One form copy (a &lt;form&gt;, or the whole frame when there is none) and its fields.</summary>
public sealed record ScopeResult(string Scope, IReadOnlyList<Resolution> Fields);

public static class Resolver
{
    public const int MinimumScore = 6;
    public const int MinimumMargin = 3;

    public static IReadOnlyList<ScopeResult> Resolve(IEnumerable<Candidate> candidates) =>
        [.. candidates
            .GroupBy(c => c.Scope)
            .Select(scope => new ScopeResult(scope.Key, [.. FieldSpec.All.Select(field => Rank(field, scope))]))
            .Where(scope => scope.Fields.Any(f => f.Seen))];

    static Resolution Rank(FieldSpec field, IEnumerable<Candidate> scope) =>
        new(field, [.. scope
            .Where(c => c.Kind == field.Kind)
            .Select(c => new Ranked(c, Scorer.Rate(field, c.Clues)))
            .OrderByDescending(r => r.Score.Points)]);
}
