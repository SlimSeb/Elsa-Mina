"use strict";

// minipoke playground: runs a released minipoke.onnx in the browser (onnxruntime-web,
// wasm) on the battle states recorded in its parity fixtures (make_fixtures.py), shows
// its policy and value, and lets you edit the inputs. The page never encodes a battle:
// every input comes from a fixture decision. Serve the repository root
// (python3 -m http.server 8765) and open /tools/minipoke/playground/.
// window.playground exposes the state and the main actions for the devtools console.

const ORT_DIST = "https://cdn.jsdelivr.net/npm/onnxruntime-web@1.20.1/dist/";
const MODELS_URL = new URL("../../../src/ElsaMina.Battles/Models/", location.href);
const fixturesUrl = (model) =>
  new URL(`../../../test/ElsaMina.UnitTests/Battles/Strategies/${encodeURIComponent(model)}/Fixtures/`, location.href);

const N_SLOTS = 6;
const ACT_DIM = 14;
const PARITY_TOLERANCE = 1e-4;
// Rewards are +-1 for the result plus +-0.1 per faint (players.py), so |return| <= 1.6
const VALUE_RANGE = 1.6;
// The model writes -1e9 for masked actions
const MASKED_LOGIT = -1e8;
const EDIT_DEBOUNCE_MS = 60;
// encoder.STACKABLE: these side conditions are stored as layers / cap
const STACKABLE = { SPIKES: 3, TOXIC_SPIKES: 2 };
const SIDE_CONTROLS = ["STEALTH_ROCK", "SPIKES", "TOXIC_SPIKES", "STICKY_WEB", "REFLECT", "LIGHT_SCREEN",
  "AURORA_VEIL", "TAILWIND", "SAFEGUARD", "MIST"];
// A v1 spec has no "inputs": a v1 model always takes encoder.py's six arrays (batch left out)
const V1_INPUTS = [
  { name: "mon_ids", dtype: "int64", shape: [12, 3] },
  { name: "move_ids", dtype: "int64", shape: [12, 4] },
  { name: "mon_feats", dtype: "float32", shape: [12, 84] },
  { name: "move_feats", dtype: "float32", shape: [12, 4, 39] },
  { name: "field", dtype: "float32", shape: [81] },
  { name: "mask", dtype: "bool", shape: [14] },
];
const TYPE_COLORS = {
  BUG: "#92bc2c", DARK: "#595761", DRAGON: "#0c69c8", ELECTRIC: "#f2d94e", FAIRY: "#ee90e6",
  FIGHTING: "#d3425f", FIRE: "#fba54c", FLYING: "#a1bbec", GHOST: "#5f6dbc", GRASS: "#5fbd58",
  GROUND: "#da7c4d", ICE: "#75d0c1", NORMAL: "#a0a29f", POISON: "#b763cf", PSYCHIC: "#fa8581",
  ROCK: "#c9bb8a", STEEL: "#5695a3", WATER: "#539ddf", THREE_QUESTION_MARKS: "#68a090", STELLAR: "#40b5a5",
};

const app = {
  models: [],
  fixtureFolders: new Map(),   // model folder -> fixture file names
  fixtureSource: new Map(),    // fixture file name -> model folder that recorded it
  model: null,                 // { name, spec, layout, vocab, session, encoderVersion }
  fixture: null,               // { name, source, data, encoderVersion }
  index: 0,
  original: null,              // decision arrays as recorded (typed arrays by input name)
  edited: null,                // the same arrays, edited
  originalOutput: null,        // { logits, value } of the unedited arrays, in this browser
  editedOutput: null,
  changes: [],
  temperature: 1,
  modelToken: 0,
  runToken: 0,
  lastRunMs: null,
};
let controls = [];
let actionRows = [];
let editTimer = null;
let runChain = Promise.resolve();
const openEditors = new Set();

// ---------------------------------------------------------------- helpers

const $ = (id) => document.getElementById(id);
const esc = (s) => String(s).replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
const num = (array, i) => Number(array[i]);
const product = (shape) => shape.reduce((a, b) => a * b, 1);
const clamp = (v, lo, hi) => Math.min(hi, Math.max(lo, v));
const pretty = (name) => name === "THREE_QUESTION_MARKS" ? "???"
  : name.charAt(0) + name.slice(1).toLowerCase().replace(/_/g, " ");
const identName = (ident) => (ident ?? "").replace(/^p\d[a-d]?: ?/, "");
const nanAsInfinity = (x) => Number.isNaN(x) ? Infinity : x;

function fmtDiff(d) {
  if (d === Infinity) return "NaN";
  if (d === 0) return "0";
  return d >= 1e-3 ? d.toFixed(3) : d.toExponential(1);
}

function fmtValue(v, digits = 3) {
  if (!Number.isFinite(v)) return String(v);
  const rounded = Number(v.toFixed(digits));
  return String(rounded === 0 ? 0 : rounded);
}

function h(tag, attrs = {}, ...children) {
  const el = document.createElement(tag);
  for (const [key, value] of Object.entries(attrs)) {
    if (value == null || value === false) continue;
    if (key === "class") el.className = value;
    else if (key === "text") el.textContent = value;
    else el.setAttribute(key, value === true ? "" : value);
  }
  for (const child of children.flat()) if (child != null) el.append(child);
  return el;
}

function unravel(shape, index) {
  const out = [];
  for (let k = shape.length - 1; k >= 0; k--) {
    out.unshift(index % shape[k]);
    index = Math.floor(index / shape[k]);
  }
  return out;
}

function ravel(shape, indices) {
  let flat = 0;
  for (let k = 0; k < shape.length; k++) flat = flat * shape[k] + indices[k];
  return flat;
}

function setStatus(text) {
  $("status").textContent = text;
}

function showBanner(message, kind = "error") {
  const banner = $("banner");
  banner.className = `banner ${kind}`;
  banner.textContent = "";
  banner.append(h("span", { text: message }), h("button", { type: "button", class: "banner-close", text: "dismiss" }));
  banner.querySelector("button").addEventListener("click", () => { banner.hidden = true; });
  banner.hidden = false;
}

async function guarded(action) {
  try {
    return await action();
  } catch (error) {
    console.error(error);
    showBanner(error.message ?? String(error));
    setStatus("");
    return undefined;
  }
}

// ---------------------------------------------------------------- files

async function fetchJson(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}: ${url}`);
  return response.json();
}

// http.server's directory listing: one <a href> per entry, folders end with "/"
async function listDirectory(url) {
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}: ${url}`);
  const doc = new DOMParser().parseFromString(await response.text(), "text/html");
  return [...doc.querySelectorAll("a[href]")].map((a) => decodeURIComponent(a.getAttribute("href")));
}

async function parseFixtureBytes(buffer) {
  const bytes = new Uint8Array(buffer);
  let text;
  if (bytes[0] === 0x1f && bytes[1] === 0x8b) {
    const stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream("gzip"));
    text = await new Response(stream).text();
  } else {
    text = new TextDecoder().decode(bytes);  // a server that already decoded it, or plain .json
  }
  return JSON.parse(text);
}

async function fetchFixture(model, file) {
  const url = new URL(encodeURIComponent(file), fixturesUrl(model));
  const response = await fetch(url);
  if (!response.ok) throw new Error(`${response.status} ${response.statusText}: ${url}`);
  return parseFixtureBytes(await response.arrayBuffer());
}

// ---------------------------------------------------------------- spec and feature layout

function sections(list) {
  const labels = [];
  const offsets = {};
  for (const [key, names] of list) {
    offsets[key] = labels.length;
    labels.push(...names);
  }
  return { labels, offsets };
}

function invertVocab(map) {
  const names = [];
  for (const [name, id] of Object.entries(map)) names[id] = name;
  return names;
}

