namespace ResilientForm.Finding;

public enum FieldKind { Text, RadioGroup, Select }

/// <summary>
/// Describes a field by intent: what kind of control it is and which words a
/// person (or, less reliably, a developer) would use for it. Nothing here is a
/// selector.
/// </summary>
public sealed record FieldSpec(
    string Name,
    FieldKind Kind,
    string[] Phrases,          // whole-word phrases in visible text: labels, nearby text, placeholder
    string[] AttributeHints,   // substrings of id/name/autocomplete once separators are stripped
    string[] Conflicts,        // qualifier words that mean "a different field that looks similar"
    string[] ExpectedOptions)  // option texts a radio group should offer
{
    public static readonly FieldSpec FirstName = new(
        "First Name", FieldKind.Text,
        Phrases: ["first name", "given name", "forename"],
        AttributeHints: ["firstname", "fname", "givenname", "forename"],
        Conflicts: ["last", "surname", "middle", "family", "father", "mother", "spouse"],
        ExpectedOptions: []);

    public static readonly FieldSpec Gender = new(
        "Gender", FieldKind.RadioGroup,
        Phrases: ["gender", "sex"],
        AttributeHints: ["gender", "sex"],
        Conflicts: [],
        ExpectedOptions: ["male", "female"]);

    public static readonly FieldSpec State = new(
        "State", FieldKind.Select,
        Phrases: ["state", "province", "region"],
        AttributeHints: ["state", "province", "region"],
        Conflicts: ["country"],
        ExpectedOptions: []);

    public static readonly IReadOnlyList<FieldSpec> All = [FirstName, Gender, State];
}
