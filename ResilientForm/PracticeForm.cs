using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using ResilientForm.Finding;

namespace ResilientForm;

/// <summary>The three fields of one form copy, by meaning rather than by selector.</summary>
public sealed class PracticeForm(IWebDriver driver, FormCopy copy)
{
    public void SetFirstName(string value)
    {
        var input = Element(FieldSpec.FirstName);
        input.Clear();
        input.SendKeys(value);
    }

    public string FirstName => Element(FieldSpec.FirstName).GetDomProperty("value") ?? "";

    public IReadOnlyList<string> GenderOptions => Winner(FieldSpec.Gender).Clues.Options;

    public void SelectGender(string option)
    {
        var index = GenderOptions.ToList().FindIndex(o => string.Equals(o, option, StringComparison.OrdinalIgnoreCase));
        if (index < 0) throw new ArgumentException($"'{option}' is not one of: {string.Join(", ", GenderOptions)}", nameof(option));
        Radios()[index].Click();
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

    public void SelectState(string option) => State().SelectByText(option);

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