// Feature labels, in encoder.py's order (_mon_features, _move_features, the field tail
// of encode()) and encoder_v2.py's; enum names come from the spec's orderings.
function buildLayout(spec) {
  const o = spec.orderings;
  const inputs = (spec.inputs
    ? spec.inputs.map((input) => ({ name: input.name, dtype: input.dtype, shape: input.shape.slice(1) }))
    : V1_INPUTS.map((input) => ({ ...input, shape: [...input.shape] })))
    .map((input) => ({ ...input, size: product(input.shape) }));
  const byName = Object.fromEntries(inputs.map((input) => [input.name, input]));

  const mon = sections([
    ["present", ["present"]], ["placeholder", ["unrevealed placeholder"]], ["mine", ["is ours"]],
    ["active", ["active"]], ["fainted", ["fainted"]], ["hp", ["hp fraction"]],
    ["status", o.statuses.map((s) => `status ${s}`)],
    ["boosts", o.boosts.map((b) => `boost ${b} / 6`)],
    ["types", o.types.map((t) => `type ${pretty(t)}`)],
    ["tera", o.types.map((t) => `tera type ${pretty(t)}`)],
    ["terastallized", ["terastallized"]],
    ["stats", o.stats.map((s) => `base ${s} / 200`)],
    ["level", ["level / 100"]],
    ["itemKnown", ["item known"]], ["abilityKnown", ["ability known"]],
    ["effects", o.effects.map((e) => `effect ${pretty(e)}`)],
    ["mustRecharge", ["must recharge"]], ["firstTurn", ["first turn"]], ["preparing", ["preparing"]],
    ["knownMoves", ["known moves (max 4) / 4"]],
  ]);
  const move = sections([
    ["present", ["present"]], ["power", ["base power (max 250) / 150"]], ["accuracy", ["accuracy"]],
    ["pp", ["pp fraction"]], ["priority", ["priority (-7 to 5) / 5"]],
    ["category", o.categories.map((c) => `category ${pretty(c)}`)],
    ["type", o.types.map((t) => `type ${pretty(t)}`)],
    ["effectiveness", ["log2 effectiveness vs opposing active (max 2) / 2"]], ["immune", ["immune"]],
    ["stab", ["STAB"]],
    ["heal", ["heal"]], ["drain", ["drain"]], ["recoil", ["recoil"]], ["protect", ["protect move"]],
    ["pivot", ["self switch"]], ["sideCondition", ["sets a side condition"]],
    ["inflictsStatus", ["inflicts a status"]], ["forceSwitch", ["forces a switch"]],
  ]);
  const field = sections([
    ["weather", o.weathers.map((w) => `weather ${pretty(w)}`)],
    ["fields", o.fields.map((f) => `field ${pretty(f)}`)],
    ["ourSide", o.side_conditions.map((c) => `our side ${pretty(c)}`)],
    ["theirSide", o.side_conditions.map((c) => `their side ${pretty(c)}`)],
    ["turn", ["turn (max 50) / 50"]], ["canTera", ["can tera"]], ["usedTera", ["used tera"]],
    ["oppUsedTera", ["opponent used tera"]], ["forceSwitch", ["force switch"]], ["trapped", ["trapped"]],
    ["ourAlive", ["our alive / 6"]], ["theirAlive", ["their not fainted / 6"]],
    ["theirRevealed", ["their revealed / 6"]],
  ]);

  const errors = [];
  const expect = (what, actual, expected) => {
    if (actual !== expected) errors.push(`${what}: ${actual} labels, expected ${expected}`);
  };
  expect("mon_feats (f_mon)", mon.labels.length, spec.f_mon);
  expect("move_feats (f_move)", move.labels.length, spec.f_move);
  expect("field (f_field)", field.labels.length, spec.f_field);
  expect("mon_feats input", byName.mon_feats?.shape[1], spec.f_mon);
  expect("move_feats input", byName.move_feats?.shape[2], spec.f_move);
  expect("field input", byName.field?.shape[0], spec.f_field);

  // v2: "cand_feats": ["V1 move features x 39", "fraction"], "prior_feats": ["tera weight x 20 ...", ...]
  let cand = null;
  let prior = null;
  let hist = null;
  if (byName.cand_feats) {
    const [first, ...rest] = o.cand_feats ?? [];
    expect("cand_feats move features", Number(/x (\d+)/.exec(first ?? "")?.[1]), move.labels.length);
    cand = [...move.labels, ...rest.map((name) => name === "fraction" ? "fraction of consistent roles" : name)];
    expect("cand_feats input", byName.cand_feats.shape[2], cand.length);
  }
  if (byName.prior_feats) {
    const [first, ...rest] = o.prior_feats ?? [];
    expect("prior_feats tera weights", Number(/x (\d+)/.exec(first ?? "")?.[1]), o.types.length);
    prior = [...o.types.map((t) => `tera weight ${pretty(t)}`), ...rest];
    expect("prior_feats input", byName.prior_feats.shape[1], prior.length);
  }
  if (byName.hist_feats) {
    hist = o.hist_feats ?? [];
    expect("hist_feats input", byName.hist_feats.shape[0], hist.length);
  }
  if (errors.length) throw new Error(`The feature layout does not add up for this spec: ${errors.join("; ")}.`);
  return { inputs, byName, mon, move, field, cand, prior, hist, orderings: o };
}

async function loadModel(name) {
  const base = new URL(`${encodeURIComponent(name)}/`, MODELS_URL);
  const spec = await fetchJson(new URL("encoder_spec.json", base));
  const layout = buildLayout(spec);
  const session = await ort.InferenceSession.create(new URL("minipoke.onnx", base).href, { executionProviders: ["wasm"] });
  const expected = layout.inputs.map((input) => input.name).sort().join(",");
  if ([...session.inputNames].sort().join(",") !== expected || !session.outputNames.includes("logits")
      || !session.outputNames.includes("value")) {
    await session.release();
    throw new Error(`${name}: the ONNX graph takes ${session.inputNames.join(", ")} and returns `
      + `${session.outputNames.join(", ")}, the spec lists ${expected}.`);
  }
  const vocab = {
    species: invertVocab(spec.vocab.species),
    move: invertVocab(spec.vocab.move),
    ability: invertVocab(spec.vocab.ability),
  };
  return { name, spec, layout, vocab, session, encoderVersion: spec.encoder_version ?? 1 };
}

// ids: 0 = padding, 1 = unknown
function vocabName(names, id) {
  if (id === 0) return "(none)";
  if (id === 1) return "(unknown)";
  const name = names[id];
  if (name === undefined) return `#${id}`;
  return name === "" ? "(blank)" : name;
}

const speciesName = (id) => vocabName(app.model.vocab.species, id);
const moveName = (id) => vocabName(app.model.vocab.move, id);
const abilityName = (id) => vocabName(app.model.vocab.ability, id);

// items are hashed (spec "item_hash"), so only their id is known
function itemName(id) {
  if (id === 0) return "no item";
  if (id === 1) return "item unknown";
  return `item #${id}`;
}

// ---------------------------------------------------------------- arrays and inference

function toTyped(dtype, flat) {
  switch (dtype) {
    case "int64": return BigInt64Array.from(flat, (v) => BigInt(v));
    case "bool": return Uint8Array.from(flat, (v) => (v ? 1 : 0));
    case "float32": return Float32Array.from(flat);
    default: throw new Error(`unsupported input dtype ${dtype}`);
  }
}

function decisionArrays(decision, inputs) {
  const arrays = {};
  for (const input of inputs) {
    const flat = decision[input.name];
    if (!Array.isArray(flat)) throw new Error(`The decision has no "${input.name}" array, which the model needs.`);
    if (flat.length !== input.size) {
      throw new Error(`"${input.name}" has ${flat.length} values, the model expects [${input.shape.join(", ")}] = ${input.size}.`);
    }
    arrays[input.name] = toTyped(input.dtype, flat);
  }
  return arrays;
}

function cloneArrays(arrays) {
  return Object.fromEntries(Object.entries(arrays).map(([name, array]) => [name, array.slice()]));
}

function writeCell(array, index, value) {
  if (array instanceof BigInt64Array) array[index] = BigInt(Math.round(Number(value)));
  else if (array instanceof Uint8Array) array[index] = Number(value) ? 1 : 0;
  else array[index] = Number(value);
}

// One run at a time on a session: runs queue up in order
function runModel(model, arrays) {
  const job = runChain.then(async () => {
    const feeds = {};
    for (const input of model.layout.inputs) {
      feeds[input.name] = new ort.Tensor(input.dtype, arrays[input.name].slice(), [1, ...input.shape]);
    }
    const start = performance.now();
    const results = await model.session.run(feeds);
    app.lastRunMs = performance.now() - start;
    return { logits: Array.from(results.logits.data), value: Number(results.value.data[0]) };
  });
  runChain = job.catch(() => undefined);
  return job;
}

function compareRecorded(decision, output) {
  if (!Array.isArray(decision.logits) || typeof decision.value !== "number") return null;
  let logit = 0;
  decision.mask.forEach((legal, a) => {
    if (legal) logit = Math.max(logit, nanAsInfinity(Math.abs(output.logits[a] - decision.logits[a])));
  });
  return { logit, value: nanAsInfinity(Math.abs(output.value - decision.value)) };
}

function softmax(logits, mask, temperature) {
  const probs = new Array(logits.length).fill(0);
  const legal = [];
  for (let a = 0; a < logits.length; a++) if (mask[a] && logits[a] > MASKED_LOGIT) legal.push(a);
  if (!legal.length) return probs;
  const max = Math.max(...legal.map((a) => logits[a] / temperature));
  let sum = 0;
  for (const a of legal) {
    probs[a] = Math.exp(logits[a] / temperature - max);
    sum += probs[a];
  }
  for (const a of legal) probs[a] /= sum;
  return probs;
}

function argmax(logits, mask) {
  let best = -1;
  for (let a = 0; a < logits.length; a++) {
    if (mask[a] && logits[a] > MASKED_LOGIT && (best < 0 || logits[a] > logits[best])) best = a;
  }
  return best;
}

// ---------------------------------------------------------------- model and fixture selection

async function selectModel(name) {
  const token = ++app.modelToken;
  setStatus(`loading ${name}...`);
  const model = await loadModel(name);
  if (token !== app.modelToken) {
    await model.session.release();
    return;
  }
  const previous = app.model;
  app.model = model;
  if (previous) runChain.then(() => previous.session.release());
  $("model-select").value = name;
  buildRawEditor();
  $("check-result").textContent = "";
  if (app.fixture && app.fixture.encoderVersion === model.encoderVersion) {
    fillFixtureSelect();
    await showDecision(app.index);
    return;
  }
  const dropped = app.fixture;
  app.fixture = null;
  fillFixtureSelect();
  const files = app.fixtureFolders.get(name) ?? [];
  if (files.length) await loadFixtureByName(files[0]);
  else clearDecisionView(`No fixtures folder for ${name}: drop a fixture (.json.gz) on the page.`);
  if (dropped) {
    showBanner(`${dropped.name} was encoded with encoder version ${dropped.encoderVersion}, `
      + `${name} uses version ${model.encoderVersion}: loaded one of ${name}'s fixtures instead.`, "warn");
  }
}

