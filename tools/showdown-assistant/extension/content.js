// Receives the battle rooms' messages from hook.js, forwards them in order to the assistant server (through
// background.js) and shows its advice in a panel over the battle, highlighting the recommended button.
(() => {
  'use strict';

  const SOURCE = 'elsamina-battle-assistant';
  const RETRY_MS = 3000;
  const TICK_MS = 500;
  // A long battle is a few thousand frames; past this the log stops being kept and can't be replayed
  const MAX_LOG_FRAMES = 20000;
  const PANEL_ID = 'elsamina-assistant';
  const HIGHLIGHT_CLASS = 'ema-highlight';
  // The Tera checkbox: classic client, new client
  const TERA_SELECTOR = 'input[name="terastallize"], input[name="tera"]';
  const TYPE_COLORS = {
    Normal: '#9fa19f', Fire: '#e62829', Water: '#2980ef', Electric: '#d8b000', Grass: '#3fa129', Ice: '#3dcef3',
    Fighting: '#ff8000', Poison: '#9141cb', Ground: '#915121', Flying: '#5c8ec5', Psychic: '#ef4179', Bug: '#91a119',
    Rock: '#afa981', Ghost: '#704170', Dragon: '#5060e1', Dark: '#624d4e', Steel: '#60a1b8', Fairy: '#ef70ef',
    Stellar: '#40b5a5',
  };

  const settings = { enabled: true, search: true, collapsed: false, panelPosition: null };
  const rooms = new Map();
  const server = { online: null, message: '' };
  let retryTimer = null;
  let panel = null;
  let renderedKey = '';
  let highlighted = [];

  // ---- Settings --------------------------------------------------------------------------------------------------

  browser.storage.local.get(settings).then((stored) => {
    Object.assign(settings, stored);
    render();
  });

  browser.storage.onChanged.addListener((changes, area) => {
    if (area !== 'local') {
      return;
    }
    const wasEnabled = settings.enabled;
    for (const [key, { newValue }] of Object.entries(changes)) {
      if (key in settings) {
        settings[key] = newValue;
      }
    }
    if (settings.enabled && !wasEnabled) {
      // The frames received meanwhile were not sent: replay each battle from its start
      for (const room of rooms.values()) {
        room.needsReset = true;
        pump(room);
      }
    }
    if (changes.serverUrl) {
      retryNow();
    }
    renderedKey = '';
    render();
  });

  // ---- Frames from the page --------------------------------------------------------------------------------------

  window.addEventListener('message', (event) => {
    const data = event.data;
    if (event.source !== window || !data || data.source !== SOURCE) {
      return;
    }
    if (typeof data.frame === 'string') {
      onFrame(data.frame);
    } else if (typeof data.roomId === 'string' && typeof data.choice === 'string') {
      onChoice(data.roomId, data.choice);
    }
  });

  function getRoom(roomId) {
    let room = rooms.get(roomId);
    if (!room) {
      room = {
        roomId, log: [], complete: false, queue: [], inFlight: false, needsReset: false,
        advice: null, partial: false, search: null, chosen: null, turn: 0,
      };
      rooms.set(roomId, room);
    }
    return room;
  }

  function onFrame(frame) {
    const newline = frame.indexOf('\n');
    const roomId = frame.slice(1, newline === -1 ? undefined : newline).trim();
    const room = getRoom(roomId);
    if (frame.includes('\n|init|battle')) {
      // A new battle, or Showdown sending the whole log again (reconnect, page reload)
      Object.assign(room, { log: [], complete: true, queue: [], needsReset: false, advice: null, search: null,
        chosen: null });
    }
    if (room.log.length < MAX_LOG_FRAMES) {
      room.log.push(frame);
    } else {
      room.complete = false;
    }
    room.queue.push(frame);
    if (frame.includes('\n|deinit')) {
      rooms.delete(roomId);
    }
    pump(room);
    render();
  }

  function onChoice(roomId, choice) {
    const room = rooms.get(roomId);
    if (room) {
      room.chosen = choice || null;
      render();
    }
  }

  // ---- Server ----------------------------------------------------------------------------------------------------

  function send(message) {
    return browser.runtime.sendMessage(message).catch((error) => ({ error: String(error) }));
  }

  // One request per room at a time, so the server applies the frames in order
  async function pump(room) {
    if (!settings.enabled || room.inFlight || (!room.needsReset && room.queue.length === 0)) {
      return;
    }
    const reset = room.needsReset && room.complete;
    const frames = reset ? room.log.slice() : room.queue;
    room.queue = [];
    room.needsReset = false;
    room.inFlight = true;
    const reply = await send({ type: 'frames', roomId: room.roomId, frames, reset });
    room.inFlight = false;

    if (reply.error) {
      setServer(false, reply.error);
      if (reset) {
        room.needsReset = true;
      } else {
        room.queue = frames.concat(room.queue);
      }
      scheduleRetry();
      render();
      return;
    }

    setServer(true, '');
    if (reply.partial && room.complete && !reset) {
      // The server restarted mid-battle: give it the battle from its start
      room.needsReset = true;
    } else {
      applyAdvice(room, reply);
    }
    pump(room);
  }

  function applyAdvice(room, advice) {
    room.partial = advice.partial;
    room.turn = advice.turn;
    if (advice.status !== 'unchanged') {
      if (advice.status === 'advice' && advice.rqid !== room.advice?.rqid) {
        room.chosen = null;
      }
      room.advice = advice;
      room.search = null;
      if (advice.status === 'advice') {
        search(room, advice);
      }
    }
    render();
  }

  async function search(room, advice) {
    if (!settings.search || !advice.canSearch || advice.actions.length < 2) {
      return;
    }
    room.search = { status: 'running' };
    render();
    const reply = await send({ type: 'search', roomId: room.roomId, rqid: advice.rqid });
    if (room.advice !== advice) {
      return;
    }
    room.search = reply.error ? { status: 'failed', message: reply.error } : reply;
    render();
  }

  function setServer(online, message) {
    server.online = online;
    server.message = message;
  }

  function scheduleRetry() {
    if (retryTimer === null) {
      retryTimer = setTimeout(retryNow, RETRY_MS);
    }
  }

  function retryNow() {
    clearTimeout(retryTimer);
    retryTimer = null;
    for (const room of rooms.values()) {
      pump(room);
    }
  }

  // ---- Panel -----------------------------------------------------------------------------------------------------

  // The battle on screen: Showdown hides the rooms it doesn't show
  function visibleRoom() {
    let shown = null;
    for (const room of rooms.values()) {
      const element = document.getElementById(`room-${room.roomId}`);
      if (element && element.offsetParent !== null && (!shown || room.advice)) {
        shown = room;
      }
    }
    return shown;
  }

  function recommendedAction(room) {
    const advice = room.advice;
    if (advice?.status !== 'advice') {
      return null;
    }
    return room.search?.status === 'searched' ? room.search.action : advice.best;
  }

  function render() {
    if (!document.body) {
      return;
    }
    const room = settings.enabled ? visibleRoom() : null;
    const key = room === null ? '' : JSON.stringify([room.roomId, room.advice, room.search, room.chosen,
      room.partial, room.complete, room.turn, server, settings.collapsed]);
    if (key !== renderedKey) {
      renderedKey = key;
      if (room === null) {
        panel?.remove();
        panel = null;
      } else {
        drawPanel(room);
      }
    }
    highlight(room);
  }

  function element(tag, className, text) {
    const node = document.createElement(tag);
    if (className) {
      node.className = className;
    }
    if (text !== undefined) {
      node.textContent = text;
    }
    return node;
  }

  function ensurePanel() {
    if (panel?.isConnected) {
      return panel;
    }
    panel = element('div', 'ema-panel');
    panel.id = PANEL_ID;
    const position = settings.panelPosition;
    if (position) {
      panel.style.left = `${Math.min(position.left, window.innerWidth - 80)}px`;
      panel.style.top = `${Math.min(position.top, window.innerHeight - 40)}px`;
      panel.style.right = 'auto';
      panel.style.bottom = 'auto';
    }
    document.body.appendChild(panel);
    return panel;
  }

  function drawPanel(room) {
    const root = ensurePanel();
    root.replaceChildren();
    const advice = room.advice;

    const header = element('div', 'ema-header');
    header.append(element('span', 'ema-title', 'Battle assistant'));
    if (advice?.turn) {
      header.append(element('span', 'ema-turn', `Turn ${advice.turn}`));
    }
    const collapse = element('button', 'ema-collapse', settings.collapsed ? '+' : '–');
    collapse.title = settings.collapsed ? 'Expand' : 'Collapse';
    collapse.addEventListener('click', () => browser.storage.local.set({ collapsed: !settings.collapsed }));
    header.append(collapse);
    makeDraggable(root, header);
    root.append(header);
    if (settings.collapsed) {
      return;
    }

    const body = element('div', 'ema-body');
    root.append(body);
    if (server.online === false) {
      body.append(element('div', 'ema-message ema-error', `${server.message}. Start it with "dotnet run" in ` +
        'tools/showdown-assistant/server: the battle is sent again once it answers.'));
      return;
    }
    if (!advice) {
      // Requests only come to the players: by turn 2 without one, this battle is someone else's
      body.append(element('div', 'ema-message', room.turn >= 2
        ? 'Spectating: advice only shows in your own battles.'
        : 'Waiting for your next decision…'));
      return;
    }

    switch (advice.status) {
      case 'advice':
        drawAdvice(body, room, advice);
        break;
      case 'waiting':
        body.append(element('div', 'ema-message', 'Waiting for the opponent…'));
        break;
      case 'over':
      case 'closed':
        body.append(element('div', 'ema-message', 'Battle over.'));
        break;
      default:
        body.append(element('div', 'ema-message', advice.message || 'No advice for this choice.'));
    }

    if (advice.format && !advice.isTrainedFormat) {
      body.append(element('div', 'ema-note', `The model learned gen9randombattle: in ${advice.format} its ` +
        'advice is a guess.'));
    }
    if (room.partial && !room.complete) {
      body.append(element('div', 'ema-note', 'The server missed the start of this battle: reload the page ' +
        'for accurate advice.'));
    }
  }

  function drawAdvice(body, room, advice) {
    if (typeof advice.value === 'number') {
      const evaluation = element('div', 'ema-eval');
      evaluation.title = 'The model\'s estimate of the outcome, from -1 (lost) to +1 (won)';
      const bar = element('div', 'ema-evalbar');
      const fill = element('div', 'ema-evalfill');
      fill.style.width = `${Math.round(Math.max(0, Math.min(1, (advice.value + 1) / 2)) * 100)}%`;
      bar.append(fill);
      evaluation.append(element('span', 'ema-evallabel', 'Eval'), bar,
        element('span', 'ema-evalnum', `${advice.value >= 0 ? '+' : ''}${advice.value.toFixed(2)}`));
      body.append(evaluation);
    }

    const recommended = recommendedAction(room);
    const searched = room.search?.status === 'searched' ? room.search.action : null;
    const list = element('ol', 'ema-actions');
    for (const action of advice.actions) {
      const row = element('li', 'ema-action');
      if (action.action === recommended) {
        row.classList.add('ema-best');
      }
      if (action.veto) {
        row.classList.add('ema-vetoed');
        row.title = `Ruled out: ${action.veto}`;
      }
      const bar = element('span', 'ema-bar');
      bar.style.width = `${(action.probability * 100).toFixed(1)}%`;
      row.append(bar);

      const label = element('span', 'ema-label');
      if (action.kind === 'switch') {
        label.append(element('span', 'ema-kind', '⇄'), document.createTextNode(action.label));
      } else {
        label.append(document.createTextNode(action.label));
      }
      row.append(label);
      if (action.terastallize) {
        row.append(typeChip(action.teraType, `Tera ${action.teraType ?? ''}`.trim(), 'ema-tera'));
      } else if (action.type) {
        row.append(typeChip(action.type, action.type));
      }
      row.append(element('span', 'ema-detail', action.detail));
      if (action.action === advice.best && searched !== null) {
        row.append(element('span', 'ema-badge', 'model'));
      }
      if (searched !== null && action.action === searched) {
        row.append(element('span', 'ema-badge ema-badge-search', 'search'));
      }
      row.append(element('span', 'ema-prob', `${Math.round(action.probability * 100)}%`));
      list.append(row);
    }
    body.append(list);

    const status = searchStatus(room, advice);
    if (status) {
      body.append(element('div', 'ema-search', status));
    }
    if (room.chosen) {
      body.append(element('div', 'ema-chosen', `You chose: ${room.chosen}`));
    }
  }

  function searchStatus(room, advice) {
    const result = room.search;
    if (!result) {
      return null;
    }
    switch (result.status) {
      case 'running':
        return 'Looking one turn ahead…';
      case 'searched':
        return result.action === advice.best
          ? 'One turn ahead: the search agrees.'
          : 'One turn ahead: the search prefers another choice.';
      case 'failed':
        return `Search failed: ${result.message ?? 'no answer'}`;
      default:
        return null;
    }
  }

  function typeChip(type, text, extraClass) {
    const chip = element('span', `ema-type${extraClass ? ` ${extraClass}` : ''}`, text);
    chip.style.backgroundColor = TYPE_COLORS[type] ?? '#888';
    return chip;
  }

  function makeDraggable(root, handle) {
    handle.addEventListener('mousedown', (event) => {
      if (event.button !== 0 || event.target.closest('button')) {
        return;
      }
      event.preventDefault();
      const start = root.getBoundingClientRect();
      const offsetX = event.clientX - start.left;
      const offsetY = event.clientY - start.top;
      const move = (moveEvent) => {
        root.style.left = `${Math.max(0, Math.min(window.innerWidth - 60, moveEvent.clientX - offsetX))}px`;
        root.style.top = `${Math.max(0, Math.min(window.innerHeight - 30, moveEvent.clientY - offsetY))}px`;
        root.style.right = 'auto';
        root.style.bottom = 'auto';
      };
      const stop = () => {
        document.removeEventListener('mousemove', move);
        document.removeEventListener('mouseup', stop);
        const box = root.getBoundingClientRect();
        browser.storage.local.set({ panelPosition: { left: Math.round(box.left), top: Math.round(box.top) } });
      };
      document.addEventListener('mousemove', move);
      document.addEventListener('mouseup', stop);
    });
  }

  // ---- Showdown's buttons ----------------------------------------------------------------------------------------

  // Showdown redraws its controls (switch menu, move menu): the highlight is checked again on every tick
  function highlight(room) {
    const targets = room ? highlightTargets(room) : [];
    for (const node of highlighted) {
      if (!targets.includes(node)) {
        node.classList.remove(HIGHLIGHT_CLASS);
      }
    }
    for (const node of targets) {
      node.classList.add(HIGHLIGHT_CLASS);
    }
    highlighted = targets;
  }

  function highlightTargets(room) {
    const actionId = recommendedAction(room);
    const action = room.advice?.actions.find((candidate) => candidate.action === actionId);
    const roomElement = document.getElementById(`room-${room.roomId}`);
    if (!action || room.chosen || !roomElement) {
      return [];
    }
    const targets = [...roomElement.querySelectorAll(buttonSelector(action))];
    if (action.terastallize) {
      const tera = roomElement.querySelector(TERA_SELECTOR);
      if (tera) {
        targets.push(tera.closest('label') ?? tera);
      }
    }
    return targets;
  }

  // The choice's button in either client. "move 2": the classic client's chooseMove button of value 2, the new
  // client's "/move 2" one ("/move 2 terastallize" once its Tera box is ticked). "switch 3": the classic client's
  // chooseSwitch button of value 2 (it counts from 0), the new client's "/switch 3" one.
  function buttonSelector(action) {
    const [verb, position] = action.choice.split(' ');
    if (verb === 'switch') {
      return `button[name="chooseSwitch"][value="${Number(position) - 1}"], button[data-cmd="/switch ${position}"]`;
    }
    return `button[name="chooseMove"][value="${position}"], button[data-cmd="/move ${position}"], ` +
      `button[data-cmd^="/move ${position} "]`;
  }

  setInterval(render, TICK_MS);
})();
