namespace ResilientForm.Tests;

public sealed record Target(string Name, string Url)
{
    public override string ToString() => Name;
}

public static class Targets
{
    /// <summary>FORM_URL alone when it is set; otherwise the live page plus every local variant.</summary>
    public static IEnumerable<Target> All()
    {
        if (Settings.FormUrl is { } url)
        {
            yield return new Target("FORM_URL", url);
            yield break;
        }
        yield return new Target("live", Settings.LiveUrl);
        foreach (var file in Directory.GetFiles(Path.Combine(AppContext.BaseDirectory, "Variants"), "*.html").Order())
            yield return new Target(Path.GetFileNameWithoutExtension(file), new Uri(file).AbsoluteUri);
    }
}
