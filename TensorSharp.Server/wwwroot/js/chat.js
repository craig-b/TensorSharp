import { state, el } from './state.js';
import { escapeHtml } from './util.js';
import { createSession } from './session.js';
import { describeAttachments, uploadUrlForAttachment } from './attachments.js';
import { runImageEdit, runVideoGenerate } from './media.js';

const autoScrollThreshold = 32;
const editOriginalHtml = new Map();

export function isChatNearBottom() {
  return el.chatContainer.scrollHeight - el.chatContainer.scrollTop - el.chatContainer.clientHeight <= autoScrollThreshold;
}

export function updateAutoScrollState() {
  state.shouldAutoScroll = isChatNearBottom();
}

export function scrollChatToBottom(force = false) {
  if (force || state.shouldAutoScroll) {
    el.chatContainer.scrollTop = el.chatContainer.scrollHeight;
    state.shouldAutoScroll = true;
  }
}

export function renderEmptyState() {
  if (state.chatHistory.length > 0) {
    return;
  }

  if (!state.currentLoadedModel) {
    el.chatContainer.innerHTML = `
      <div class="empty-state" id="empty-state">
        <a class="brand-home" href="https://tensorsharp.ai" target="_blank" rel="noopener" title="Visit tensorsharp.ai"><div class="big-wordmark" aria-label="TensorSharp">TensorSharp</div></a>
        <p>Start TensorSharp.Server with <code>--model &lt;path.gguf&gt; --backend &lt;type&gt; [--mmproj &lt;path&gt;] [--max-tokens 20000]</code>, then refresh this page.</p>
      </div>`;
    return;
  }

  el.chatContainer.innerHTML = `
    <div class="empty-state">
      <a class="brand-home" href="https://tensorsharp.ai" target="_blank" rel="noopener" title="Visit tensorsharp.ai"><div class="big-wordmark assistant-preview" aria-label="TensorSharp">TensorSharp</div></a>
      <p>Start a conversation with the configured model. You can attach images, videos, audio, and text files for multimodal inference.</p>
    </div>`;
}

export async function sendMessage() {
  if (state.isGenerating) return;
  const text = el.messageInput.value.trim();
  if (!text && state.pendingAttachments.length === 0) return;
  if (!state.currentLoadedModel) {
    alert('No model is configured. Restart TensorSharp.Server with --model <path.gguf> --backend <type>.');
    return;
  }

  const userContent = text || describeAttachments();
  const msg = { role: 'user', content: userContent };

  const imagePaths = [];
  const audioPaths = [];
  const textFilePaths = [];
  let isVideo = false;
  const textParts = [];
  const attachmentsCopy = [...state.pendingAttachments];

  attachmentsCopy.forEach(att => {
    if (att.mediaType === 'image') {
      imagePaths.push(att.file);
    } else if (att.mediaType === 'video') {
      isVideo = true;
      if (att.frames) att.frames.forEach(f => imagePaths.push(f));
    } else if (att.mediaType === 'audio') {
      audioPaths.push(att.file);
    } else if (att.mediaType === 'text' && att.textContent) {
      textParts.push(`[File: ${att.fileName}]\n${att.textContent}\n[End of file]`);
      if (att.file) textFilePaths.push(att.file);
    } else if (att.mediaType === 'pdf') {
      if (att.textContent) {
        // Born-digital PDF: inline the extracted text.
        textParts.push(`[File: ${att.fileName}]\n${att.textContent}\n[End of file]`);
        if (att.file) textFilePaths.push(att.file);
      } else if (att.frames && att.frames.length) {
        // Scanned / image-only PDF: send its page images to the vision model.
        // Keep the original PDF file as document provenance too, so the server
        // never silently trims pages if the expanded vision prompt is too large.
        if (att.file) textFilePaths.push(att.file);
        att.frames.forEach(f => imagePaths.push(f));
      }
    }
  });

  if (textParts.length > 0) {
    const fileContext = textParts.join('\n\n');
    msg.content = fileContext + '\n\n' + msg.content;
  }

  if (imagePaths.length > 0) msg.imagePaths = imagePaths;
  if (audioPaths.length > 0) msg.audioPaths = audioPaths;
  if (textFilePaths.length > 0) msg.textFilePaths = textFilePaths;
  if (isVideo) msg.isVideo = true;

  state.chatHistory.push(msg);
  addUserBubble(userContent, attachmentsCopy, state.chatHistory.length - 1);
  el.messageInput.value = '';
  el.messageInput.style.height = 'auto';
  state.pendingAttachments = [];
  el.attachmentsDiv.innerHTML = '';

  const empt = el.chatContainer.querySelector('.empty-state');
  if (empt) empt.remove();

  await requestAssistantResponse();
}

