using ResilientForm.Finding;

namespace ResilientForm.Tests;

/// <summary>The "decide" step: one winner per field per form copy, or an honest failure.</summary>
public class ResolverTests
{
    static Candidate Text(string scope, string label) =>
        new(FieldKind.Text, scope, new Clues([label], "", "", "", [], []), [], []);

    static Resolution FirstName(ScopeResult scope) => scope.Fields.Single(f => f.Field == FieldSpec.FirstName);

    [Test]
    public void Highest_scorer_in_a_copy_wins()
    {
        var copy = Resolver.Resolve([Text("form#a", "Last Name"), Text("form#a", "First Name")]).Single();

        Assert.That(FirstName(copy).Winner?.Candidate.Clues.Labels[0], Is.EqualTo("First Name"));
    }

    [Test]
    public void Each_copy_is_decided_on_its_own()
    {
        var copies = Resolver.Resolve([Text("form#a", "First Name"), Text("form#b", "First Name")]);

        Assert.That(copies.Select(c => c.Scope), Is.EqualTo(new[] { "form#a", "form#b" }));
        Assert.That(copies.Select(c => FirstName(c).Winner), Is.All.Not.Null);
    }

    [Test]
    public void Two_equally_good_candidates_in_one_copy_are_ambiguous()
    {
        var copy = Resolver.Resolve([Text("form#a", "First Name"), Text("form#a", "First name")]).Single();

        Assert.That(FirstName(copy).Ambiguous, Is.True);
        Assert.That(FirstName(copy).Winner, Is.Null);
    }

    [Test]
    public void Copies_where_nothing_reaches_the_minimum_are_ignored()
    {
        var copies = Resolver.Resolve([Text("form#modal", "Recipient"), Text("form#a", "First Name")]);

        Assert.That(copies.Select(c => c.Scope), Is.EqualTo(new[] { "form#a" }));
    }

    [Test]
    public void Only_candidates_of_the_right_kind_compete()
    {
        var copy = Resolver.Resolve([Text("form#a", "First Name"), Text("form#a", "Gender")]).Single();
        var gender = copy.Fields.Single(f => f.Field == FieldSpec.Gender);

        Assert.That(gender.Ranking, Is.Empty);
        Assert.That(gender.Winner, Is.Null);
    }
}
