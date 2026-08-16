import { state } from './state.js';
import { initChat, sendMessage, abortGeneration } from './chat.js';
import { initAttachments, handleFileSelect } from './attachments.js';
import { initSessionLifecycle, clearChat, fetchServerState } from './session.js';
import { initQueuePolling } from './queue.js';

document.getElementById('btn-clear').onclick = clearChat;

document.getElementById('btn-attach').addEventListener('click', () => document.getElementById('file-input').click());

document.getElementById('btn-send').addEventListener('click', () => {
  if (state.isGenerating) abortGeneration();
  else sendMessage();
});

document.getElementById('file-input').addEventListener('change', handleFileSelect);

initChat();
initAttachments();
initSessionLifecycle();

fetchServerState();

initQueuePolling();
