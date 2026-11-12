// Runs inside one frame (as a WebDriver script body, hence the top-level return).
// Gathers every candidate control and the text that describes it, across all open
// shadow roots, plus the iframes to descend into. It only collects clues; the
// scoring and the decision happen in C#.

const CONTROL = 'input:not([type=hidden]), select, textarea';

const roots = [];
(function walk(root) {
  roots.push(root);
  for (const el of root.querySelectorAll('*')) if (el.shadowRoot) walk(el.shadowRoot);
})(document);
const all = selector => roots.flatMap(root => [...root.querySelectorAll(selector)]);

// Navigate the tree as it is rendered: a slotted node sits where its <slot> is,
// and the top of a shadow root continues at its host.
const place = node => node.assignedSlot || node;
const parent = node => {
  const p = place(node).parentNode;
  return p instanceof ShadowRoot ? p.host : p;
};
const shadowDepth = node => {
  let depth = 0;
  for (let root = node.getRootNode(); root instanceof ShadowRoot; root = root.host.getRootNode()) depth++;
  return depth;
};

const visible = el => (el.checkVisibility ? el.checkVisibility({ visibilityProperty: true }) : el.offsetParent !== null);
const clean = text => (text || '').replace(/\s+/g, ' ').trim();

// Text a person reads in a node, leaving out the contents of controls (a label
// wrapping a <select> should not read as every option in it).
function ownText(node) {
  if (node.nodeType === Node.TEXT_NODE) return node.textContent;
  if (node.nodeType !== Node.ELEMENT_NODE && !(node instanceof ShadowRoot)) return '';
  if (node.nodeType === Node.ELEMENT_NODE && node.matches(`${CONTROL}, script, style, template`)) return '';
  return [...(node.shadowRoot || node).childNodes].map(ownText).join(' ');
}

function containsControl(node) {
  if (node.nodeType !== Node.ELEMENT_NODE) return false;
  if (node.matches(CONTROL) || node.querySelector(CONTROL)) return true;
  if (node.shadowRoot && node.shadowRoot.querySelector(CONTROL)) return true;
  const slots = [...(node.matches('slot') ? [node] : []), ...node.querySelectorAll('slot')];
  return slots.some(slot => slot.assignedElements().some(containsControl));
}

// The closest caption before a control: walk back through earlier siblings, then
// up a level, and stop at the first text, or at a neighbouring field (whose
// caption is not ours).
function nearbyText(el) {
  let node = el;
  for (let depth = 0; node && node.nodeType === Node.ELEMENT_NODE && depth < 4; depth++) {
    for (let sibling = place(node).previousSibling; sibling; sibling = sibling.previousSibling) {
      if (sibling.nodeType === Node.ELEMENT_NODE && !visible(sibling)) continue;
      if (containsControl(sibling)) return '';
      const text = clean(ownText(sibling));
      if (text) return text;
    }
    node = parent(node);
  }
  return '';
}

function labelledBy(el) {
  const ids = (el.getAttribute('aria-labelledby') || '').split(/\s+/).filter(Boolean);
  return ids.map(id => el.getRootNode().getElementById(id)).filter(Boolean).map(n => clean(ownText(n)));
}

const attributes = el => ['id', 'name', 'autocomplete'].map(a => el.getAttribute(a)).filter(Boolean);

// Radio option text: its own label, else the text right after it, else its value.
function optionText(radio) {
  const label = [...(radio.labels || [])].map(l => clean(ownText(l))).find(Boolean);
  if (label) return label;
  let text = '';
  for (let s = radio.nextSibling; s && !(s.nodeType === Node.ELEMENT_NODE && containsControl(s)); s = s.nextSibling)
    text += ' ' + ownText(s);
  return clean(text) || radio.value || '';
}

// A form copy is the closest enclosing <form>, looking through shadow boundaries;
// without one, the root (page or shadow root) the control lives in.
const scopes = new Map();
function scopeOf(el) {
  let owner = null;
  for (let node = el; node && !owner; node = parent(node))
    if (node.nodeType === Node.ELEMENT_NODE && node.tagName === 'FORM') owner = node;
  owner = owner || el.getRootNode();
  if (!scopes.has(owner)) {
    const depth = shadowDepth(owner instanceof ShadowRoot ? owner.host : owner);
    const name = owner instanceof Document ? 'page'
      : owner instanceof ShadowRoot ? owner.host.tagName.toLowerCase()
      : 'form' + (owner.id ? '#' + owner.id : '');
    let label = (depth ? `shadow×${depth} › ` : '') + name;
    const taken = new Set(scopes.values());
    for (let n = 2; taken.has(label); n++) label = label.replace(/ \(\d+\)$/, '') + ` (${n})`;
    scopes.set(owner, label);
  }
  return scopes.get(owner);
}

