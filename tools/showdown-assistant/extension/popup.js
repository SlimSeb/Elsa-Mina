'use strict';

const DEFAULT_SETTINGS = { serverUrl: 'http://127.0.0.1:7685', enabled: true, search: true };
const ORIGINS = browser.runtime.getManifest().host_permissions;

const enabled = document.getElementById('enabled');
const search = document.getElementById('search');
const serverUrl = document.getElementById('serverUrl');
const serverStatus = document.getElementById('server');
const permissions = document.getElementById('permissions');

async function showStatus() {
  const status = await browser.runtime.sendMessage({ type: 'status' });
  if (status.error) {
    serverStatus.className = 'status error';
    serverStatus.textContent = status.error;
    return;
  }
  serverStatus.className = status.loaded ? 'status ok' : 'status error';
  const model = status.loaded ? `${status.model} ready` : `${status.model} NOT loaded (see the server's log)`;
  const lookahead = status.canSearch ? 'one-turn search on' : 'no search (--ShowdownDirectory not set)';
  serverStatus.textContent = `${model}, ${lookahead}.`;
}

async function showPermissions() {
  permissions.hidden = await browser.permissions.contains({ origins: ORIGINS });
}

async function load() {
  const settings = await browser.storage.local.get(DEFAULT_SETTINGS);
  enabled.checked = settings.enabled;
  search.checked = settings.search;
  serverUrl.value = settings.serverUrl;
  await Promise.all([showStatus(), showPermissions()]);
}

enabled.addEventListener('change', () => browser.storage.local.set({ enabled: enabled.checked }));
search.addEventListener('change', () => browser.storage.local.set({ search: search.checked }));
serverUrl.addEventListener('change', async () => {
  await browser.storage.local.set({ serverUrl: serverUrl.value.trim() || DEFAULT_SETTINGS.serverUrl });
  await showStatus();
});
document.getElementById('grant').addEventListener('click', async () => {
  await browser.permissions.request({ origins: ORIGINS });
  await showPermissions();
  await showStatus();
});

load();
