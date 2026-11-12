# Resilient form automation (CloudQA Task 1)

C# + Selenium tests for three fields of the
[CloudQA practice form](https://app.cloudqa.io/home/AutomationPracticeForm):
**First Name** (text box), **Gender** (radio buttons) and **State** (dropdown).

The fields are not found by id, XPath or CSS selector. Each one is described by what a
person sees: its label, nearby text and options. Every candidate on the page is scored
against that description, so the tests keep working when ids, names, classes, order or
structure change. They also work in every copy of the form the page contains: inside
iframes, nested iframes, shadow DOM up to 6 levels deep, and an iframe inside shadow DOM.

## Requirements

- [.NET SDK 10](https://dotnet.microsoft.com/download)
- Google Chrome. Selenium Manager downloads the matching driver automatically.

## Run

```bash
cd ResilientForm
dotnet test
```

This runs the scoring unit tests, then the browser tests against the live page and the
seven modified pages in `ResilientForm/Variants/`.

**Against your own page** (the URL is configurable; only this page is tested):

```bash
FORM_URL="https://your-host/modified-form.html" dotnet test
FORM_URL="file:///absolute/path/to/page.html" dotnet test
```

PowerShell: `$env:FORM_URL="https://…"; dotnet test`

**See why each element was picked** (the scoreboard):

```bash
dotnet test --filter Name=Every_copy_resolves_its_fields --logger "console;verbosity=detailed"
```

**Watch the browser**: set `HEADLESS=0`. To fill the main form and keep the page open so you
can look at it, add `HOLD_SECONDS`:

```bash
HEADLESS=0 HOLD_SECONDS=60 FORM_URL="file://$PWD/Variants/duplicate-id.html" \
  dotnet test --filter Name=Main_form_holds_all_three_values_together
```

## What the tests check, in every form copy found

| Test | Checks |
|---|---|
| `Every_copy_resolves_its_fields` | At least one copy found. Each field has a clear winner, never two near-equal candidates. On the local variants: the expected number of copies, and that the *right* element was picked. |
| `First_name_keeps_the_typed_value_in_every_copy` | The typed value reads back, and retyping replaces it. |
| `Gender_allows_exactly_one_choice_in_every_copy` | Male and Female exist. Choosing each option leaves exactly that option selected. |
| `Main_form_holds_all_three_values_together` | In the main form only: fills all three fields, then reads all three back. |
| `State_selects_by_visible_text_in_every_copy` | Choosing an option by its text selects it ("India", or the last option if the page has no India). |
| `ScorerTests`, `ResolverTests` | The scoring and deciding rules on decoy cases, without a browser. |

## Layout

```
ResilientForm/
├── Settings.cs            FORM_URL, HEADLESS, HOLD_SECONDS, timeout
├── PracticeForm.cs        SetFirstName / SelectGender / SelectState and read-backs for one copy
├── Finding/
│   ├── FieldSpec.cs       each field described by intent: kind, words, conflicting words
│   ├── collect.js         runs in the browser: candidates + their clues, across shadow roots
│   ├── Scorer.cs          clues → points + reasons
│   ├── Resolver.cs        per form copy: winner, or "ambiguous"/"not found"
│   └── PageScanner.cs     walks frames, builds the form copies, prints the scoreboard
├── Tests/                 FormTests (browser), ScorerTests, ResolverTests, Targets
└── Variants/              modified pages: renamed attributes, shuffled order,
                           restructured layout, decoys, frames + shadow DOM, and
                           live-shuffled (the full live page, every copy shuffled),
                           duplicate-id (Last Name reuses First Name's id)
tools/
└── make-live-shuffled.py  rebuilds live-shuffled* and duplicate-id from the live site
```

See [APPROACH.md](APPROACH.md) for how it works and its limitations.
