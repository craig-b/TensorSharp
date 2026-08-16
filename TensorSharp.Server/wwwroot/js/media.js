import { state } from './state.js';
import { scrollChatToBottom } from './chat.js';

export async function runImageEdit(userMsg, bubbleText, statsDiv, queueDiv) {
  if (queueDiv) queueDiv.classList.add('hidden');
  bubbleText.textContent = 'Starting edit… (preparing the diffusion pipeline)';
  statsDiv.textContent = 'Qwen-Image-Edit';
  const t0 = performance.now();

  // The image element is created lazily on the first frame, then refreshed in place as the
  // denoise progresses so the user sees the picture forming (rather than a frozen "generating…").
  let img = null;
  function ensureImg() {
    if (img) return img;
    bubbleText.innerHTML = '';
    img = document.createElement('img');
    img.alt = 'edited image (denoising…)';
    img.style.maxWidth = '512px';
    img.style.maxHeight = '512px';
    img.style.borderRadius = '8px';
    img.style.display = 'block';
    img.style.imageRendering = 'auto';
    bubbleText.appendChild(img);
    return img;
  }

  try {
    const res = await fetch('/api/image-edit/stream', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ imagePaths: userMsg.imagePaths, prompt: userMsg.content || '' }),
      signal: state.currentAbortController.signal
    });
    if (!res.ok) {
      let err = 'HTTP ' + res.status;
      try { const j = await res.json(); if (j && j.error) err = j.error; } catch {}
      bubbleText.textContent = 'Image edit failed: ' + err;
      statsDiv.textContent = 'Failed';
      return;
    }

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop();
      for (const line of lines) {
        if (!line.startsWith('data: ')) continue;
        let data;
        try { data = JSON.parse(line.slice(6)); } catch { continue; }

        if (data.imageEdit) {
          if (data.image) { ensureImg().src = data.image; }
          if (data.total > 0) statsDiv.textContent = `Denoising… step ${data.step}/${data.total}`;
          scrollChatToBottom();
        } else if (data.done) {
          if (data.error) {
            bubbleText.textContent = 'Image edit failed: ' + data.error;
            statsDiv.textContent = 'Failed';
          } else {
            ensureImg().src = data.url + '?t=' + Date.now();  // final full-resolution result
            img.alt = 'edited image';
            const dl = document.createElement('a');
            dl.href = data.url;
            dl.setAttribute('download', '');
            dl.textContent = '⬇ Download image';
            dl.style.cssText = 'display:inline-block;margin-top:8px;color:var(--accent);';
            bubbleText.appendChild(dl);
            statsDiv.textContent = `Edited ${data.width}×${data.height} · ${((performance.now() - t0) / 1000).toFixed(1)}s`;
          }
          scrollChatToBottom();
        }
      }
    }
  } catch (e) {
    if (e.name === 'AbortError') { if (!img) bubbleText.textContent = 'Image edit cancelled.'; statsDiv.textContent = 'Stopped by user'; }
    else { bubbleText.textContent = 'Image edit error: ' + e.message; statsDiv.textContent = 'Failed'; }
  }
}

// "1h 04m" / "7m 12s" / "43s"; '' when the value is missing or not yet known.
function fmtDuration(s) {
  if (typeof s !== 'number' || !(s > 0)) return '';
  if (s >= 3600) return `${Math.floor(s / 3600)}h ${String(Math.floor((s % 3600) / 60)).padStart(2, '0')}m`;
  if (s >= 60) return `${Math.floor(s / 60)}m ${String(Math.floor(s % 60)).padStart(2, '0')}s`;
  return `${Math.round(s)}s`;
}

export async function runVideoGenerate(userMsg, bubbleText, statsDiv, queueDiv) {
  if (queueDiv) queueDiv.classList.add('hidden');
  bubbleText.textContent = 'Starting video generation… (this takes a few minutes)';
  const i2vImage = userMsg.imagePaths && userMsg.imagePaths.length > 0 ? userMsg.imagePaths[0] : null;
  statsDiv.textContent = i2vImage ? 'Wan Image-to-Video' : 'Wan Text-to-Video';
  const t0 = performance.now();

  try {
    const payload = { prompt: userMsg.content || '' };
    if (i2vImage) payload.imagePath = i2vImage;
    const res = await fetch('/api/video-generate/stream', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload),
      signal: state.currentAbortController.signal
    });
    if (!res.ok) {
      let err = 'HTTP ' + res.status;
      try { const j = await res.json(); if (j && j.error) err = j.error; } catch {}
      bubbleText.textContent = 'Video generation failed: ' + err;
      statsDiv.textContent = 'Failed';
      return;
    }

    const reader = res.body.getReader();
    const decoder = new TextDecoder();
    let buffer = '';
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      const lines = buffer.split('\n');
      buffer = lines.pop();
      for (const line of lines) {
        if (!line.startsWith('data: ')) continue;
        let data;
        try { data = JSON.parse(line.slice(6)); } catch { continue; }

        if (data.videoGen) {
          // A single denoising pass at 720p/121 frames runs for minutes, so the
          // server also sends heartbeats carrying the phase and a running ETA.
          // Show those, otherwise the UI looks frozen between steps.
          const eta = fmtDuration(data.etaSeconds);
          const el = fmtDuration(data.elapsedSeconds);
          if (data.phase && data.phase !== 'denoise') {
            const label = data.phase === 'text-encode' ? 'Encoding prompt'
                        : data.phase === 'image-encode' ? 'Encoding the input image'
                        : data.phase === 'vae-decode' ? 'Decoding frames'
                        : data.phase === 'done' ? 'Finishing' : data.phase;
            statsDiv.textContent = `${label}… ${el}`;
            bubbleText.textContent = `${label}${data.detail ? ' (' + data.detail + ')' : ''}…`;
          } else if (data.total > 0) {
            statsDiv.textContent = `Denoising… step ${data.step}/${data.total}` + (eta ? ` · ${eta} left` : '');
            bubbleText.textContent = `Generating video… step ${data.step} of ${data.total}`
              + (data.detail ? ` — ${data.detail}` : '')
              + (eta ? ` · about ${eta} left` : '')
              + (el ? ` · ${el} elapsed` : '');
          }
        } else if (data.done) {
          if (data.error) {
            bubbleText.textContent = 'Video generation failed: ' + data.error;
            statsDiv.textContent = 'Failed';
          } else {
            bubbleText.innerHTML = '';
            const vid = document.createElement('video');
            vid.src = data.url;
            vid.controls = true;
            vid.autoplay = true;
            vid.loop = true;
            vid.muted = true;
            vid.playsInline = true;
            vid.style.maxWidth = '640px';
            vid.style.maxHeight = '480px';
            vid.style.borderRadius = '8px';
            vid.style.display = 'block';
            bubbleText.appendChild(vid);
            const dl = document.createElement('a');
            dl.href = data.url;
            dl.setAttribute('download', '');
            dl.textContent = '⬇ Download video';
            dl.style.cssText = 'display:inline-block;margin-top:8px;color:var(--accent);';
            bubbleText.appendChild(dl);
            statsDiv.textContent = `${data.width}×${data.height} · ${data.frames} frames @ ${data.fps} fps · seed ${data.seed} · ${((performance.now() - t0) / 1000).toFixed(1)}s`;
          }
          scrollChatToBottom();
        }
      }
    }
  } catch (e) {
    if (e.name === 'AbortError') { bubbleText.textContent = 'Video generation cancelled.'; statsDiv.textContent = 'Stopped by user'; }
    else { bubbleText.textContent = 'Video generation error: ' + e.message; statsDiv.textContent = 'Failed'; }
  }
}
