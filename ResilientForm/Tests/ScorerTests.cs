using ResilientForm.Finding;

namespace ResilientForm.Tests;

/// <summary>Decoy cases for the scoring rules, without a browser.</summary>
public class ScorerTests
{
    static Clues Text(string label = "", string near = "", string placeholder = "", string aria = "", params string[] attributes)
        => new(label == "" ? [] : [label], near, placeholder, aria, attributes, []);

    static int Points(FieldSpec spec, Clues clues) => Scorer.Rate(spec, clues).Points;

    [Test]
    public void Linked_label_outweighs_a_stolen_id()
    {
        var real = Text(label: "First Name", near: "First Name", attributes: ["x91", "f_3"]);
        var decoy = Text(label: "Last Name", near: "Last Name", attributes: ["fname", "First Name"]);

        Assert.That(Points(FieldSpec.FirstName, real), Is.GreaterThan(Points(FieldSpec.FirstName, decoy)));
        Assert.That(Points(FieldSpec.FirstName, decoy), Is.LessThan(Resolver.MinimumScore));
    }

    [Test]
    public void Nearby_caption_alone_is_enough()
    {
        // The shadow-DOM copy: its label sits beside a <slot>, so it is not linked.
        Assert.That(Points(FieldSpec.FirstName, Text(near: "First Name")), Is.GreaterThanOrEqualTo(Resolver.MinimumScore));
    }

    [Test]
    public void Qualified_look_alike_is_penalised()
    {
        var real = Text(label: "First Name");
        var fathers = Text(label: "Father's First Name");

        Assert.That(Points(FieldSpec.FirstName, fathers), Is.LessThan(Points(FieldSpec.FirstName, real) - Resolver.MinimumMargin));
    }

    [Test]
    public void Generic_placeholder_does_not_count_as_first_name()
    {
        Assert.That(Points(FieldSpec.FirstName, Text(placeholder: "Name")), Is.Zero);
    }

    [Test]
    public void Matching_is_by_whole_words_and_ignores_case_and_spacing()
    {
        Assert.That(Points(FieldSpec.FirstName, Text(label: "  FIRST   name: ")), Is.GreaterThanOrEqualTo(10));
        Assert.That(Points(FieldSpec.State, Text(label: "Statement")), Is.Zero);
    }

    [Test]
    public void Attribute_hints_ignore_separators()
    {
        Assert.That(Points(FieldSpec.FirstName, Text(attributes: ["first_name"])), Is.EqualTo(3));
        Assert.That(Points(FieldSpec.FirstName, Text(attributes: ["given-name"])), Is.EqualTo(3));
    }

    [Test]
    public void Country_dropdown_does_not_pass_as_state()
    {
        var country = Text(label: "Country", attributes: ["state", "State"]);
        Assert.That(Points(FieldSpec.State, country), Is.LessThan(Resolver.MinimumScore));
    }

    [Test]
    public void Gender_group_is_recognised_by_heading_and_options()
    {
        var gender = new Clues([], "Gender", "", "", ["gender"], ["Male", "Female", "Transgender"]);
        var title = new Clues([], "Title", "", "", ["gender"], ["Mr", "Mrs"]);

        Assert.That(Points(FieldSpec.Gender, gender), Is.EqualTo(6 + 3 + 2 + 2));
        Assert.That(Points(FieldSpec.Gender, title), Is.LessThan(Resolver.MinimumScore));
    }

    [Test]
    public void Female_option_does_not_also_count_as_male()
    {
        var onlyFemale = new Clues([], "", "", "", [], ["Female"]);
        Assert.That(Points(FieldSpec.Gender, onlyFemale), Is.EqualTo(2));
    }

    [Test]
    public void Reasons_explain_each_point()
    {
        var score = Scorer.Rate(FieldSpec.FirstName, Text(label: "First Name", attributes: ["fname"]));
        Assert.That(score.Reasons, Is.EquivalentTo(new[] { "+10 label 'First Name'", "+3 attribute 'fname'" }));
    }
}