export async function requestAssistantResponse() {
  if (state.isGenerating) return;
  if (!state.currentLoadedModel) return;

  state.isGenerating = true;
  state.currentAbortController = new AbortController();
  setSendButtonState(true);

  const assistDiv = addAssistantBubble();
  const bubbleText = assistDiv.querySelector('.bubble-text');
  const statsDiv = assistDiv.querySelector('.stats');
  const queueDiv = assistDiv.querySelector('.queue-indicator');

  let fullText = '';
  let fullThinking = '';
  let wasAborted = false;

  try {
    if (!state.currentSessionId) {
      await createSession();
    }

    // Qwen-Image-Edit: an attached image + prompt produces a NEW image, not chat text.
    const _lastUser = state.chatHistory[state.chatHistory.length - 1];
    if (state.currentArchitecture === 'qwen_image' && _lastUser && _lastUser.imagePaths && _lastUser.imagePaths.length > 0) {
      await runImageEdit(_lastUser, bubbleText, statsDiv, queueDiv);
      return;
    }

    // Wan video generation: the prompt (plus an optional attached image as the
    // first frame, Wan 2.2 image-to-video) produces a video, not chat text.
    if (state.currentArchitecture === 'wan' && _lastUser) {
      await runVideoGenerate(_lastUser, bubbleText, statsDiv, queueDiv);
      return;
    }

    const reasoningOn = document.getElementById('reasoning-toggle').checked;
    const chatPayload = { messages: state.chatHistory, maxTokens: state.serverDefaultMaxTokens, think: reasoningOn };
    if (state.currentSessionId) {
      chatPayload.sessionId = state.currentSessionId;
    }
    if (state.needsCacheReset) {
      chatPayload.newChat = true;
      state.needsCacheReset = false;
    }

    let res = await fetch('/api/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(chatPayload),
      signal: state.currentAbortController.signal
    });

    if (res.status === 404 && chatPayload.sessionId) {
      // Session vanished on the server (e.g. restart); recreate and retry once.
      state.currentSessionId = null;
      await createSession();
      if (state.currentSessionId) {
        chatPayload.sessionId = state.currentSessionId;
        chatPayload.newChat = true;
        res = await fetch('/api/chat', {
          method: 'POST',
          headers: { 'Content-Type': 'application/json' },
          body: JSON.stringify(chatPayload),
          signal: state.currentAbortController.signal
        });
      }
    }

    if (!res.ok) {
      let detail = `HTTP ${res.status}`;
      try {
        const errorText = await res.text();
        if (errorText) {
          try {
            const errorBody = JSON.parse(errorText);
            detail = errorBody.error || errorBody.detail || errorBody.message || errorText;
          } catch {
            detail = errorText;
          }
        }
      } catch {}
      throw new Error(detail);
    }

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    let queueCleared = false;

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });

      const lines = buffer.split('\n');
      buffer = lines.pop();

      for (const line of lines) {
        if (!line.startsWith('data: ')) continue;
        try {
          const data = JSON.parse(line.slice(6));
          if (data.queue_position !== undefined) {
            if (queueDiv) {
              queueDiv.classList.remove('hidden');
              queueDiv.textContent = '';
              const spinner = document.createElement('div');
              spinner.className = 'queue-spinner';
              queueDiv.appendChild(spinner);
              const txt = document.createElement('span');
              txt.textContent = data.queue_position === 1
                ? 'Next in queue (1 request ahead)'
                : `Position ${data.queue_position} in queue (${data.queue_position} requests ahead)`;
              queueDiv.appendChild(txt);
              scrollChatToBottom();
            }
            continue;
          }
          if (!queueCleared && queueDiv) {
            queueDiv.classList.add('hidden');
            queueCleared = true;
          }
          if (data.thinking) {
            fullThinking += data.thinking;
            let thinkBlock = assistDiv.querySelector('.thinking-block');
            if (!thinkBlock) {
              thinkBlock = document.createElement('div');
              thinkBlock.className = 'thinking-block';
              const expanded = reasoningOn ? ' expanded' : '';
              const visible = reasoningOn ? ' visible' : '';
              const display = reasoningOn ? '' : ' hidden';
              thinkBlock.innerHTML = `<div class="thinking-header${display}" data-action="toggle-thinking"><span class="arrow${expanded}">&#x25B6;</span> Reasoning</div><div class="thinking-content${visible}"></div>`;
              bubbleText.parentNode.insertBefore(thinkBlock, bubbleText);
            }
            const tc = thinkBlock.querySelector('.thinking-content');
            tc.textContent += data.thinking;
            if (reasoningOn) scrollChatToBottom();
          }
          if (data.token) {
            fullText += data.token;
            bubbleText.textContent = fullText;
            scrollChatToBottom();
          }
          if (data.replace !== undefined) {
            // DiffusionGemma live denoising preview: each frame refines the whole
            // canvas, so replace (not append) the assistant message body.
            fullText = data.replace;
            bubbleText.textContent = fullText;
            if (data.preview && data.diffusionTotal > 0) {
              statsDiv.textContent = `Denoising\u2026 step ${data.diffusionStep}/${data.diffusionTotal}`;
            }
            scrollChatToBottom();
          }
          if (data.done) {
            if (data.sessionId) state.currentSessionId = data.sessionId;
            if (data.error) {
              bubbleText.textContent = 'Error: ' + data.error;
              statsDiv.textContent = 'Inference failed';
            } else {
              let stats = `${data.tokenCount} tokens \u00B7 ${data.elapsed.toFixed(1)}s \u00B7 ${data.tokPerSec.toFixed(1)} tok/s`;
              if (typeof data.promptTokens === 'number' && data.promptTokens > 0) {
                const reused = typeof data.kvReusedTokens === 'number' ? data.kvReusedTokens : 0;
                const pct = typeof data.kvReusePercent === 'number'
                  ? data.kvReusePercent
                  : (100 * reused / data.promptTokens);
                stats += ` \u00B7 KV ${reused}/${data.promptTokens} (${pct.toFixed(0)}%)`;
              }
              // The reply stopped because it ran out of max-tokens budget, not
              // because the model was done. Say so, otherwise an answer that
              // ends mid-word just looks like the model broke.
              if (data.truncated) stats += ' \u00B7 truncated (max tokens reached)';
              statsDiv.textContent = stats;
            }
          }
        } catch {}
      }
    }
  } catch (e) {
    if (e.name === 'AbortError') {
      wasAborted = true;
      statsDiv.textContent = 'Stopped by user';
    } else {
      bubbleText.textContent = 'Error: ' + e.message;
    }
  } finally {
    if (fullText) {
      const assistantMessage = { role: 'assistant', content: fullText };
      if (fullThinking) assistantMessage.thinking = fullThinking;
      state.chatHistory.push(assistantMessage);

      const assistIdx = state.chatHistory.length - 1;
      assistDiv.dataset.idx = assistIdx;
      const bubble = assistDiv.querySelector('.bubble');
      const actionsDiv = document.createElement('div');
      actionsDiv.className = 'msg-actions';
      actionsDiv.innerHTML = `<button class="danger" data-action="revert" data-idx="${assistIdx}" title="Remove this response">&#x21A9; Revert</button>`;
      bubble.appendChild(actionsDiv);
    }
    state.isGenerating = false;
    state.currentAbortController = null;
    setSendButtonState(false);
    const indicator = assistDiv.querySelector('.typing-indicator');
    if (indicator) indicator.remove();
    const queueInd = assistDiv.querySelector('.queue-indicator');
    if (queueInd) queueInd.classList.add('hidden');
  }
}