async function loadFixtureByName(file) {
  const model = app.model;
  setStatus(`loading ${file}...`);
  const data = await fetchFixture(model.name, file);
  if (model !== app.model) return;
  await acceptFixture(data, file, model.name);
}

async function loadFile(file) {
  setStatus(`reading ${file.name}...`);
  const data = await parseFixtureBytes(await file.arrayBuffer());
  await acceptFixture(data, file.name, app.fixtureSource.get(file.name) ?? null);
}

async function acceptFixture(data, name, source) {
  if (!Array.isArray(data?.decisions) || !Array.isArray(data?.lines)) {
    throw new Error(`${name} is not a parity fixture (no "lines" and "decisions").`);
  }
  if (!data.decisions.length) throw new Error(`${name} has no decisions.`);
  const encoderVersion = data.encoder_version ?? 1;
  if (encoderVersion !== app.model.encoderVersion) {
    fillFixtureSelect();
    setStatus("");
    throw new Error(`Refused ${name}: it was encoded with encoder version ${encoderVersion}, `
      + `${app.model.name} uses version ${app.model.encoderVersion}.`);
  }
  decisionArrays(data.decisions[0], app.model.layout.inputs);  // throws when an input is missing
  app.fixture = { name, source, data, encoderVersion };
  $("banner").hidden = true;
  $("check-result").textContent = "";
  fillFixtureSelect();
  await showDecision(0);
}

function fillFixtureSelect() {
  const select = $("fixture-select");
  select.textContent = "";
  const files = app.fixtureFolders.get(app.model?.name) ?? [];
  const loaded = app.fixture;
  if (loaded && (loaded.source !== app.model.name || !files.includes(loaded.name))) {
    const from = loaded.source ? `from ${loaded.source}` : "dropped";
    select.append(h("option", { value: "", text: `${loaded.name.replace(/\.json(\.gz)?$/, "")} (${from})` }));
  }
  for (const file of files) select.append(h("option", { value: file, text: file.replace(/\.json\.gz$/, "") }));
  if (loaded && loaded.source === app.model.name && files.includes(loaded.name)) select.value = loaded.name;
  else select.value = "";
  select.disabled = !select.options.length;
}

function clearDecisionView(message) {
  app.original = app.edited = app.originalOutput = app.editedOutput = null;
  for (const id of ["log", "field", "ours", "theirs", "actions", "value", "parity", "nav-info", "policy-info"]) {
    $(id).textContent = "";
  }
  $("nav-info").textContent = message;
  setStatus("");
}

async function showDecision(index) {
  const { model, fixture } = app;
  if (!fixture) return;
  const decisions = fixture.data.decisions;
  app.index = clamp(Math.round(index), 0, decisions.length - 1);
  const decision = decisions[app.index];
  app.original = decisionArrays(decision, model.layout.inputs);
  app.edited = cloneArrays(app.original);
  app.changes = [];
  app.originalOutput = app.editedOutput = null;
  clearTimeout(editTimer);
  controls = [];
  renderNavigator();
  renderLog();
  buildStatePanel();
  buildActionRows();
  renderEditSummary();
  renderRawInfo();
  const token = ++app.runToken;
  setStatus("running...");
  const output = await runModel(model, app.original);
  if (token !== app.runToken || model !== app.model) return;
  app.originalOutput = app.editedOutput = output;
  renderOutputs();
  renderParity();
  setStatus(`${model.name}: ${app.lastRunMs.toFixed(1)} ms per run`);
}

// ---------------------------------------------------------------- edits

function diffCells() {
  const changes = [];
  for (const input of app.model.layout.inputs) {
    const before = app.original[input.name];
    const after = app.edited[input.name];
    for (let i = 0; i < input.size; i++) {
      if (before[i] !== after[i]) changes.push({ name: input.name, index: i, from: num(before, i), to: num(after, i) });
    }
  }
  return changes;
}

function afterEdit({ sync = false } = {}) {
  app.changes = diffCells();
  if (sync) syncControls();
  renderStateViews();
  renderEditSummary();
  renderRawInfo();
  clearTimeout(editTimer);
  editTimer = setTimeout(runEdited, EDIT_DEBOUNCE_MS);
}

async function runEdited() {
  const { model } = app;
  const token = ++app.runToken;
  setStatus("running...");
  const output = await runModel(model, app.edited);
  if (token !== app.runToken || model !== app.model) return;
  app.editedOutput = output;
  renderOutputs();
  setStatus(`${model.name}: ${app.lastRunMs.toFixed(1)} ms per run`);
}

function resetEdits() {
  if (!app.original) return;
  app.edited = cloneArrays(app.original);
  afterEdit({ sync: true });
}

// A control bound to cells of app.edited: read() gives the control's value from the
// arrays, write(value) stores it. Controls are rebuilt with each decision.
function bind(el, read, write, prop = "value") {
  controls.push({ el, read, prop });
  el[prop] = read();
  const event = el.type === "range" ? "input" : "change";
  el.addEventListener(event, () => {
    write(prop === "checked" ? el.checked : el.value);
    afterEdit();
  });
  return el;
}

function syncControls() {
  for (const control of controls) control.el[control.prop] = control.read();
}

function onehotRead(array, base, indices) {
  const hit = indices.find((i) => array[base + i] > 0.5);
  return hit === undefined ? "" : String(hit);
}

function onehotWrite(array, base, indices, value) {
  for (const i of indices) array[base + i] = 0;
  if (value !== "") array[base + Number(value)] = 1;
}

function selectOf(options, extraClass) {
  return h("select", { class: extraClass }, options.map(([value, text]) => h("option", { value, text })));
}

// ---------------------------------------------------------------- navigator and log

function lastTurn(lines, count) {
  for (let i = count - 1; i >= 0; i--) {
    if (lines[i].startsWith("|turn|")) return { index: i, turn: Number(lines[i].split("|")[2]) };
  }
  return { index: -1, turn: 0 };
}

function renderNavigator() {
  const { data } = app.fixture;
  const decisions = data.decisions;
  const slider = $("decision-slider");
  slider.max = String(decisions.length - 1);
  slider.value = String(app.index);
  $("prev").disabled = app.index === 0;
  $("next").disabled = app.index === decisions.length - 1;
  const decision = decisions[app.index];
  const { turn } = lastTurn(data.lines, decision.line_count);
  const forced = app.original.field[app.model.layout.field.offsets.forceSwitch] > 0.5;
  $("nav-info").innerHTML = [
    `<b>decision ${app.index + 1} / ${decisions.length}</b>`,
    `turn ${turn}${forced ? " (force switch)" : ""}`,
    esc(data.battle_tag ?? "?"),
    `<span class="mon ours">${esc(data.player_role ?? "?")}</span> ${esc(data.username ?? "?")}`,
  ].join(" &middot; ");
}

function renderLog() {
  const { data } = app.fixture;
  const decisions = data.decisions;
  const count = decisions[app.index].line_count;
  const previousCount = app.index > 0 ? decisions[app.index - 1].line_count : 0;
  const current = lastTurn(data.lines, count).index;
  const role = data.player_role;
  const html = [];
  for (let i = 0; i < count; i++) {
    const parts = data.lines[i].split("|");
    const kind = parts.length > 1 ? parts[1] : "";
    if (kind === "" || kind === "t:") continue;
    const classes = ["ln"];
    if (kind === "request") classes.push("req");
    if (kind === "turn") classes.push("turn");
    if (META_KINDS.has(kind)) classes.push("meta");
    if (current >= 0 && i >= current) classes.push("cur");
    if (i >= previousCount) classes.push("new");
    html.push(`<div class="${classes.join(" ")}" title="line ${i}">${formatLine(parts, role)}</div>`);
  }
  const log = $("log");
  log.innerHTML = html.join("");
  log.scrollTop = log.scrollHeight;
}

const META_KINDS = new Set(["init", "title", "j", "l", "gametype", "player", "teamsize", "gen", "tier", "rule",
  "start", "deinit", "upkeep", "-anim", "-hint", "n"]);

