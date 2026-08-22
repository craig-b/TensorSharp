import { fetchServerState } from './session.js';

// ---- API key ---------------------------------------------------------------
// The server may be started with --api-key, in which case every API route
// returns 401 without it. The key is remembered per browser and sent as a
// Bearer header on every API call; the banner appears the first time a 401
// comes back. Without a key configured on the server none of this engages.
let apiKey = localStorage.getItem('tsApiKey') || '';

export function apiFetch(url, opts) {
  opts = opts || {};
  if (apiKey) {
    const headers = new Headers(opts.headers || {});
    headers.set('Authorization', 'Bearer ' + apiKey);
    opts = { ...opts, headers };
  }
  return fetch(url, opts).then(res => {
    if (res.status === 401) showApiKeyBanner(!!apiKey);
    return res;
  });
}

// <img>/<video>/<a download> can't send headers, so on a keyed server
// /uploads media is fetched with the key and swapped in as a blob URL.
// Unkeyed, the direct URL is returned untouched (browser caching and
// progressive video playback keep working).
export async function protectedMediaUrl(url) {
  if (!apiKey || !url || !url.startsWith('/uploads/')) return url;
  try {
    const res = await apiFetch(url);
    if (!res.ok) return url;
    return URL.createObjectURL(await res.blob());
  } catch {
    return url;
  }
}

// Resolves elements rendered with data-protected-src (media inside innerHTML
// templates, where an async URL can't be awaited inline).
export function hydrateProtectedMedia(root) {
  root.querySelectorAll('[data-protected-src]').forEach(async el => {
    el.src = await protectedMediaUrl(el.dataset.protectedSrc);
  });
}

// Blob URLs carry no filename, so a keyed download link gets the upload's
// basename as its download attribute; unkeyed links keep the plain URL.
export async function setProtectedDownload(anchor, url) {
  const resolved = await protectedMediaUrl(url);
  anchor.href = resolved;
  if (resolved !== url) {
    anchor.setAttribute('download', url.split('/').pop().split('?')[0]);
  }
}

function showApiKeyBanner(rejected) {
  const banner = document.getElementById('api-key-banner');
  if (!banner.hidden) return;
  document.getElementById('api-key-msg').textContent = rejected
    ? 'The saved API key was rejected. Enter a valid key to continue.'
    : 'This server requires an API key.';
  banner.hidden = false;
  document.getElementById('api-key-input').focus();
}

export function initApiKeyBanner() {
  document.getElementById('api-key-form').addEventListener('submit', e => {
    e.preventDefault();
    const input = document.getElementById('api-key-input');
    const key = input.value.trim();
    if (!key) return;
    apiKey = key;
    localStorage.setItem('tsApiKey', key);
    input.value = '';
    document.getElementById('api-key-banner').hidden = true;
    fetchServerState();
  });
}
