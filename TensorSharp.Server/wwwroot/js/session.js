import { state, el, DEFAULT_SESSION_ID } from './state.js';
import { updateStatusBadge } from './status.js';
import { renderEmptyState } from './chat.js';

export async function fetchServerState() {
  try {
    const res = await fetch('/api/models');
    const data = await res.json();
    state.serverDefaultMaxTokens = data.defaultMaxTokens || 20000;
    updateStatusBadge(data);
    renderEmptyState();
    await createSession();
    return data;
  } catch (e) {
    console.error('Failed to fetch server state:', e);
    return null;
  }
}

export async function createSession() {
  try {
    const res = await fetch('/api/sessions', { method: 'POST' });
    const data = await res.json();
    if (data && data.sessionId) {
      state.currentSessionId = data.sessionId;
    }
  } catch (e) {
    console.error('Failed to create chat session:', e);
    state.currentSessionId = null;
  }
}

export async function disposeCurrentSession() {
  if (!state.currentSessionId) return;
  const id = state.currentSessionId;
  state.currentSessionId = null;
  if (id === DEFAULT_SESSION_ID) return;
  try {
    await fetch('/api/sessions/' + encodeURIComponent(id), { method: 'DELETE' });
  } catch (e) {
    console.warn('Failed to dispose chat session:', e);
  }
}

export async function clearChat() {
  state.chatHistory = [];
  state.pendingAttachments = [];
  el.attachmentsDiv.innerHTML = '';
  el.chatContainer.innerHTML = '';
  state.needsCacheReset = false;
  state.shouldAutoScroll = true;
  await disposeCurrentSession();
  await createSession();
  renderEmptyState();
}

export function initSessionLifecycle() {
  window.addEventListener('beforeunload', () => {
    if (state.currentSessionId && state.currentSessionId !== DEFAULT_SESSION_ID) {
      const id = state.currentSessionId;
      state.currentSessionId = null;
      try {
        fetch('/api/sessions/' + encodeURIComponent(id), { method: 'DELETE', keepalive: true });
      } catch {}
    }
  });
}
