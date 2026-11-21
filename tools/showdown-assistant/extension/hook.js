// Runs in the page, before Showdown's scripts, and copies the battle rooms' messages to content.js. The two clients
// reach the server differently, so both ways are wrapped:
// - the classic client (play.pokemonshowdown.com) uses SockJS, whose WebSocket transport frames messages as
//   a["...", "..."];
// - the new client (play.pokemonshowdown.com/preactalpha) connects in a Web Worker
//   (/js/client-connection-worker.js), which posts each server message to the page as { type: 'message', data }
//   and gets the page's as { type: 'send', data }. When the worker fails it falls back to a WebSocket in the page,
//   with messages as they are.
(() => {
  'use strict';

  const SOURCE = 'elsamina-battle-assistant';
  const CONNECTION_WORKER = 'connection-worker';

  function post(message) {
    window.postMessage({ source: SOURCE, ...message }, window.location.origin);
  }

  // A WebSocket payload: SockJS frames, or one message as it is
  function unframe(data) {
    if (typeof data !== 'string' || data.length === 0) {
      return [];
    }
    if (data[0] === 'a' || data[0] === '[') {
      try {
        const messages = JSON.parse(data[0] === 'a' ? data.slice(1) : data);
        return Array.isArray(messages) ? messages.filter((message) => typeof message === 'string') : [];
      } catch {
        return [];
      }
    }
    // SockJS open, heartbeat and close frames
    if (data === 'o' || data === 'h' || data[0] === 'c') {
      return [];
    }
    return [data];
  }

  function onReceived(messages) {
    for (const message of messages) {
      if (message.startsWith('>battle-')) {
        post({ frame: message });
      }
    }
  }

  // "battle-gen9randombattle-1|/choose move 1|12": the player chose (or took it back with /undo)
  function onSent(messages) {
    for (const message of messages) {
      const separator = message.indexOf('|');
      const roomId = message.slice(0, separator);
      if (separator <= 0 || !roomId.startsWith('battle-')) {
        continue;
      }
      const command = message.slice(separator + 1);
      if (command.startsWith('/choose ')) {
        post({ roomId, choice: command.slice('/choose '.length).replace(/\|\d+$/, '') });
      } else if (command.startsWith('/undo')) {
        post({ roomId, choice: '' });
      }
    }
  }

  const NativeWebSocket = window.WebSocket;
  if (NativeWebSocket && !NativeWebSocket.elsaminaHooked) {
    class HookedWebSocket extends NativeWebSocket {
      constructor(...args) {
        super(...args);
        this.addEventListener('message', (event) => onReceived(unframe(event.data)));
      }

      send(data) {
        onSent(unframe(data));
        return super.send(data);
      }
    }

    HookedWebSocket.elsaminaHooked = true;
    window.WebSocket = HookedWebSocket;
  }

  const NativeWorker = window.Worker;
  if (NativeWorker && !NativeWorker.elsaminaHooked) {
    class HookedWorker extends NativeWorker {
      #isConnection = false;

      constructor(url, options) {
        super(url, options);
        this.#isConnection = String(url).includes(CONNECTION_WORKER);
        if (this.#isConnection) {
          this.addEventListener('message', (event) => {
            if (event.data?.type === 'message' && typeof event.data.data === 'string') {
              onReceived([event.data.data]);
            }
          });
        }
      }

      postMessage(message, ...rest) {
        if (this.#isConnection && message?.type === 'send' && typeof message.data === 'string') {
          onSent([message.data]);
        }
        return super.postMessage(message, ...rest);
      }
    }

    HookedWorker.elsaminaHooked = true;
    window.Worker = HookedWorker;
  }
})();
