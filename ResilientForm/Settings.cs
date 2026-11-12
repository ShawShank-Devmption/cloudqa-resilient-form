namespace ResilientForm;

public static class Settings
{
    public const string LiveUrl = "https://app.cloudqa.io/home/AutomationPracticeForm";

    /// <summary>Page under test. When set, only this page is tested.</summary>
    public static string? FormUrl => Environment.GetEnvironmentVariable("FORM_URL") is { Length: > 0 } url ? url : null;

    /// <summary>HEADLESS=0 shows the browser.</summary>
    public static bool Headless => Environment.GetEnvironmentVariable("HEADLESS") != "0";

    /// <summary>HOLD_SECONDS=30 keeps the filled page open that long, to look at it (use with HEADLESS=0).</summary>
    public static TimeSpan Hold =>
        TimeSpan.FromSeconds(int.TryParse(Environment.GetEnvironmentVariable("HOLD_SECONDS"), out var seconds) ? seconds : 0);

    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);
}
