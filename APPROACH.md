# Approach and limitations

## The problem with selectors

A selector such as `#fname` or `//form/div[1]/input` relies on one fact about the page.
Rename the id or move the div and it either breaks or, worse, quietly finds a different
element. The live page shows this before anyone modifies it. `#fname` matches two
inputs, because the first shadow-DOM copy of the form puts its inputs into the main page
with the same ids, and the same holds for the radio group name `gender`.

## Find by meaning, decide by evidence

Each field is described by intent (`FieldSpec.cs`): what kind of control it is, the
words a person would use for it, and the words that mark a look-alike ("Last", "Father's",
"Country"). Finding a field takes three steps.

1. **Collect** (`collect.js`, one script call per frame). Gather every visible text input,
   every `<select>`, and every radio group, through all open shadow roots. For each one,
   record the clues: linked `<label>`s, `aria-labelledby`, a `<legend>`, the closest
   caption text before it, the placeholder, the aria-label, and the id, name and
   autocomplete attributes. Radio groups also record their option texts. Caption text
   is read from the tree as it is rendered, so an input slotted into a shadow root picks
   up the label next to its `<slot>`.
2. **Score** (`Scorer.cs`). Award points by how stable each clue usually is:

   | Clue | Points |
   |---|---|
   | linked label / legend / aria-labelledby matches | +10 |
   | closest caption text matches | +6 |
   | placeholder or aria-label matches | +4 |
   | id / name / autocomplete matches (`first_name`, `fname`, `given-name`…) | +3 |
   | each expected radio option present (Male, Female) | +2 |
   | a conflicting word in its visible text ("Last Name", "Country") | −8 |

   Matching uses whole words and ignores case and punctuation, so "Female" does not
   count as "Male" and "Statement" is not "State".
3. **Decide** (`Resolver.cs`), separately for each form copy (the nearest enclosing
   `<form>`). The top candidate must score at least 6 and lead the runner-up by at least 3.
   Otherwise the field is reported as *ambiguous* or *not found*. It fails loudly
   instead of guessing.

`PageScanner.cs` repeats this in every iframe, including iframes inside shadow roots,
and remembers the frame path so that actions switch to the right frame first. On the
live page it finds 22 copies. The "IFrame inside Shadow DOM" section loads the practice
page into itself, Chrome refuses to nest it a second time, and a depth limit of 5 guards
against browsers that would.

Every decision is printed with its reasons, for example:

```
[1] top › form#automationtestform
  First Name picked
    ✔   19  Text "First Name": +10 label 'First Name', +6 nearby text 'First Name', +3 attribute 'fname'
  Gender     picked
    ✔   13  RadioGroup "Gender": +6 nearby text 'Gender', +3 attribute 'gender', +2 option 'Male', +2 option 'Female'
```

## How it was checked

- Unit tests for the scoring and deciding rules on decoy cases.
- Seven modified pages in `Variants/`:
  - renamed ids, names and classes, with the old ids given to the wrong fields
  - shuffled field order
  - a table layout with wrapping labels and a fieldset, and no ids
  - decoys: Father's First Name, a Country dropdown with `id="state"`, a "Title" radio
    group named `gender`, a hidden First Name, a search box with placeholder "First name"
  - copies in an iframe, an open shadow root, and a slotted shadow component
  - `live-shuffled.html`: the full live page and the three pages it frames, saved locally
    with the fields shuffled in all 22 copies (`tools/make-live-shuffled.py`). It has to be
    local because the live site sends `X-Frame-Options: SAMEORIGIN`, so its iframe pages
    refuse to load inside a page served from anywhere else.
  - `duplicate-id.html`: the live page with the main form's Last Name label and input
    reusing First Name's id. The browser then links the "Last Name" label to the First
    Name box, which costs it the −8 conflict penalty (19 → 11). It still wins clearly.

  Each page declares how many copies it holds, and its true fields carry a `data-truth`
  marker. The tests check that marker. The finder never reads `data-*` attributes.
- Injected bugs were checked to make the tests fail: clicking the wrong radio, and
  letting ids outweigh labels.

## Observations about the page

- The "State" dropdown actually lists countries (first option: "-- Select Country --").
  Country is a separate free-text box. Option text is therefore not used as a clue for
  State.
- The Gender option texts are `<span>`s, not `<label>`s, so clicking the text does not
  select the radio. Transgender's span even says `for="female"`. Options are matched by
  the text right after each radio.

## Limitations

- **Hand-tuned weights.** The weights were tuned on this page and five variants. A page
  built very differently could need adjusting. The scoreboard shows where.
- **English word lists** in `FieldSpec.cs`. A translated page would not match.
- **Native controls only.** Custom dropdowns (div-based, React-Select and the like) and
  ARIA `role="radio"` widgets are not candidates.
- **Document order, not visual position.** A label placed after its input, or moved
  visually with CSS, is not seen as "nearby". If a radio's text comes *before* the radio,
  the option names are read wrongly.
- **Copies are grouped by `<form>`.** A page without `<form>` tags treats each page or
  shadow root as one copy. Two copies of the form there would be reported as ambiguous
  rather than handled.
- **Closed shadow roots** cannot be read by any script, so fields inside them are not
  found.
- **Radio exclusivity relies on the page.** If a modified page removes the radios' shared
  `name`, the browser lets several be selected, and the Gender test fails. That is a real
  defect in such a page, not in the finder.
- **Speed.** Each test reloads and rescans the page. The live page takes a few seconds
  per test because of its 22 copies.