function formatLine(parts, role) {
  const kind = parts[1];
  const p = parts.slice(2);
  const mon = (s) => {
    const m = /^(p[1-4])([a-d]?): ?(.*)$/.exec(s ?? "");
    if (!m) return esc(s ?? "");
    return `<span class="mon ${m[1] === role ? "ours" : "theirs"}">${esc(m[3])}</span>`;
  };
  const side = (s) => {
    const m = /^(p[1-4])/.exec(s ?? "");
    if (!m) return esc(s ?? "");
    return `<span class="mon ${m[1] === role ? "ours" : "theirs"}">${m[1] === role ? "our side" : "their side"}</span>`;
  };
  const tags = (rest) => rest.filter(Boolean).map((tag) => {
    const of = /^\[of\] (.*)$/.exec(tag);
    if (of) return `<span class="tag">of ${mon(of[1])}</span>`;
    return `<span class="tag">${esc(tag.replace(/^\[(\w+)\] ?/, "$1 "))}</span>`;
  }).join(" ");
  const effect = (s) => esc((s ?? "").replace(/^(move|ability|item): /, ""));
  switch (kind) {
    case "turn": return `Turn ${esc(p[0])}`;
    case "move": return `${mon(p[0])} used <b>${esc(p[1])}</b>${p[2] ? ` on ${mon(p[2])}` : ""} ${tags(p.slice(3))}`;
    case "switch": return `${mon(p[0])} switched in <span class="dim">${esc(p[1])}</span> ${esc(p[2] ?? "")}`;
    case "drag": return `${mon(p[0])} was dragged in <span class="dim">${esc(p[1])}</span> ${esc(p[2] ?? "")}`;
    case "replace": return `${mon(p[0])} was revealed as ${esc(p[1])}`;
    case "-damage": return `${mon(p[0])} took damage: ${esc(p[1])} ${tags(p.slice(2))}`;
    case "-heal": return `${mon(p[0])} healed: ${esc(p[1])} ${tags(p.slice(2))}`;
    case "-sethp": return `${mon(p[0])} hp set to ${esc(p[1])} ${tags(p.slice(2))}`;
    case "faint": return `<b>${mon(p[0])} fainted</b>`;
    case "-supereffective": return `super effective on ${mon(p[0])}`;
    case "-resisted": return `${mon(p[0])} resisted`;
    case "-immune": return `${mon(p[0])} is immune ${tags(p.slice(1))}`;
    case "-crit": return `critical hit on ${mon(p[0])}`;
    case "-miss": return `${mon(p[0])} missed${p[1] ? ` ${mon(p[1])}` : ""}`;
    case "-fail": return `${mon(p[0])}: it failed ${effect(p[1])} ${tags(p.slice(2))}`;
    case "-boost": return `${mon(p[0])} ${esc(p[1])} +${esc(p[2])} ${tags(p.slice(3))}`;
    case "-unboost": return `${mon(p[0])} ${esc(p[1])} -${esc(p[2])} ${tags(p.slice(3))}`;
    case "-setboost": return `${mon(p[0])} ${esc(p[1])} set to ${esc(p[2])} ${tags(p.slice(3))}`;
    case "-clearallboost": return "every boost cleared";
    case "-clearnegativeboost": return `${mon(p[0])} negative boosts cleared ${tags(p.slice(1))}`;
    case "-weather": return p[0] === "none" ? "the weather ended"
      : `weather: <b>${esc(p[0])}</b> ${p.includes("[upkeep]") ? "(continues)" : tags(p.slice(1))}`;
    case "-fieldstart": return `field started: <b>${effect(p[0])}</b> ${tags(p.slice(1))}`;
    case "-fieldend": return `field ended: ${effect(p[0])}`;
    case "-sidestart": return `${side(p[0])}: <b>${effect(p[1])}</b> started`;
    case "-sideend": return `${side(p[0])}: ${effect(p[1])} ended ${tags(p.slice(2))}`;
    case "-status": return `${mon(p[0])} is now <b>${esc(p[1])}</b> ${tags(p.slice(2))}`;
    case "-curestatus": return `${mon(p[0])} was cured of ${esc(p[1])} ${tags(p.slice(2))}`;
    case "-terastallize": return `${mon(p[0])} terastallized into <b>${esc(p[1])}</b>`;
    case "-ability": return `${mon(p[0])} ability: ${esc(p[1])} ${tags(p.slice(2))}`;
    case "-item": return `${mon(p[0])} item: ${esc(p[1])} ${tags(p.slice(2))}`;
    case "-enditem": return `${mon(p[0])} lost its ${esc(p[1])} ${tags(p.slice(2))}`;
    case "-activate": return `${mon(p[0])}: ${effect(p[1])} ${tags(p.slice(2))}`;
    case "-start": return `${mon(p[0])}: ${effect(p[1])} started ${tags(p.slice(2))}`;
    case "-end": return `${mon(p[0])}: ${effect(p[1])} ended ${tags(p.slice(2))}`;
    case "-singleturn":
    case "-singlemove": return `${mon(p[0])}: ${effect(p[1])}`;
    case "cant": return `${mon(p[0])} can't move: ${effect(p[1])}${p[2] ? ` (${esc(p[2])})` : ""}`;
    case "-hitcount": return `hit ${esc(p[1])} times`;
    case "-formechange":
    case "detailschange": return `${mon(p[0])} changed to ${esc(p[1])}`;
    case "-transform": return `${mon(p[0])} transformed into ${mon(p[1])}`;
    case "-prepare": return `${mon(p[0])} is preparing ${esc(p[1])}`;
    case "win": return `<b>${esc(p[0])} won</b>`;
    case "player": return p[1] ? `${esc(p[0])} is ${esc(p[1])}` : esc(p[0]);
    case "request": return formatRequest(p.join("|"));
    default: return `${esc(kind)} ${p.map((part) => (/^p[1-4][a-d]?: /.test(part) ? mon(part) : esc(part))).join(" ")}`;
  }
}

function formatRequest(json) {
  let summary = "request";
  try {
    const request = JSON.parse(json);
    if (request.wait) summary = "request: wait";
    else if (request.teamPreview) summary = "request: team preview";
    else if (request.forceSwitch) summary = "request: force switch";
    else if (request.active) summary = "request: choose a move or a switch";
    if (request.rqid != null) summary += ` (rqid ${request.rqid})`;
    json = JSON.stringify(request, null, 1);
  } catch {
    // not JSON: show it raw
  }
  return `<details><summary>${esc(summary)}</summary><pre>${esc(json)}</pre></details>`;
}

// ---------------------------------------------------------------- state panel

function monLabel(row) {
  const ours = row < N_SLOTS;
  const slot = ours ? row : row - N_SLOTS;
  const idents = app.fixture.data.decisions[app.index].team_idents ?? [];
  const name = ours && idents[slot] ? identName(idents[slot]) : speciesName(num(app.edited.mon_ids, row * 3));
  return `${ours ? "our" : "their"} ${slot} (${name})`;
}

function buildStatePanel() {
  const role = app.fixture.data.player_role ?? "?";
  $("ours-title").textContent = `Our team (${role}, slot = switch action)`;
  $("theirs-title").textContent = "Their team (revealed order)";
  const ours = $("ours");
  const theirs = $("theirs");
  ours.textContent = "";
  theirs.textContent = "";
  for (let row = 0; row < 2 * N_SLOTS; row++) {
    const card = h("div", { class: "card", "data-row": String(row) }, h("div", { class: "card-view" }));
    const { labels, offsets } = app.model.layout.mon;
    const present = num(app.edited.mon_feats, row * labels.length + offsets.present) > 0.5;
    if (present) card.append(buildMonEditor(row));
    (row < N_SLOTS ? ours : theirs).append(card);
  }
  const field = $("field");
  field.textContent = "";
  field.append(h("div", { class: "field-view" }), buildFieldEditor());
  renderStateViews();
}

function buildMonEditor(row) {
  const { layout } = app.model;
  const F = layout.mon.labels.length;
  const o = layout.mon.offsets;
  const ord = layout.orderings;
  const feats = () => app.edited.mon_feats;
  const base = row * F;

  const hp = h("input", { type: "range", min: "0", max: "1", step: "0.01", "aria-label": "hp fraction" });
  bind(hp, () => String(num(feats(), base + o.hp)), (v) => { feats()[base + o.hp] = Number(v); });

  const statuses = ord.statuses.map((_, i) => i);
  const status = selectOf([["", "no status"], ...ord.statuses.map((s, i) => [String(i), s])]);
  bind(status, () => onehotRead(feats(), base + o.status, statuses),
    (v) => onehotWrite(feats(), base + o.status, statuses, v));

  const boosts = ord.boosts.map((name, i) => {
    const options = [];
    for (let stage = 6; stage >= -6; stage--) options.push([String(stage), stage > 0 ? `+${stage}` : String(stage)]);
    const select = selectOf(options, "boost");
    bind(select, () => String(Math.round(num(feats(), base + o.boosts + i) * 6)),
      (v) => { feats()[base + o.boosts + i] = Number(v) / 6; });
    return h("label", { class: "boost-field" }, name, select);
  });

  const details = h("details", { class: "card-edit" },
    h("summary", { text: "edit" }),
    h("div", { class: "edit-row" }, h("label", { class: "hp-edit" }, "hp", hp), h("label", {}, "status ", status)),
    h("div", { class: "edit-row boosts" }, boosts));
  details.open = openEditors.has(row);
  details.addEventListener("toggle", () => {
    if (details.open) openEditors.add(row);
    else openEditors.delete(row);
  });
  return details;
}

