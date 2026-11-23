// Runs the extension's hook.js, background.js and content.js against a live assistant server, with a fake page (no
// dependencies), replaying a recorded battle the way one of Showdown's clients receives it:
// - new (default): the new client, whose connection Worker posts { type: 'message', data } to the page, and its
//   data-cmd buttons ("/move 2", "/switch 3", "/move 2 terastallize" once the Tera box is ticked);
// - classic: the classic client, with SockJS frames on a WebSocket and its chooseMove / chooseSwitch buttons.
// Checks that the panel recommends what the server does, that the matching button is highlighted, and the offline,
// choice, hidden room and battle end paths. Start the server first, then, from the repository root:
//
//   node tools/showdown-assistant/tests/extension_harness.mjs tools/showdown-assistant/extension \
//     http://127.0.0.1:7685 test/ElsaMina.UnitTests/Battles/Strategies/GladionV1_22M/Fixtures/battle-gen9randombattle-995044-p1.json.gz [new|classic]
import fs from 'node:fs';
import vm from 'node:vm';
import zlib from 'node:zlib';

const [, , extensionDir, base, fixturePath, client = 'new'] = process.argv;
if (client !== 'new' && client !== 'classic') {
  throw new Error(`Unknown client ${client}: new or classic`);
}
const isNew = client === 'new';
const fixture = JSON.parse(zlib.gunzipSync(fs.readFileSync(fixturePath)));
const room = fixture.battle_tag;
let failures = 0;
const check = (condition, message) => {
  if (!condition) {
    failures++;
    console.log('FAIL', message);
  }
};

// ---- Fake DOM ----
class ClassList {
  constructor() { this.names = new Set(); }
  add(...names) { names.forEach((name) => this.names.add(name)); }
  remove(...names) { names.forEach((name) => this.names.delete(name)); }
  contains(name) { return this.names.has(name); }
}

class Node_ {
  constructor(tag, attributes = {}) {
    this.tagName = tag; this.attributes = attributes; this.children = []; this.classList = new ClassList();
    this.style = {}; this.text = ''; this.listeners = {}; this.parent = null; this.hidden = false; this.id = '';
    this.title = '';
  }
  set className(value) { this.classList = new ClassList(); value.split(/\s+/).filter(Boolean).forEach((c) => this.classList.add(c)); }
  get className() { return [...this.classList.names].join(' '); }
  set textContent(value) { this.text = String(value); this.children = []; }
  get textContent() { return this.text + this.children.map((child) => child.textContent).join(''); }
  append(...nodes) { for (const node of nodes) { node.parent = this; this.children.push(node); } }
  appendChild(node) { this.append(node); return node; }
  replaceChildren(...nodes) { this.children = []; this.text = ''; this.append(...nodes); }
  remove() { if (this.parent) { this.parent.children = this.parent.children.filter((c) => c !== this); this.parent = null; } }
  get isConnected() { let node = this; while (node.parent) node = node.parent; return node === document.body; }
  addEventListener(type, listener) { (this.listeners[type] ||= []).push(listener); }
  closest(selector) { return selector === 'label' ? this.label ?? null : null; }
  getBoundingClientRect() { return { left: 0, top: 0 }; }
  get offsetParent() { return this.hidden ? null : document.body; }
  find(className) {
    const found = [];
    const walk = (node) => { if (node.classList?.contains(className)) found.push(node); node.children?.forEach(walk); };
    walk(this);
    return found;
  }
}

class Text_ {
  constructor(text) { this.textContent = text; this.parent = null; }
}