export function revertFrom(historyIdx) {
  if (state.isGenerating) return;

  state.chatHistory.splice(historyIdx);

  el.chatContainer.querySelectorAll('.message').forEach(el => {
    if (parseInt(el.dataset.idx) >= historyIdx) el.remove();
  });

  state.needsCacheReset = true;
  if (state.chatHistory.length === 0) {
    renderEmptyState();
  }
}

export function startEdit(historyIdx) {
  if (state.isGenerating) return;

  const msgEl = el.chatContainer.querySelector(`.message[data-idx="${historyIdx}"]`);
  if (!msgEl) return;

  const bubble = msgEl.querySelector('.bubble');
  const originalText = state.chatHistory[historyIdx].content;

  editOriginalHtml.set(historyIdx, bubble.innerHTML);

  const mediaPreviews = bubble.querySelectorAll('.media-preview');
  let mediaHtml = '';
  mediaPreviews.forEach(mp => { mediaHtml += mp.outerHTML; });

  bubble.innerHTML = `${mediaHtml}<textarea class="edit-textarea">${escapeHtml(originalText)}</textarea>
    <div class="edit-actions">
      <button data-action="cancel-edit" data-idx="${historyIdx}">Cancel</button>
      <button class="primary" data-action="submit-edit" data-idx="${historyIdx}">Save &amp; Send</button>
    </div>`;

  const textarea = bubble.querySelector('.edit-textarea');
  textarea.focus();
  textarea.setSelectionRange(textarea.value.length, textarea.value.length);
}