function buildFieldEditor() {
  const { layout } = app.model;
  const o = layout.field.offsets;
  const ord = layout.orderings;
  const field = () => app.edited.field;

  const weathers = ord.weathers.map((_, i) => i);
  const weather = selectOf([["", "none"], ...ord.weathers.map((w, i) => [String(i), pretty(w)])]);
  bind(weather, () => onehotRead(field(), o.weather, weathers), (v) => onehotWrite(field(), o.weather, weathers, v));

  const terrains = ord.fields.map((name, i) => [name, i]).filter(([name]) => name.endsWith("_TERRAIN")).map(([, i]) => i);
  const terrain = selectOf([["", "none"], ...terrains.map((i) => [String(i), pretty(ord.fields[i])])]);
  bind(terrain, () => onehotRead(field(), o.fields, terrains), (v) => onehotWrite(field(), o.fields, terrains, v));

  const otherFields = ord.fields.map((name, i) => [name, i]).filter(([name]) => !name.endsWith("_TERRAIN"))
    .map(([name, i]) => {
      const box = h("input", { type: "checkbox" });
      bind(box, () => field()[o.fields + i] > 0.5, (v) => { field()[o.fields + i] = v ? 1 : 0; }, "checked");
      return h("label", { class: "check" }, box, pretty(name));
    });

  const sideEditor = (offset, title) => h("div", { class: "side-edit" }, h("b", { text: title }),
    SIDE_CONTROLS.filter((name) => ord.side_conditions.includes(name)).map((name) => {
      const i = offset + ord.side_conditions.indexOf(name);
      const cap = STACKABLE[name];
      if (cap) {
        const layers = [];
        for (let n = 0; n <= cap; n++) layers.push([String(n), String(n)]);
        const select = selectOf(layers);
        bind(select, () => String(Math.round(field()[i] * cap)), (v) => { field()[i] = Number(v) / cap; });
        return h("label", { class: "check" }, pretty(name), select);
      }
      const box = h("input", { type: "checkbox" });
      bind(box, () => field()[i] > 0.5, (v) => { field()[i] = v ? 1 : 0; }, "checked");
      return h("label", { class: "check" }, box, pretty(name));
    }));

  const details = h("details", { class: "field-edit" },
    h("summary", { text: "edit weather, terrain, hazards and screens" }),
    h("div", { class: "edit-row" }, h("label", {}, "weather ", weather), h("label", {}, "terrain ", terrain)),
    h("div", { class: "edit-row checks" }, otherFields),
    h("div", { class: "edit-row sides" }, sideEditor(o.ourSide, "our side"), sideEditor(o.theirSide, "their side")));
  details.open = openEditors.has("field");
  details.addEventListener("toggle", () => {
    if (details.open) openEditors.add("field");
    else openEditors.delete("field");
  });
  return details;
}

function renderStateViews() {
  const changedRows = new Set();
  const changedField = app.changes.some((change) => change.name === "field" || change.name.startsWith("hist"));
  for (const change of app.changes) {
    const row = rowOfCell(change.name, change.index);
    if (row != null) changedRows.add(row);
  }
  for (const card of document.querySelectorAll(".card")) {
    const row = Number(card.dataset.row);
    const f = (c) => num(app.edited.mon_feats, row * app.model.layout.mon.labels.length + c);
    const o = app.model.layout.mon.offsets;
    card.classList.toggle("ours", row < N_SLOTS);
    card.classList.toggle("theirs", row >= N_SLOTS);
    card.classList.toggle("active", f(o.present) > 0.5 && f(o.active) > 0.5);
    card.classList.toggle("fainted", f(o.present) > 0.5 && f(o.fainted) > 0.5);
    card.classList.toggle("absent", f(o.present) < 0.5);
    card.classList.toggle("edited", changedRows.has(row));
    card.querySelector(".card-view").innerHTML = monView(row);
  }
  const fieldView = document.querySelector(".field-view");
  if (fieldView) fieldView.innerHTML = renderFieldView();
  $("field").classList.toggle("edited", changedField);
}

// Which Pokémon row a cell belongs to (null for field-level inputs)
function rowOfCell(name, index) {
  const input = app.model.layout.byName[name];
  const at = unravel(input.shape, index);
  if (["mon_ids", "move_ids", "mon_feats", "move_feats"].includes(name)) return at[0];
  if (["cand_ids", "cand_feats", "prior_ids", "prior_w", "prior_feats"].includes(name)) return N_SLOTS + at[0];
  return null;
}

function typeChip(name) {
  return `<span class="type"><i style="background:${TYPE_COLORS[name] ?? "#888"}"></i>${esc(pretty(name))}</span>`;
}

function monView(row) {
  const { layout } = app.model;
  const arrays = app.edited;
  const F = layout.mon.labels.length;
  const o = layout.mon.offsets;
  const ord = layout.orderings;
  const f = (c) => num(arrays.mon_feats, row * F + c);
  const ours = row < N_SLOTS;
  const slot = ours ? row : row - N_SLOTS;
  const ids = [0, 1, 2].map((c) => num(arrays.mon_ids, row * 3 + c));
  const slotTag = `<span class="slot">${ours ? "slot" : "opp"} ${slot}</span>`;
  if (f(o.present) < 0.5) {
    const what = f(o.placeholder) > 0.5 ? "unrevealed" : "empty";
    return `<div class="mon-head">${slotTag}<span class="mon-name dim">${what}</span></div>`;
  }
  const idents = app.fixture.data.decisions[app.index].team_idents ?? [];
  const species = speciesName(ids[0]);
  const name = ours && idents[slot] ? identName(idents[slot]) : species;
  const hp = clamp(f(o.hp), 0, 1);
  const hpClass = hp > 0.5 ? "high" : hp > 0.2 ? "mid" : "low";
  const statusIndex = ord.statuses.findIndex((_, i) => f(o.status + i) > 0.5);
  const markers = [
    f(o.active) > 0.5 ? `<span class="badge active-badge">active</span>` : "",
    statusIndex >= 0 ? `<span class="badge status-badge s-${esc(ord.statuses[statusIndex])}">${esc(ord.statuses[statusIndex])}</span>` : "",
  ].join("");
  const boosts = ord.boosts.map((b, i) => [b, Math.round(f(o.boosts + i) * 6)]).filter(([, v]) => v !== 0)
    .map(([b, v]) => `<span class="chip ${v > 0 ? "up" : "down"}">${v > 0 ? "+" : ""}${v} ${esc(b)}</span>`).join("");
  const types = ord.types.filter((_, i) => f(o.types + i) > 0.5).map(typeChip).join("");
  const teraIndex = ord.types.findIndex((_, i) => f(o.tera + i) > 0.5);
  const tera = teraIndex >= 0 ? `tera ${typeChip(ord.types[teraIndex])}${f(o.terastallized) > 0.5 ? " <b>terastallized</b>" : ""}`
    : (f(o.terastallized) > 0.5 ? "<b>terastallized</b>" : "");
  const effects = ord.effects.filter((_, i) => f(o.effects + i) > 0.5).map((e) => `<span class="chip">${esc(pretty(e))}</span>`);
  if (f(o.mustRecharge) > 0.5) effects.push(`<span class="chip">must recharge</span>`);
  if (f(o.firstTurn) > 0.5) effects.push(`<span class="chip">first turn</span>`);
  if (f(o.preparing) > 0.5) effects.push(`<span class="chip">preparing</span>`);
  const stats = ord.stats.map((s, i) => `${s} ${Math.round(f(o.stats + i) * 200)}`).join(", ");
  // id 1 already says unknown; a known id with its flag off (an ability inferred from a
  // single possible one, or an edit) shows the flag
  const known = (id, flag, label) => (id === 1 || f(flag) > 0.5 ? "" : ` <span class="dim">(${label} = 0)</span>`);

  const lines = [
    `<div class="mon-head">${slotTag}<span class="mon-name" title="${esc(stats)}">${esc(name)}</span>`
      + `${species !== name.toLowerCase().replace(/[^a-z0-9]/g, "") ? ` <span class="dim">${esc(species)}</span>` : ""}`
      + ` <span class="dim">L${Math.round(f(o.level) * 100)}</span>${markers}</div>`,
    `<div class="hp-line"><div class="hp"><div class="hp-fill ${hpClass}" style="width:${(hp * 100).toFixed(1)}%"></div></div>`
      + `<span class="hp-text">${f(o.fainted) > 0.5 ? "fainted" : `${(hp * 100).toFixed(1)}%`}</span></div>`,
    `<div class="types">${types}${tera ? ` <span class="tera">${tera}</span>` : ""}</div>`,
    boosts || effects.length ? `<div class="chips">${boosts}${effects.join("")}</div>` : "",
    `<div class="small">${esc(itemName(ids[1]))}${known(ids[1], o.itemKnown, "item known")} &middot; `
      + `${ids[2] === 1 ? "ability unknown" : `${esc(abilityName(ids[2]))} <span class="id">#${ids[2]}</span>`}`
      + `${known(ids[2], o.abilityKnown, "ability known")}</div>`,
    `<ul class="moves">${movesView(row)}</ul>`,
  ];
  if (!ours && layout.cand) lines.push(priorsView(slot));
  return lines.join("");
}

function movesView(row) {
  const { layout } = app.model;
  const FM = layout.move.labels.length;
  const mo = layout.move.offsets;
  const ord = layout.orderings;
  const items = [];
  for (let j = 0; j < 4; j++) {
    const id = num(app.edited.move_ids, row * 4 + j);
    const f = (c) => num(app.edited.move_feats, (row * 4 + j) * FM + c);
    if (id === 0 && f(mo.present) < 0.5) continue;
    items.push(`<li>${moveSummary(id, f, mo, ord)}</li>`);
  }
  return items.join("") || `<li class="dim">no known move</li>`;
}

