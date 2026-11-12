using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using ResilientForm.Finding;

namespace ResilientForm.Tests;

/// <summary>
/// Runs against every target page and every form copy found on it. Copies are only
/// known at run time, so each test loops over them and reports failures per copy.
/// </summary>
[TestFixtureSource(typeof(Targets), nameof(Targets.All))]
[Parallelizable(ParallelScope.Fixtures)]
public class FormTests(Target target)
{
    IWebDriver driver = null!;
    IReadOnlyList<FormCopy> copies = [];

    [OneTimeSetUp]
    public void StartBrowser()
    {
        var options = new ChromeOptions();
        if (Settings.Headless) options.AddArgument("--headless=new");
        options.AddArgument("--window-size=1280,1000");
        driver = new ChromeDriver(options);
    }

    [OneTimeTearDown]
    public void StopBrowser() => driver.Dispose();

    [SetUp]
    public void OpenPage()
    {
        driver.Navigate().GoToUrl(target.Url);
        copies = PageScanner.Scan(driver);
    }

    IEnumerable<(FormCopy Copy, PracticeForm Form)> CopiesWith(FieldSpec field) =>
        copies.Where(c => c[field].Winner is not null).Select(c => (c, new PracticeForm(driver, c)));

    [Test]
    public void Every_copy_resolves_its_fields()
    {
        TestContext.Out.WriteLine(PageScanner.Report(copies));
        Assert.That(copies, Is.Not.Empty, "no form copy found on the page");

        // Local variants declare how many copies they contain and mark the true fields.
        driver.SwitchTo().DefaultContent();
        var expected = driver.FindElements(By.CssSelector("meta[name=expected-copies]")).FirstOrDefault()?.GetAttribute("content");

        Assert.Multiple(() =>
        {
            if (expected is not null)
                Assert.That(copies, Has.Count.EqualTo(int.Parse(expected)), "number of form copies");

            foreach (var copy in copies)
                foreach (var field in copy.Fields)
                {
                    var where = $"{field.Field.Name} in {copy.Location}";
                    if (field.Ambiguous)
                        Assert.Fail($"{where}: two candidates too close to tell apart");
                    else if (field.Winner is null && expected is not null)
                        Assert.Fail($"{where}: not found");
                    else if (field.Winner is null)
                        Assert.Warn($"{where}: not found in this copy");
                    else if (expected is not null)
                    {
                        copy.Enter(driver);
                        var truth = field.Winner.Candidate.Elements.Select(e => e.GetAttribute("data-truth"));
                        Assert.That(truth, Is.All.EqualTo(TruthTag(field.Field)), $"{where}: picked the wrong element");
                    }
                }
        });
    }

    static string TruthTag(FieldSpec field) => field.Name.ToLowerInvariant().Replace(' ', '-');

    [Test]
    public void First_name_keeps_the_typed_value_in_every_copy()
    {
        Assert.Multiple(() =>
        {
            foreach (var (copy, form) in CopiesWith(FieldSpec.FirstName))
            {
                form.SetFirstName("Ada");
                Assert.That(form.FirstName, Is.EqualTo("Ada"), copy.Location);

                form.SetFirstName("Grace Hopper");
                Assert.That(form.FirstName, Is.EqualTo("Grace Hopper"), $"{copy.Location}: retyping replaces the old value");
            }
        });
    }

    [Test]
    public void Gender_allows_exactly_one_choice_in_every_copy()
    {
        Assert.Multiple(() =>
        {
            foreach (var (copy, form) in CopiesWith(FieldSpec.Gender))
            {
                Assert.That(form.GenderOptions, Does.Contain("Male").And.Contain("Female"), copy.Location);
                foreach (var option in form.GenderOptions)
                {
                    form.SelectGender(option);
                    Assert.That(form.SelectedGenders, Is.EqualTo(new[] { option }), $"{copy.Location}: after choosing {option}");
                }
            }
        });
    }

    [Test]
    public void Main_form_holds_all_three_values_together()
    {
        // The main form: the first copy in the top page itself, not inside an iframe or shadow root.
        var main = copies.FirstOrDefault(c => c.FramePath.Count == 0);
        Assert.That(main, Is.Not.Null, "no form copy in the top page");
        var form = new PracticeForm(driver, main!);
        var state = form.StateOptions.Contains("India") ? "India" : form.StateOptions[^1];

        form.SetFirstName("Ada");
        form.SelectGender("Female");
        form.SelectState(state);

        Assert.Multiple(() =>
        {
            Assert.That(form.FirstName, Is.EqualTo("Ada"));
            Assert.That(form.SelectedGenders, Is.EqualTo(new[] { "Female" }));
            Assert.That(form.SelectedState, Is.EqualTo(state));
        });

        Thread.Sleep(Settings.Hold);
    }

    [Test]
    public void State_selects_by_visible_text_in_every_copy()
    {
        Assert.Multiple(() =>
        {
            foreach (var (copy, form) in CopiesWith(FieldSpec.State))
            {
                var option = form.StateOptions.Contains("India") ? "India" : form.StateOptions[^1];
                form.SelectState(option);
                Assert.That(form.SelectedState, Is.EqualTo(option), copy.Location);
            }
        });
    }
}