export function cancelEdit(historyIdx) {
  const msgEl = el.chatContainer.querySelector(`.message[data-idx="${historyIdx}"]`);
  if (!msgEl) return;

  const bubble = msgEl.querySelector('.bubble');
  const original = editOriginalHtml.get(historyIdx);
  if (original) {
    bubble.innerHTML = original;
    editOriginalHtml.delete(historyIdx);
  }
}

export async function submitEdit(historyIdx) {
  const msgEl = el.chatContainer.querySelector(`.message[data-idx="${historyIdx}"]`);
  if (!msgEl) return;

  const bubble = msgEl.querySelector('.bubble');
  const textarea = bubble.querySelector('.edit-textarea');
  const newText = textarea.value.trim();
  if (!newText) return;

  editOriginalHtml.delete(historyIdx);

  state.chatHistory[historyIdx].content = newText;
  state.chatHistory.splice(historyIdx + 1);

  el.chatContainer.querySelectorAll('.message').forEach(el => {
    if (parseInt(el.dataset.idx) > historyIdx) el.remove();
  });

  const mediaPreviews = bubble.querySelectorAll('.media-preview');
  let mediaHtml = '';
  mediaPreviews.forEach(mp => { mediaHtml += mp.outerHTML; });

  bubble.innerHTML = `${mediaHtml}<span class="bubble-text">${escapeHtml(newText)}</span>
    <div class="msg-actions">
      <button data-action="start-edit" data-idx="${historyIdx}" title="Edit this message">&#x270E; Edit</button>
      <button class="danger" data-action="revert" data-idx="${historyIdx}" title="Revert from here">&#x21A9; Revert</button>
    </div>`;

  state.needsCacheReset = true;
  await requestAssistantResponse();
}

export function abortGeneration() {
  if (state.currentAbortController) {
    state.currentAbortController.abort();
  }
}

export function setSendButtonState(generating) {
  const btn = document.getElementById('btn-send');
  if (generating) {
    btn.className = 'stop-btn';
    btn.innerHTML = '&#x25A0;';
    btn.title = 'Stop generating';
  } else {
    btn.className = 'send-btn';
    btn.innerHTML = '&#x27A4;';
    btn.title = 'Send message';
  }
}