function moveSummary(id, f, mo, ord) {
  const typeIndex = ord.types.findIndex((_, i) => f(mo.type + i) > 0.5);
  const category = ord.categories.find((_, i) => f(mo.category + i) > 0.5);
  const power = Math.round(f(mo.power) * 150);
  const details = [category ? pretty(category).slice(0, 4) : "", power ? `${power} bp` : "",
    `${Math.round(f(mo.accuracy) * 100)}% acc`, `pp ${Math.round(f(mo.pp) * 100)}%`];
  const priority = Math.round(f(mo.priority) * 5);
  if (priority) details.push(`prio ${priority > 0 ? "+" : ""}${priority}`);
  const marks = [];
  if (f(mo.immune) > 0.5) marks.push(`<span class="eff zero">immune</span>`);
  else if (Math.abs(f(mo.effectiveness)) > 1e-6) {
    const mult = 2 ** (f(mo.effectiveness) * 2);
    marks.push(`<span class="eff ${mult > 1 ? "up" : "down"}">x${fmtValue(mult, 2)}</span>`);
  }
  if (f(mo.stab) > 0.5) marks.push(`<span class="eff">STAB</span>`);
  return `<span class="mv-name">${esc(moveName(id))}</span> <span class="id">#${id}</span> `
    + `${typeIndex >= 0 ? typeChip(ord.types[typeIndex]) : ""} <span class="dim">${esc(details.filter(Boolean).join(", "))}</span> ${marks.join(" ")}`;
}

// encoder_v2.py's set priors of their slot `slot`
function priorsView(slot) {
  const { layout } = app.model;
  const arrays = app.edited;
  const [, K, FC] = layout.byName.cand_feats.shape;
  const [, rows, A] = layout.byName.prior_ids.shape;
  const FP = layout.prior.length;
  const NT = layout.orderings.types.length;
  const cands = [];
  for (let j = 0; j < K; j++) {
    const id = num(arrays.cand_ids, slot * K + j);
    const fraction = num(arrays.cand_feats, (slot * K + j) * FC + FC - 1);
    if (id === 0 && fraction === 0) continue;
    cands.push(`<span class="chip">${esc(moveName(id))} ${Math.round(fraction * 100)}%</span>`);
  }
  const weighted = (r, name) => {
    const out = [];
    for (let j = 0; j < A; j++) {
      const id = num(arrays.prior_ids, (slot * rows + r) * A + j);
      const w = num(arrays.prior_w, (slot * rows + r) * A + j);
      if (id === 0 && w === 0) continue;
      out.push(`<span class="chip">${esc(name(id))} ${Math.round(w * 100)}%</span>`);
    }
    return out.join("");
  };
  const pf = (c) => num(arrays.prior_feats, slot * FP + c);
  const tera = layout.orderings.types.map((t, i) => [t, pf(i)]).filter(([, w]) => w > 0).sort((a, b) => b[1] - a[1])
    .map(([t, w]) => `${typeChip(t)} ${Math.round(w * 100)}%`).join(" ");
  const flags = layout.prior.slice(NT).map((label, i) => [label, pf(NT + i)]);
  if (flags.every(([, v]) => v === 0) && !cands.length) {
    return `<div class="priors dim small">set priors: none (species without sets)</div>`;
  }
  const flagText = flags.map(([label, v]) => `${esc(label)} ${fmtValue(v, 2)}`).join(", ");
  return `<div class="priors small"><div><b>candidate moves</b> ${cands.join("") || `<span class="dim">none</span>`}</div>`
    + `<div><b>abilities</b> ${weighted(0, abilityName) || `<span class="dim">none</span>`}</div>`
    + `<div><b>items</b> ${weighted(1, itemName) || `<span class="dim">none</span>`}</div>`
    + `<div><b>tera</b> ${tera || `<span class="dim">none</span>`}</div>`
    + `<div class="dim">${flagText}</div></div>`;
}

function renderFieldView() {
  const { layout } = app.model;
  const o = layout.field.offsets;
  const ord = layout.orderings;
  const f = (i) => num(app.edited.field, i);
  const on = (names, base) => names.filter((_, i) => f(base + i) > 0.5).map(pretty);
  const side = (base) => {
    const chips = ord.side_conditions.map((name, i) => [name, f(base + i)]).filter(([, v]) => v > 0)
      .map(([name, v]) => {
        const cap = STACKABLE[name];
        return `<span class="chip">${esc(pretty(name))}${cap ? ` x${Math.round(v * cap)}` : ""}</span>`;
      });
    return chips.join("") || `<span class="dim">none</span>`;
  };
  const flag = (label, i) => `<span class="flag ${f(i) > 0.5 ? "on" : "off"}">${label}</span>`;
  const turn = Math.round(f(o.turn) * 50);
  const rows = [
    `<div><b>weather</b> ${esc(on(ord.weathers, o.weather).join(", ") || "none")}`
      + ` &middot; <b>fields</b> ${esc(on(ord.fields, o.fields).join(", ") || "none")}</div>`,
    `<div><b class="mon ours">our side</b> ${side(o.ourSide)}</div>`,
    `<div><b class="mon theirs">their side</b> ${side(o.theirSide)}</div>`,
    `<div><b>turn</b> ${turn}${turn >= 50 ? "+" : ""} ${flag("can tera", o.canTera)} ${flag("used tera", o.usedTera)}`
      + ` ${flag("opponent used tera", o.oppUsedTera)} ${flag("force switch", o.forceSwitch)} ${flag("trapped", o.trapped)}</div>`,
    `<div class="dim">our alive ${Math.round(f(o.ourAlive) * 6)}/6, their not fainted ${Math.round(f(o.theirAlive) * 6)}/6,`
      + ` their revealed ${Math.round(f(o.theirRevealed) * 6)}/6</div>`,
  ];
  if (layout.hist) {
    const ids = app.edited.hist_ids;
    const hist = layout.hist.map((label, i) => `<li>${esc(label)}: <b>${fmtValue(num(app.edited.hist_feats, i), 3)}</b></li>`);
    rows.push(`<div class="history"><b>history</b> last move: ours ${esc(moveName(num(ids, 0)))}, `
      + `theirs ${esc(moveName(num(ids, 1)))}<ul>${hist.join("")}</ul></div>`);
  }
  return rows.join("");
}

// ---------------------------------------------------------------- edit summary and raw editor

function cellLabel(name, index) {
  const { layout } = app.model;
  const input = layout.byName[name];
  const at = unravel(input.shape, index);
  const slotName = (row) => monLabel(row);
  switch (name) {
    case "mon_ids": return `${slotName(at[0])} ${["species", "item", "ability"][at[1]]} id`;
    case "move_ids": return `${slotName(at[0])} move ${at[1] + 1} id`;
    case "mon_feats": return `${slotName(at[0])}: ${layout.mon.labels[at[1]]}`;
    case "move_feats": return `${slotName(at[0])} move ${at[1] + 1}: ${layout.move.labels[at[2]]}`;
    case "field": return layout.field.labels[at[0]];
    case "mask": return `action ${at[0]} legal (${actionLabels()[at[0]]})`;
    case "cand_ids": return `${slotName(N_SLOTS + at[0])} candidate ${at[1] + 1} move id`;
    case "cand_feats": return `${slotName(N_SLOTS + at[0])} candidate ${at[1] + 1}: ${layout.cand[at[2]]}`;
    case "prior_ids": return `${slotName(N_SLOTS + at[0])} ${["ability", "item"][at[1]] ?? "row " + at[1]} prior ${at[2] + 1} id`;
    case "prior_w": return `${slotName(N_SLOTS + at[0])} ${["ability", "item"][at[1]] ?? "row " + at[1]} prior ${at[2] + 1} weight`;
    case "prior_feats": return `${slotName(N_SLOTS + at[0])}: ${layout.prior[at[1]]}`;
    case "hist_ids": return ["our active's last move id", "their active's last move id"][at[0]];
    case "hist_feats": return layout.hist[at[0]];
    default: return `${name}[${at.join(", ")}]`;
  }
}

function renderEditSummary() {
  const changes = app.changes;
  $("edit-count").textContent = changes.length
    ? `${changes.length} input cell${changes.length === 1 ? "" : "s"} edited (ghost bars: the recorded state)`
    : "No edits: the inputs are the recorded ones";
  $("reset").disabled = !changes.length;
  $("edit-note").hidden = !changes.length;
  const shown = changes.slice(0, 12).map((change) => {
    const at = unravel(app.model.layout.byName[change.name].shape, change.index).join(", ");
    return `<li><code>${esc(change.name)}[${at}]</code> ${esc(cellLabel(change.name, change.index))}: `
      + `${fmtValue(change.from, 4)} &rarr; <b>${fmtValue(change.to, 4)}</b></li>`;
  });
  if (changes.length > shown.length) shown.push(`<li class="dim">and ${changes.length - shown.length} more</li>`);
  $("edit-list").innerHTML = shown.join("");
}

