import { el } from './state.js';
import { apiFetch } from './apikey.js';

export function pluralReq(n) {
  return n === 1 ? '1 request' : `${n} requests`;
}

export function renderQueueBadge(processing, waiting) {
  // Reset to a known-idle state, then re-apply the active classes if needed so
  // the badge color/layout always matches the current counts.
  el.queueBadge.classList.remove('queue-active', 'processing-only', 'has-waiting');
  el.queueBadge.style.removeProperty('background');
  el.queueBadge.style.removeProperty('color');

  if (processing <= 0 && waiting <= 0) {
    el.queueBadge.classList.add('hidden');
    el.queueBadge.innerHTML = '';
    return;
  }

  const segments = [`${pluralReq(processing)} processing`];
  if (waiting > 0) segments.push(`${pluralReq(waiting)} queued`);

  el.queueBadge.innerHTML = '<span class="queue-badge-dot"></span><span>' + segments.join(' · ') + '</span>';
  el.queueBadge.classList.add('queue-active', waiting > 0 ? 'has-waiting' : 'processing-only');
  el.queueBadge.title = `Live inference load: ${processing} processing concurrently, ${waiting} waiting in queue`;
  el.queueBadge.classList.remove('hidden');
}

export async function pollQueueStatus() {
  try {
    const res = await apiFetch('/api/queue/status');
    const data = await res.json();
    // `processing` is the count of requests being generated concurrently; older
    // servers only sent the boolean `busy`, so fall back to that.
    const processing = typeof data.processing === 'number'
      ? data.processing
      : (data.busy ? 1 : 0);
    const waiting = typeof data.pending_requests === 'number' ? data.pending_requests : 0;
    renderQueueBadge(processing, waiting);
  } catch {}
}

export function initQueuePolling() {
  pollQueueStatus();
  setInterval(pollQueueStatus, 1000);
}