// Enough of CSS for the selectors content.js uses: 'tag[attr="v"][attr^="v"], tag[...]'
function matches(node, selector) {
  return selector.split(',').some((part) => {
    const match = part.trim().match(/^(\w+)((?:\[[\w-]+\^?="[^"]*"\])*)$/);
    if (!match || node.tagName !== match[1]) return false;
    return [...match[2].matchAll(/\[([\w-]+)(\^?)="([^"]*)"\]/g)].every(([, name, prefix, value]) => {
      const actual = node.attributes[name];
      return actual !== undefined && (prefix ? actual.startsWith(value) : actual === value);
    });
  });
}

// Each client's controls: the switch buttons (Pokémon 1-6 of the request), the move buttons, the Tera checkbox
const switchButtons = [1, 2, 3, 4, 5, 6].map((position) => new Node_('button', isNew
  ? { 'data-cmd': `/switch ${position}` }
  : { name: 'chooseSwitch', value: String(position - 1) }));
const moveButtons = [1, 2, 3, 4].map((position) => new Node_('button', isNew
  ? { 'data-cmd': `/move ${position}` }
  : { name: 'chooseMove', value: String(position) }));
const buttons = [...switchButtons, ...moveButtons];
const teraLabel = new Node_('label');
const teraInput = Object.assign(new Node_('input', { name: isNew ? 'tera' : 'terastallize' }), { label: teraLabel });
const roomElement = new Node_('div');
roomElement.querySelectorAll = (selector) => [...buttons, teraInput].filter((node) => matches(node, selector));
roomElement.querySelector = (selector) => roomElement.querySelectorAll(selector)[0] ?? null;

// The new client's move buttons send "/move N terastallize" while its Tera box is ticked
function tickTera(ticked) {
  if (isNew) {
    moveButtons.forEach((button, i) => { button.attributes['data-cmd'] = `/move ${i + 1}${ticked ? ' terastallize' : ''}`; });
  }
}

function expectedButton(action) {
  const [verb, position] = action.choice.split(' ');
  return (verb === 'switch' ? switchButtons : moveButtons)[Number(position) - 1];
}

const document = {
  body: new Node_('body'),
  createElement: (tag) => new Node_(tag),
  createTextNode: (text) => new Text_(text),
  getElementById: (id) => (id === `room-${room}` ? roomElement : null),
  addEventListener() {}, removeEventListener() {},
};

// ---- Fake window, socket and worker ----
const messageListeners = [];
const window = {
  location: { origin: 'https://play.pokemonshowdown.com' },
  innerWidth: 1280, innerHeight: 800,
  addEventListener: (type, listener) => { if (type === 'message') messageListeners.push(listener); },
  postMessage: (data) => setTimeout(() => messageListeners.forEach((listener) => listener({ source: window, data })), 0),
};
const sent = [];
class FakeWebSocket extends EventTarget {
  constructor(url) { super(); this.url = url; }
  send(data) { sent.push(data); }
}
class FakeWorker extends EventTarget {
  constructor(url) { super(); this.url = url; }
  postMessage(message) { sent.push(message); }
}
window.WebSocket = FakeWebSocket;
window.Worker = FakeWorker;

// ---- Fake browser APIs: content and background share them ----
let lastAdvice = null;
let lastSearch = null;
const roomsSent = new Set();
const store = {};
const changeListeners = [];
const backgroundListeners = [];
const browser = {
  storage: {
    local: {
      get: async (defaults) => ({ ...defaults, ...store }),
      set: async (values) => {
        const changes = {};
        for (const [key, value] of Object.entries(values)) { changes[key] = { newValue: value }; store[key] = value; }
        changeListeners.forEach((listener) => listener(changes, 'local'));
      },
    },
    onChanged: { addListener: (listener) => changeListeners.push(listener) },
  },
  runtime: {
    sendMessage: async (message) => {
      if (message.roomId) roomsSent.add(message.roomId);
      const reply = await backgroundListeners[0](message);
      if (message.type === 'frames' && reply.status === 'advice') lastAdvice = reply;
      if (message.type === 'search') lastSearch = reply;
      return reply;
    },
    onMessage: { addListener: (listener) => backgroundListeners.push(listener) },
    onInstalled: { addListener() {} },
  },
};

const context = vm.createContext({
  window, document, browser, CSS: { escape: (text) => text }, console, fetch, AbortSignal,
  setTimeout, clearTimeout, setInterval, EventTarget,
});
for (const file of ['hook.js', 'background.js', 'content.js']) {
  vm.runInContext(fs.readFileSync(`${extensionDir}/${file}`, 'utf8'), context, { filename: file });
}
await browser.storage.local.set({ serverUrl: base });

// ---- The client's connection ----
const event = (data) => Object.assign(new Event('message'), { data });
let receive;
let choose;
if (isNew) {
  const worker = new window.Worker('/js/client-connection-worker.js');
  check(worker instanceof FakeWorker, 'the hooked worker is still a Worker');
  worker.dispatchEvent(event({ type: 'connected' }));
  receive = (message) => worker.dispatchEvent(event({ type: 'message', data: message }));
  choose = (message) => worker.postMessage({ type: 'send', data: message });
  // Other workers are left alone
  const other = new window.Worker('/js/other-worker.js');
  other.dispatchEvent(event({ type: 'message', data: '>battle-gen9randombattle-1\n|init|battle' }));
} else {
  const socket = new window.WebSocket('wss://sim3.psim.us/showdown/1/abc/websocket');
  check(socket instanceof FakeWebSocket, 'the hooked socket is still a WebSocket');
  socket.dispatchEvent(event('o'));
  receive = (message) => socket.dispatchEvent(event(`a${JSON.stringify([message])}`));
  choose = (message) => socket.send(JSON.stringify([message]));
}

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));
const panel = () => document.body.children.find((child) => child.id === 'elsamina-assistant');
async function waitFor(predicate, ms = 15000) {
  for (let waited = 0; waited < ms; waited += 50) {
    if (predicate()) return true;
    await sleep(50);
  }
  return false;
}
const highlightedButtons = () => buttons.filter((button) => button.classList.contains('ema-highlight'));
const bestLabel = () => panel()?.find('ema-best')[0]?.find('ema-label')[0]?.textContent.replace('⇄', '');
const describe = (button) => (button ? JSON.stringify(button.attributes) : 'none');

