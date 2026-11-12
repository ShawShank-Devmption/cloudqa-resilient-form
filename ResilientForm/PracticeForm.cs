using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ResilientForm.Finding;

namespace ResilientForm;

/// <summary>The three fields of one form copy, by meaning rather than by selector.</summary>
public sealed class PracticeForm(IWebDriver driver, FormCopy copy)
{
    readonly Dictionary<FieldSpec, (string Locator, string Value)> filled = [];

    /// <summary>What this form object last set in each field, and the locator of the element it used.</summary>
    public IReadOnlyDictionary<FieldSpec, (string Locator, string Value)> Filled => filled;

    public void SetFirstName(string value)
    {
        var input = Element(FieldSpec.FirstName);
        input.Clear();
        input.SendKeys(value);
        filled[FieldSpec.FirstName] = (Winner(FieldSpec.FirstName).Locators[0], value);
    }

    public string FirstName => Element(FieldSpec.FirstName).GetDomProperty("value") ?? "";

    public IReadOnlyList<string> GenderOptions => Winner(FieldSpec.Gender).Clues.Options;

    public void SelectGender(string option)
    {
        var index = GenderOptions.ToList().FindIndex(o => string.Equals(o, option, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw new ArgumentException($"'{option}' is not one of: {string.Join(", ", GenderOptions)}", nameof(option));
        Radios()[index].Click();
        filled[FieldSpec.Gender] = (Winner(FieldSpec.Gender).Locators[index], GenderOptions[index]);
    }

    public IReadOnlyList<string> SelectedGenders
    {
        get
        {
            var radios = Radios();
            return [.. GenderOptions.Where((_, i) => radios[i].Selected)];
        }
    }

    public IReadOnlyList<string> StateOptions => [.. State().Options.Select(o => o.Text.Trim())];

    public void SelectState(string option)
    {
        State().SelectByText(option);
        filled[FieldSpec.State] = (Winner(FieldSpec.State).Locators[0], option);
    }

    /// <summary>Outlines the elements this form object filled, so they stand out in a visible browser.</summary>
    public void Highlight()
    {
        copy.Enter(driver);
        foreach (var field in filled.Keys)
        {
            var picked = Winner(field);
            var element = picked.Elements[picked.Locators.ToList().IndexOf(filled[field].Locator)];
            ((IJavaScriptExecutor)driver).ExecuteScript(
                "arguments[0].style.outline = '3px solid #e11d48'; arguments[0].style.outlineOffset = '2px';", element);
        }
    }

    public string SelectedState => State().SelectedOption.Text.Trim();

    Candidate Winner(FieldSpec field) =>
        copy[field].Winner?.Candidate ?? throw new NotFoundException($"{field.Name} not found in {copy.Location}");

    IReadOnlyList<IWebElement> Radios()
    {
        var radios = Winner(FieldSpec.Gender).Elements;
        copy.Enter(driver);
        return radios;
    }

    IWebElement Element(FieldSpec field)
    {
        var element = Winner(field).Elements[0];
        copy.Enter(driver);
        return element;
    }

    SelectElement State() => new(Element(FieldSpec.State));
}
