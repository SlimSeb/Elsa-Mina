// Talks to the local assistant server for content.js and the popup: the extension's host permissions let it
// reach 127.0.0.1, which a page's content script could not.
'use strict';

const DEFAULT_SETTINGS = { serverUrl: 'http://127.0.0.1:7685', enabled: true, search: true };
const FRAMES_TIMEOUT_MS = 10000;
// The search gives up after 8 seconds on the server
const SEARCH_TIMEOUT_MS = 15000;
const STATUS_TIMEOUT_MS = 3000;

async function serverUrl() {
  const { serverUrl: url } = await browser.storage.local.get({ serverUrl: DEFAULT_SETTINGS.serverUrl });
  return url.replace(/\/+$/, '');
}

async function call(method, path, body, timeoutMs) {
  const url = `${await serverUrl()}${path}`;
  try {
    const response = await fetch(url, {
      method,
      headers: body === undefined ? {} : { 'Content-Type': 'application/json' },
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: AbortSignal.timeout(timeoutMs),
    });
    if (!response.ok) {
      return { error: `The server answered ${response.status} ${await response.text()}` };
    }
    return response.status === 204 ? {} : await response.json();
  } catch (error) {
    return { error: `No server at ${url.split('/').slice(0, 3).join('/')} (${error.name})`, offline: true };
  }
}

function battlePath(roomId) {
  return `/battles/${encodeURIComponent(roomId)}`;
}

browser.runtime.onMessage.addListener((message) => {
  switch (message?.type) {
    case 'frames':
      return call('POST', `${battlePath(message.roomId)}/frames`,
        { frames: message.frames, reset: message.reset }, FRAMES_TIMEOUT_MS);
    case 'search':
      return call('POST', `${battlePath(message.roomId)}/search`, { rqid: message.rqid }, SEARCH_TIMEOUT_MS);
    case 'status':
      return call('GET', '/status', undefined, STATUS_TIMEOUT_MS);
    default:
      return undefined;
  }
});

browser.runtime.onInstalled.addListener(async () => {
  const stored = await browser.storage.local.get(DEFAULT_SETTINGS);
  await browser.storage.local.set(stored);
});