function buildRawEditor() {
  const select = $("raw-input");
  select.textContent = "";
  for (const input of app.model.layout.inputs) {
    select.append(h("option", { value: input.name, text: `${input.name} ${input.dtype} [${input.shape.join(", ")}]` }));
  }
  renderRawInfo();
}

// The flat index typed in the raw editor ("593", or "7, 5" for row 7, column 5)
function rawIndex() {
  const input = app.model.layout.byName[$("raw-input").value];
  if (!input) return null;
  const parts = $("raw-index").value.split(",").map((s) => s.trim()).filter(Boolean).map(Number);
  if (!parts.length || parts.some((v) => !Number.isInteger(v) || v < 0)) return null;
  let index;
  if (parts.length === 1) index = parts[0];
  else if (parts.length === input.shape.length && parts.every((v, k) => v < input.shape[k])) index = ravel(input.shape, parts);
  else return null;
  return index < input.size ? { input, index } : null;
}

function renderRawInfo() {
  if (!app.model) return;
  const input = app.model.layout.byName[$("raw-input").value];
  $("raw-shape").textContent = input ? `${input.dtype}, ${input.size} cells` : "";
  const target = app.edited ? rawIndex() : null;
  if (!target) {
    $("raw-info").textContent = app.edited ? "Index out of range." : "";
    return;
  }
  const { input: cell, index } = target;
  const at = unravel(cell.shape, index);
  const original = num(app.original[cell.name], index);
  const current = num(app.edited[cell.name], index);
  $("raw-info").innerHTML = `<code>${esc(cell.name)}[${at.join(", ")}]</code> (flat ${index}): ${esc(cellLabel(cell.name, index))}`
    + `<br>recorded ${fmtValue(original, 6)}, current <b>${fmtValue(current, 6)}</b>`;
}

function rawSet(useOriginal) {
  const target = rawIndex();
  if (!target || !app.edited) return;
  const { input, index } = target;
  const value = useOriginal ? num(app.original[input.name], index) : Number($("raw-value").value);
  if (!Number.isFinite(value)) return;
  writeCell(app.edited[input.name], index, value);
  afterEdit({ sync: true });
}

// ---------------------------------------------------------------- outputs

function actionLabels() {
  const decision = app.fixture.data.decisions[app.index];
  const idents = decision.team_idents ?? [];
  const F = app.model.layout.mon.labels.length;
  const o = app.model.layout.mon.offsets;
  let active = -1;
  for (let row = 0; row < N_SLOTS; row++) {
    if (num(app.edited.mon_feats, row * F + o.present) > 0.5 && num(app.edited.mon_feats, row * F + o.active) > 0.5) {
      active = row;
      break;
    }
  }
  const move = (j) => active < 0 ? `move ${j + 1}` : moveName(num(app.edited.move_ids, active * 4 + j));
  const labels = [];
  for (let i = 0; i < N_SLOTS; i++) labels.push(`switch ${idents[i] ? identName(idents[i]) : `slot ${i}`}`);
  for (let j = 0; j < 4; j++) labels.push(`move ${move(j)}`);
  for (let j = 0; j < 4; j++) labels.push(`tera + ${move(j)}`);
  return labels;
}

function buildActionRows() {
  const box = $("actions");
  box.textContent = "";
  actionRows = [];
  const orders = app.fixture.data.decisions[app.index].orders ?? {};
  for (let a = 0; a < ACT_DIM; a++) {
    const check = h("input", { type: "checkbox", title: `mask[${a}]: legal action` });
    bind(check, () => app.edited.mask[a] > 0, (v) => { app.edited.mask[a] = v ? 1 : 0; }, "checked");
    const row = {
      row: h("div", { class: "act" }),
      name: h("span", { class: "act-name" }),
      order: h("span", { class: "act-order", text: orders[String(a)] ?? "" }),
      ghost: h("div", { class: "bar ghost" }),
      now: h("div", { class: "bar now" }),
      prob: h("span", { class: "act-prob" }),
      logit: h("span", { class: "act-logit" }),
    };
    row.row.append(h("label", { class: "act-check" }, h("span", { class: "act-index", text: String(a) }), check),
      h("div", { class: "act-label" }, row.name, row.order),
      h("div", { class: "act-track" }, row.ghost, row.now), row.prob, row.logit);
    box.append(row.row);
    actionRows.push(row);
  }
  $("value").textContent = "";
  $("parity").textContent = "";
}

function renderOutputs() {
  const output = app.editedOutput;
  const original = app.originalOutput;
  if (!output || !original) return;
  const edited = app.changes.length > 0;
  const T = app.temperature;
  const probs = softmax(output.logits, app.edited.mask, T);
  const ghost = softmax(original.logits, app.original.mask, T);
  const best = argmax(output.logits, app.edited.mask);
  const originalBest = argmax(original.logits, app.original.mask);
  const labels = actionLabels();
  $("actions").classList.toggle("edited", edited);
  actionRows.forEach((row, a) => {
    const legal = app.edited.mask[a] > 0 && output.logits[a] > MASKED_LOGIT;
    row.row.classList.toggle("illegal", !legal);
    row.row.classList.toggle("best", a === best);
    row.row.classList.toggle("was-best", edited && a === originalBest && a !== best);
    row.name.textContent = labels[a];
    row.now.style.width = `${(probs[a] * 100).toFixed(2)}%`;
    row.ghost.style.width = `${(ghost[a] * 100).toFixed(2)}%`;
    row.ghost.hidden = !edited || ghost[a] === 0;
    row.prob.textContent = legal ? `${(probs[a] * 100).toFixed(1)}%` : "";
    row.prob.title = edited ? `recorded state: ${(ghost[a] * 100).toFixed(1)}%` : "";
    row.logit.textContent = output.logits[a] > MASKED_LOGIT ? output.logits[a].toFixed(3) : "masked";
    row.logit.title = edited && original.logits[a] > MASKED_LOGIT ? `recorded state: ${original.logits[a].toFixed(4)}` : "";
  });
  const bestText = best >= 0 ? labels[best] : "none";
  $("policy-info").textContent = `softmax over the legal actions at temperature ${T.toFixed(2)}; argmax: ${bestText}`
    + (edited && originalBest !== best ? ` (recorded state: ${originalBest >= 0 ? labels[originalBest] : "none"})` : "")
    + (edited ? ". Ghost bars: the recorded state." : ".");
  renderValue(output.value, original.value, edited);
}

function renderValue(value, originalValue, edited) {
  const box = $("value");
  if (!box.firstChild) {
    box.innerHTML = `<div class="gauge"><div class="gauge-track"><span class="gauge-zero"></span>`
      + `<span class="gauge-ghost"></span><span class="gauge-needle"></span></div>`
      + `<div class="gauge-scale"><span>-${VALUE_RANGE}</span><span>0</span><span>+${VALUE_RANGE}</span></div></div>`
      + `<div class="value-number"></div>`
      + `<p class="note">The expected training return from this state: +1 for a win and -1 for a loss, plus 0.1 per`
      + ` opposing faint and -0.1 per own faint, discounted (gamma 0.995), so it stays within`
      + ` [-${VALUE_RANGE}, ${VALUE_RANGE}]. It is not a win probability.</p>`;
  }
  const position = (v) => `${((clamp(v, -VALUE_RANGE, VALUE_RANGE) + VALUE_RANGE) / (2 * VALUE_RANGE) * 100).toFixed(2)}%`;
  box.querySelector(".gauge-needle").style.left = position(value);
  const ghost = box.querySelector(".gauge-ghost");
  ghost.style.left = position(originalValue);
  ghost.hidden = !edited;
  const delta = value - originalValue;
  box.querySelector(".value-number").innerHTML = `value <b>${value.toFixed(4)}</b>`
    + (edited ? ` <span class="delta ${delta >= 0 ? "up" : "down"}">${delta >= 0 ? "+" : ""}${delta.toFixed(4)}`
      + ` vs the recorded state (${originalValue.toFixed(4)})</span>` : "");
}

function renderParity() {
  const box = $("parity");
  const decision = app.fixture.data.decisions[app.index];
  const diff = compareRecorded(decision, app.originalOutput);
  const { source } = app.fixture;
  const model = app.model.name;
  let kind;
  let note = "";
  if (!diff) {
    box.className = "parity warn";
    box.textContent = "This fixture has no recorded logits (make_fixtures.py ran without --model).";
    return;
  }
  const pass = diff.logit < PARITY_TOLERANCE && diff.value < PARITY_TOLERANCE;
  if (source === model) {
    kind = pass ? "good" : "bad";
  } else {
    kind = "warn";
    note = source
      ? `Recorded by ${source}, not ${model}: the recorded logits only match the model that recorded the fixture's folder.`
      : "Unknown origin (not in any model's Fixtures folder): the recorded logits only match the model that recorded it.";
  }
  box.className = `parity ${kind}`;
  box.innerHTML = `<b>${pass ? "match" : "differs"}</b> (recorded state, this decision): max |logit - recorded| over legal actions`
    + ` <b>${fmtDiff(diff.logit)}</b>, |value - recorded| <b>${fmtDiff(diff.value)}</b>`
    + ` (green under ${PARITY_TOLERANCE.toExponential(0)})${note ? `<br>${esc(note)}` : ""}`;
}

// ---------------------------------------------------------------- parity checks