export function addUserBubble(text, attachments, historyIdx) {
  const div = document.createElement('div');
  div.className = 'message user';
  div.dataset.idx = historyIdx;

  let mediaHtml = '';
  attachments.forEach(att => {
    if (att.mediaType === 'image') {
      mediaHtml += `<div class="media-preview"><img src="${escapeHtml(uploadUrlForAttachment(att))}" alt="uploaded"></div>`;
    } else if (att.mediaType === 'video') {
      mediaHtml += `<div class="media-preview"><div class="audio-label">&#x1F3AC; ${escapeHtml(att.fileName || '')}</div></div>`;
    } else if (att.mediaType === 'audio') {
      mediaHtml += `<div class="media-preview"><div class="audio-label">&#x1F3B5; ${escapeHtml(att.fileName || '')}</div></div>`;
    } else if (att.mediaType === 'text') {
      mediaHtml += `<div class="media-preview"><div class="audio-label">&#x1F4DD; ${escapeHtml(att.fileName || '')}</div></div>`;
    } else if (att.mediaType === 'pdf') {
      if (att.renderedAsImages && att.frameUrls && att.frameUrls.length) {
        const n = att.frameUrls.length;
        const pageStatus = Number.isInteger(att.extractedPageCount) && Number.isInteger(att.pageCount)
          ? `${att.extractedPageCount}/${att.pageCount} pages`
          : `${n} page image${n > 1 ? 's' : ''}`;
        mediaHtml += `<div class="media-preview"><img src="${escapeHtml(att.frameUrls[0])}" alt="pdf page"></div>`;
        mediaHtml += `<div class="audio-label">&#x1F4C4; ${escapeHtml(att.fileName || '')} (${pageStatus})</div>`;
      } else {
        mediaHtml += `<div class="media-preview"><div class="audio-label">&#x1F4C4; ${escapeHtml(att.fileName || '')}</div></div>`;
      }
    }
  });

  div.innerHTML = `
    <div class="avatar">U</div>
    <div class="bubble">${mediaHtml}<span class="bubble-text">${escapeHtml(text)}</span>
      <div class="msg-actions">
        <button data-action="start-edit" data-idx="${historyIdx}" title="Edit this message">&#x270E; Edit</button>
        <button class="danger" data-action="revert" data-idx="${historyIdx}" title="Revert from here">&#x21A9; Revert</button>
      </div>
    </div>`;
  el.chatContainer.appendChild(div);
  scrollChatToBottom();
}

export function addAssistantBubble() {
  const div = document.createElement('div');
  div.className = 'message assistant';
  div.innerHTML = `
    <div class="avatar"><img src="/images/assistant_logo.png" alt=""></div>
    <div class="bubble">
      <div class="queue-indicator hidden"><div class="queue-spinner"></div><span>Waiting in queue...</span></div>
      <span class="bubble-text"></span>
      <div class="typing-indicator"><span></span><span></span><span></span></div>
      <div class="stats"></div>
    </div>`;
  el.chatContainer.appendChild(div);
  scrollChatToBottom();
  return div;
}

export function toggleThinking(header) {
  const arrow = header.querySelector('.arrow');
  const content = header.nextElementSibling;
  arrow.classList.toggle('expanded');
  content.classList.toggle('visible');
}

export function initChat() {
  el.chatContainer.addEventListener('scroll', updateAutoScrollState);

  el.messageInput.addEventListener('keydown', e => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  });

  el.messageInput.addEventListener('input', () => {
    el.messageInput.style.height = 'auto';
    el.messageInput.style.height = Math.min(el.messageInput.scrollHeight, 200) + 'px';
  });

  el.chatContainer.addEventListener('click', (event) => {
    const t = event.target.closest('[data-action]');
    if (!t || !el.chatContainer.contains(t)) return;
    const action = t.dataset.action;
    if (action === 'toggle-thinking') {
      toggleThinking(t);
      return;
    }
    const idx = parseInt(t.dataset.idx, 10);
    if (action === 'start-edit') startEdit(idx);
    else if (action === 'revert') revertFrom(idx);
    else if (action === 'cancel-edit') cancelEdit(idx);
    else if (action === 'submit-edit') submitEdit(idx);
  });
}