// A CSS selector that matches only this element within its own document or shadow root: by id
// or name when that is unique there, else a path of steps down from the root (or from the
// closest ancestor with a unique id). Used for reporting; the finder never locates by it.
function locatorOf(el) {
  const root = el.getRootNode();
  const unique = selector => root.querySelectorAll(selector).length === 1;
  const quote = value => `"${value.replace(/["\\]/g, '\\$&')}"`;
  const tag = el.tagName.toLowerCase();
  const name = el.getAttribute('name');
  const value = el.getAttribute('value');
  const short = [
    el.id && `${tag}#${CSS.escape(el.id)}`,
    name && `${tag}[name=${quote(name)}]`,
    name && value && `${tag}[name=${quote(name)}][value=${quote(value)}]`,
  ].filter(Boolean).find(unique);

  const steps = [];
  for (let node = el; !short && node && node.nodeType === Node.ELEMENT_NODE; node = node.parentNode) {
    if (node !== el && node.id && unique('#' + CSS.escape(node.id))) {
      steps.unshift('#' + CSS.escape(node.id));
      break;
    }
    const twins = [...node.parentNode.children].filter(child => child.tagName === node.tagName);
    steps.unshift(node.tagName.toLowerCase() + (twins.length > 1 ? `:nth-of-type(${twins.indexOf(node) + 1})` : ''));
  }
  const depth = shadowDepth(el);
  return (depth ? `shadow×${depth} › ` : '') + (short || steps.join(' > '));
}

const elements = [];
const ref = el => elements.push(el) - 1;
const candidates = [];

for (const el of all('input, select')) {
  const kind = el.tagName === 'SELECT' ? 'select' : el.type === 'text' ? 'text' : null;
  if (!kind || el.disabled || el.readOnly || !visible(el)) continue;
  candidates.push({
    kind,
    scope: scopeOf(el),
    labels: [...[...(el.labels || [])].map(l => clean(ownText(l))), ...labelledBy(el)].filter(Boolean),
    near: nearbyText(el),
    placeholder: el.getAttribute('placeholder') || '',
    aria: el.getAttribute('aria-label') || '',
    attributes: attributes(el),
    options: [],
    elements: [ref(el)],
    locators: [locatorOf(el)],
  });
}

// Radio groups: radios that share the closest container holding more than one
// radio, split further by name. This survives a missing or reused name attribute.
const groups = new Map();
for (const radio of all('input[type=radio]')) {
  if (radio.disabled) continue;
  let container = parent(radio);
  while (container && container.nodeType === Node.ELEMENT_NODE && container.querySelectorAll('input[type=radio]').length < 2)
    container = parent(container);
  container = container || radio.getRootNode();
  if (!groups.has(container)) groups.set(container, new Map());
  const byName = groups.get(container);
  const name = radio.name || '';
  if (!byName.has(name)) byName.set(name, []);
  byName.get(name).push(radio);
}
for (const [container, byName] of groups)
  for (const [name, radios] of byName) {
    const fieldset = container.closest && container.closest('fieldset');
    const legend = fieldset && fieldset.querySelector(':scope > legend');
    candidates.push({
      kind: 'radio',
      scope: scopeOf(radios[0]),
      labels: [...(legend ? [clean(ownText(legend))] : []), ...(container.getAttribute ? labelledBy(container) : [])].filter(Boolean),
      near: nearbyText(radios[0]),
      placeholder: '',
      aria: (container.getAttribute && container.getAttribute('aria-label')) || '',
      attributes: name ? [name] : [],
      options: radios.map(optionText),
      elements: radios.map(ref),
      locators: radios.map(locatorOf),
    });
  }

const frames = all('iframe, frame').map((frame, i) => ({
  label: (shadowDepth(frame) ? `shadow×${shadowDepth(frame)} › ` : '') + (frame.id ? `iframe#${frame.id}` : `iframe[${i}]`),
  element: ref(frame),
}));

return [JSON.stringify({ candidates, frames }), elements];