async function checkFixtureData(model, data, onProgress) {
  const result = { decisions: 0, missing: 0, logit: 0, value: 0, worstLogit: -1, worstValue: -1 };
  for (let i = 0; i < data.decisions.length; i++) {
    const decision = data.decisions[i];
    const output = await runModel(model, decisionArrays(decision, model.layout.inputs));
    result.decisions++;
    const diff = compareRecorded(decision, output);
    if (!diff) {
      result.missing++;
    } else {
      if (result.worstLogit < 0 || diff.logit > result.logit) {
        result.logit = diff.logit;
        result.worstLogit = i;
      }
      if (result.worstValue < 0 || diff.value > result.value) {
        result.value = diff.value;
        result.worstValue = i;
      }
    }
    onProgress?.(i + 1, data.decisions.length);
  }
  result.pass = result.missing === 0 && result.logit < PARITY_TOLERANCE && result.value < PARITY_TOLERANCE;
  return result;
}

async function checkFixture() {
  const { model, fixture } = app;
  if (!fixture) return null;
  const box = $("check-result");
  const result = await checkFixtureData(model, fixture.data, (done, total) => {
    box.textContent = `checking ${fixture.name}: ${done} / ${total}`;
  });
  const jump = (i) => `<a href="#" data-decision="${i}">decision ${i + 1}</a>`;
  box.className = `check-result ${result.pass ? "good" : fixture.source === model.name ? "bad" : "warn"}`;
  box.innerHTML = `${esc(fixture.name)}, ${result.decisions} decisions with ${esc(model.name)}: `
    + `${result.pass ? "<b>pass</b>" : "<b>fail</b>"}. Max |logit diff| <b>${fmtDiff(result.logit)}</b>`
    + `${result.worstLogit >= 0 ? ` (${jump(result.worstLogit)})` : ""}, max |value diff| <b>${fmtDiff(result.value)}</b>`
    + `${result.worstValue >= 0 ? ` (${jump(result.worstValue)})` : ""}`
    + `${result.missing ? `, ${result.missing} decisions without recorded logits` : ""}.`
    + `${fixture.source !== model.name ? ` ${esc(fixture.source ? `Recorded by ${fixture.source}.` : "Unknown origin.")}` : ""}`;
  return result;
}

async function checkFolder() {
  const { model } = app;
  const files = app.fixtureFolders.get(model.name) ?? [];
  const box = $("check-result");
  if (!files.length) {
    box.textContent = `${model.name} has no fixtures folder.`;
    return null;
  }
  const total = { model: model.name, fixtures: 0, decisions: 0, missing: 0, logit: 0, value: 0,
    worstLogit: null, worstValue: null, failures: [], refused: [] };
  for (const [k, file] of files.entries()) {
    box.textContent = `checking ${model.name}: fixture ${k + 1} / ${files.length} (${file})`;
    const data = await fetchFixture(model.name, file);
    if ((data.encoder_version ?? 1) !== model.encoderVersion) {
      total.refused.push(file);
      continue;
    }
    const result = await checkFixtureData(model, data);
    total.fixtures++;
    total.decisions += result.decisions;
    total.missing += result.missing;
    if (result.logit >= total.logit) { total.logit = result.logit; total.worstLogit = [file, result.worstLogit]; }
    if (result.value >= total.value) { total.value = result.value; total.worstValue = [file, result.worstValue]; }
    if (!result.pass) total.failures.push(file);
  }
  total.pass = !total.failures.length && !total.refused.length && total.fixtures === files.length;
  const where = (worst) => (worst ? ` (${esc(worst[0].replace(/\.json\.gz$/, ""))}, decision ${worst[1] + 1})` : "");
  box.className = `check-result ${total.pass ? "good" : "bad"}`;
  box.innerHTML = `${esc(model.name)}: ${total.fixtures} fixtures, ${total.decisions} decisions: `
    + `${total.pass ? "<b>pass</b>" : "<b>fail</b>"}. Max |logit diff| <b>${fmtDiff(total.logit)}</b>${where(total.worstLogit)},`
    + ` max |value diff| <b>${fmtDiff(total.value)}</b>${where(total.worstValue)}.`
    + `${total.failures.length ? ` Failing: ${esc(total.failures.join(", "))}.` : ""}`
    + `${total.refused.length ? ` Other encoder version: ${esc(total.refused.join(", "))}.` : ""}`;
  return total;
}

// ---------------------------------------------------------------- startup

function bindEvents() {
  $("model-select").addEventListener("change", (e) => guarded(() => selectModel(e.target.value)));
  $("fixture-select").addEventListener("change", (e) => {
    if (e.target.value) guarded(() => loadFixtureByName(e.target.value));
  });
  $("file-input").addEventListener("change", (e) => {
    const file = e.target.files[0];
    e.target.value = "";
    if (file) guarded(() => loadFile(file));
  });
  $("decision-slider").addEventListener("input", (e) => guarded(() => showDecision(Number(e.target.value))));
  $("prev").addEventListener("click", () => guarded(() => showDecision(app.index - 1)));
  $("next").addEventListener("click", () => guarded(() => showDecision(app.index + 1)));
  document.addEventListener("keydown", (e) => {
    if ((e.key !== "ArrowLeft" && e.key !== "ArrowRight") || !app.fixture || e.altKey || e.ctrlKey || e.metaKey) return;
    if (e.target.matches?.("input[type=text], input[type=number], input[type=range], select, textarea")) return;
    e.preventDefault();
    guarded(() => showDecision(app.index + (e.key === "ArrowLeft" ? -1 : 1)));
  });
  $("show-requests").addEventListener("change", (e) => {
    const log = $("log");
    log.classList.toggle("show-requests", e.target.checked);
    log.scrollTop = log.scrollHeight;
  });
  $("reset").addEventListener("click", resetEdits);
  $("temperature").addEventListener("input", (e) => {
    app.temperature = Number(e.target.value);
    $("temperature-value").textContent = app.temperature.toFixed(2);
    renderOutputs();
  });
  $("raw-input").addEventListener("change", () => {
    $("raw-index").value = "0";
    renderRawInfo();
  });
  $("raw-index").addEventListener("input", renderRawInfo);
  $("raw-set").addEventListener("click", () => rawSet(false));
  $("raw-revert").addEventListener("click", () => rawSet(true));
  $("check-fixture").addEventListener("click", () => guarded(withButtonsDisabled(checkFixture)));
  $("check-folder").addEventListener("click", () => guarded(withButtonsDisabled(checkFolder)));
  $("check-result").addEventListener("click", (e) => {
    const link = e.target.closest("a[data-decision]");
    if (!link) return;
    e.preventDefault();
    guarded(() => showDecision(Number(link.dataset.decision)));
  });

  const overlay = $("drop-overlay");
  const hasFiles = (e) => [...(e.dataTransfer?.types ?? [])].includes("Files");
  document.addEventListener("dragover", (e) => {
    if (!hasFiles(e)) return;
    e.preventDefault();
    overlay.hidden = false;
  });
  document.addEventListener("dragleave", (e) => {
    if (!e.relatedTarget) overlay.hidden = true;
  });
  document.addEventListener("drop", (e) => {
    if (!hasFiles(e)) return;
    e.preventDefault();
    overlay.hidden = true;
    const file = e.dataTransfer.files[0];
    if (file) guarded(() => loadFile(file));
  });
}

function withButtonsDisabled(action) {
  return async () => {
    const buttons = [$("check-fixture"), $("check-folder")];
    buttons.forEach((button) => { button.disabled = true; });
    try {
      return await action();
    } finally {
      buttons.forEach((button) => { button.disabled = false; });
    }
  };
}

async function init() {
  bindEvents();
  if (typeof ort === "undefined") {
    showBanner(`onnxruntime-web did not load from ${ORT_DIST} (offline?).`);
    return;
  }
  ort.env.wasm.wasmPaths = ORT_DIST;
  // http.server is not cross-origin isolated, so there are no wasm threads anyway
  ort.env.wasm.numThreads = 1;
  let entries;
  try {
    entries = await listDirectory(MODELS_URL);
  } catch (error) {
    showBanner(`Cannot list ${MODELS_URL.pathname} (${error.message}). Serve the repository root: `
      + "python3 -m http.server 8765, then open http://localhost:8765/tools/minipoke/playground/.");
    return;
  }
  app.models = entries.filter((entry) => entry.endsWith("/") && !entry.startsWith(".")).map((entry) => entry.slice(0, -1)).sort();
  if (!app.models.length) {
    showBanner(`No model folder under ${MODELS_URL.pathname}.`);
    return;
  }
  await Promise.all(app.models.map(async (model) => {
    try {
      const files = (await listDirectory(fixturesUrl(model))).filter((file) => file.endsWith(".json.gz")).sort();
      app.fixtureFolders.set(model, files);
      for (const file of files) app.fixtureSource.set(file, model);
    } catch {
      app.fixtureFolders.set(model, []);  // no fixtures for this model
    }
  }));
  const select = $("model-select");
  for (const model of app.models) select.append(h("option", { value: model, text: model }));
  await guarded(() => selectModel(app.models[app.models.length - 1]));
}

window.playground = { app, selectModel, loadFixtureByName, showDecision, checkFixture, checkFolder, resetEdits };
init();