let applied = 0;
let advised = 0;
let searchedDecisions = 0;
const decisions = fixture.decisions;
for (let index = 0; index < decisions.length; index++) {
  const lines = fixture.lines.slice(applied, decisions[index].line_count);
  applied = decisions[index].line_count;
  // Showdown sends the turn's log, then the request on its own
  const requestAt = lines.findLastIndex((line) => line.startsWith('|request|'));
  receive(`>${room}\n${lines.slice(0, requestAt).join('\n')}`);
  receive(`>${room}\n${lines.slice(requestAt).join('\n')}`);
  const rqid = JSON.parse(lines[requestAt].slice('|request|'.length)).rqid;
  tickTera(index % 2 === 1);

  if (index === 5) {
    // The server goes away: the panel says so, then gets the battle again from its start once it is back
    await browser.storage.local.set({ serverUrl: 'http://127.0.0.1:9' });
    receive(`>${room}\n|c|☆Opponent|gl`);
    check(await waitFor(() => panel()?.find('ema-error').length > 0), 'offline error shown');
    await browser.storage.local.set({ serverUrl: base });
  }

  const ok = await waitFor(() => lastAdvice?.rqid === rqid && bestLabel() && highlightedButtons().length > 0 &&
    !panel().textContent.includes('Looking one turn ahead') &&
    (!lastAdvice.canSearch || lastAdvice.actions.length < 2 || lastSearch?.rqid === rqid));
  check(ok, `decision ${index} (rqid ${rqid}): advice shown and a button highlighted`);
  if (!ok) {
    console.log('  advice rqid', lastAdvice?.rqid, 'best', lastAdvice?.best, 'search', JSON.stringify(lastSearch),
      '| panel:', bestLabel(), '| highlighted', highlightedButtons().length, '|', panel()?.textContent.slice(0, 160));
    continue;
  }

  const searched = lastSearch?.rqid === rqid && lastSearch.status === 'searched' ? lastSearch.action : null;
  const expected = lastAdvice.actions.find((action) => action.action === (searched ?? lastAdvice.best));
  check(bestLabel() === expected.label, `decision ${index}: panel shows ${bestLabel()}, server recommends ${expected.label}`);
  check(highlightedButtons().length === 1 && highlightedButtons()[0] === expectedButton(expected),
    `decision ${index}: ${expected.choice} highlights ${highlightedButtons().map(describe)}`);
  check(teraLabel.classList.contains('ema-highlight') === expected.terastallize, `decision ${index}: tera highlight`);
  advised++;
  if (panel().find('ema-badge-search').length || panel().textContent.includes('the search agrees')) searchedDecisions++;
  if (index < 8) console.log(`${panel().find('ema-turn')[0]?.textContent.padEnd(8)} best: ${bestLabel().padEnd(16)} ` +
    `button: ${describe(highlightedButtons()[0]).padEnd(36)} tera: ${teraLabel.classList.contains('ema-highlight')}`);

  if (index === 3) {
    // The player clicks: the highlight goes, the panel says what was chosen
    choose(`${room}|/choose move 1|${rqid}`);
    check(await waitFor(() => panel().textContent.includes('You chose: move 1')), 'choice shown');
    check(await waitFor(() => highlightedButtons().length === 0), 'highlight cleared after choosing');
    check(sent.length === 1, 'the connection still sends');
  }
  if (index === 8) {
    // Another room on screen: no panel
    roomElement.hidden = true;
    check(await waitFor(() => !panel()), 'panel hidden when the battle is not on screen');
    roomElement.hidden = false;
    check(await waitFor(() => !!panel()), 'panel back when the battle is on screen');
  }
}

receive(`>${room}\n${fixture.lines.slice(applied).join('\n')}`);
check(await waitFor(() => !panel() || panel().textContent.includes('Battle over')), 'battle over or closed at the end');
check(!roomsSent.has('battle-gen9randombattle-1'), 'messages of other workers are not copied');
console.log(`${client} client: decisions ${decisions.length}, advised ${advised}, with search ${searchedDecisions}, ` +
  `failures ${failures}`);
process.exit(failures ? 1 : 0);
