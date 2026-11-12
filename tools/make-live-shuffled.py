"""Builds fully local copies of the live practice page (and the three pages it frames):
live-shuffled.html has every form's fields in a different order, and duplicate-id.html keeps the
order but gives the main form's Last Name label and input First Name's id. Local because the live site sends X-Frame-Options: SAMEORIGIN,
so its iframe pages refuse to load inside a page served from anywhere else.

Usage: python3 tools/make-live-shuffled.py   (writes ResilientForm/Variants/live-shuffled* and duplicate-id.html)"""
import datetime, os, random, re, urllib.request

date = datetime.date.today().isoformat()
out_dir = os.path.join(os.path.dirname(__file__), '..', 'ResilientForm', 'Variants')
BASE = 'https://app.cloudqa.io'

def fetch(path):
    request = urllib.request.Request(BASE + path, headers={'User-Agent': 'Mozilla/5.0'})
    return urllib.request.urlopen(request).read().decode('utf-8')
KEY = ['First Name', 'Gender', 'State']

def children(lines, start):
    """Line ranges of the direct child <div>s of the <div> opening at index `start`."""
    depth, out, cur = 0, [], None
    for i in range(start, len(lines)):
        opens, closes = lines[i].count('<div'), lines[i].count('</div>')
        if depth == 1 and opens and cur is None: cur = i
        depth += opens - closes
        if i > start and depth == 1 and cur is not None: out.append((cur, i)); cur = None
        if depth == 0: return out
    raise SystemExit('unbalanced <div>s')

def label(lines, rng):
    m = re.search(r'control-label">\s*([^<]+?)\s*<', '\n'.join(lines[rng[0]:rng[1] + 1]))
    return m.group(1) if m else None

def shuffle(lines, start, seed):
    """Shuffle the labelled field groups under the <div> at `start`; a leading heading stays put."""
    kids = children(lines, start)
    fixed = [k for k in kids[:1] if label(lines, k) is None]
    groups = kids[len(fixed):]
    names = [label(lines, g) for g in groups]
    order = names[:]
    rng = random.Random(seed)
    while any(order.index(k) == names.index(k) for k in KEY):
        rng.shuffle(order)
    new = fixed + [groups[names.index(n)] for n in order]
    lines[kids[0][0]:kids[-1][1] + 1] = [l for g in new for l in lines[g[0]:g[1] + 1]]
    return order

def build(source, target, frames, seed=None, edits=()):
    """seed=None keeps the field order; edits are (old, new) text replacements that must match once."""
    lines = fetch(source).split('\n')
    form = next(i for i, l in enumerate(lines) if 'id="automationtestform"' in l)
    if seed is not None:
        print(f'{target}: form   →', shuffle(lines, form + 1, seed))
    component = next((i for i, l in enumerate(lines) if "customElements.define('shadow-form'" in l), None)
    if component is not None and seed is not None:
        row = next(i for i in range(component, len(lines)) if '<div class="row">' in lines[i])
        print(f'{target}: shadow →', shuffle(lines, row, seed + 1))

    html = '\n'.join(lines)
    for old, new in edits:
        assert html.count(old) == 1, old
        html = html.replace(old, new)
    html, n = re.subn(r'<script type="text/javascript">\s*\(function \(c, l, a, r, i, t, y\).*?</script>\s*', '', html, flags=re.S)
    assert n == 1, 'analytics script not found'
    for old, new in frames.items():
        assert f'src="{old}"' in html, old
        html = html.replace(f'src="{old}"', f'src="{new}"')
    # Mark the true fields so the tests can check the right element was picked (the finder ignores data-*).
    for old, mark in [('<input id="fname" type="text" name="First Name"', 'first-name'),
                      ('<input id="fname" name="fname" type="text"', 'first-name'),
                      ('type="radio" name="gender"', 'gender'),
                      ('<select class="form-control" id="state" name="State">', 'state')]:
        html = html.replace(old, old.rstrip('>') + f' data-truth="{mark}"' + ('>' if old.endswith('>') else ''))
    open(os.path.join(out_dir, target), 'w').write(html)
    return html

top = build('/home/AutomationPracticeForm', 'live-shuffled.html', {
    '/Home/IFrame': 'live-shuffled/iframe.html',
    '/Home/InnerNestedIFrame': 'live-shuffled/inner-nested-iframe.html',
    'AutomationPracticeForm': 'live-shuffled.html'}, seed=1)
os.makedirs(os.path.join(out_dir, 'live-shuffled'), exist_ok=True)
build('/Home/IFrame', 'live-shuffled/iframe.html', {'/Home/NestedIFrame': 'nested-iframe.html'}, seed=11)
build('/Home/NestedIFrame', 'live-shuffled/nested-iframe.html', {'/Home/InnerNestedIFrame': 'inner-nested-iframe.html'}, seed=21)
build('/Home/InnerNestedIFrame', 'live-shuffled/inner-nested-iframe.html', {}, seed=31)

# Same page and order, but the main form's Last Name label and input reuse First Name's id, so
# three elements share id "fname" and the Last Name label links to the First Name box.
duplicate = build('/home/AutomationPracticeForm', 'duplicate-id.html', {
    '/Home/IFrame': 'live-shuffled/iframe.html',
    '/Home/InnerNestedIFrame': 'live-shuffled/inner-nested-iframe.html',
    'AutomationPracticeForm': 'duplicate-id.html'}, edits=[(
    '''<label for="lname" name="lname" class="control-label">Last Name</label>
                        <input id="lname" type="text" name="Last Name"''',
    '''<label for="fname" name="lname" class="control-label">Last Name</label>
                        <input id="fname" type="text" name="Last Name"''')])

def finish(name, html, what):
    """Header comment and expected copy count on a top page."""
    html, n = re.subn(r'(<head[^>]*>)', r'\1\n    <meta name="expected-copies" content="22">', html, count=1)
    assert n == 1, 'no <head> tag'
    html, n = re.subn(r'^\s*<!DOCTYPE html>', lambda m: m.group(0) + (
        f'\n<!-- The live practice page (snapshot of {date}) and the pages it frames, saved locally {what}.\n'
        '     Generated by tools/make-live-shuffled.py, not hand-edited. 22 copies: 11 here, and 11 more\n'
        '     because the "IFrame inside Shadow DOM" loads this page into itself once, as the live page does. -->'),
        html, count=1, flags=re.I)
    assert n == 1, 'no doctype'
    open(os.path.join(out_dir, name), 'w').write(html)

finish('live-shuffled.html', top, "with every\n     form's fields shuffled")
finish('duplicate-id.html', duplicate, "with the main form's\n     Last Name label and input reusing First Name's id (the iframe pages are the shuffled ones)")
