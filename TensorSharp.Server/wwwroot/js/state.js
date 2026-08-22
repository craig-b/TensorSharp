// Shared mutable UI state. Modules read and write through this object so the
// split files keep the single-page behavior of the original inline script.
export const state = {
  chatHistory: [],
  pendingAttachments: [],
  isGenerating: false,
  currentAbortController: null,
  currentLoadedModel: null,
  currentArchitecture: null,
  needsCacheReset: false,
  shouldAutoScroll: true,
  serverDefaultMaxTokens: 20000,
  currentSessionId: null,
};

// Each chat has a dedicated session on the server. The session owns its KV cache;
// starting a new chat disposes the server-side session so its KV state is freed.
export const DEFAULT_SESSION_ID = '__default__';

export const el = {
  chatContainer: document.getElementById('chat-container'),
  messageInput: document.getElementById('message-input'),
  statusBadge: document.getElementById('status-badge'),
  attachmentsDiv: document.getElementById('attachments'),
  queueBadge: document.getElementById('queue-badge'),
};
