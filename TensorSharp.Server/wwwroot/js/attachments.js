import { state, el } from './state.js';

export async function handleFileSelect(event) {
  const files = Array.from(event.target.files);
  if (files.length === 0) return;
  event.target.value = '';
  // Upload sequentially so pendingAttachments keeps the user's selection order
  // (the order defines "Picture 1", "Picture 2", ... for multi-image edits).
  for (const file of files) {
    await uploadOneFile(file);
  }
}

export async function uploadOneFile(file) {
  const formData = new FormData();
  formData.append('file', file);

  try {
    const res = await fetch('/api/upload', { method: 'POST', body: formData });
    const data = await res.json();
    if (data.ok) {
      state.pendingAttachments.push(data);
      renderAttachments();
      if (data.warning) {
        alert(data.warning);
      } else if (data.renderedAsImages && data.frameUrls) {
        // A complete scanned / image-only PDF was recovered as page images.
        // The attachment chip keeps the page count visible.
      }
    } else {
      alert('Upload failed: ' + data.error);
    }
  } catch (e) {
    alert('Upload error: ' + e.message);
  }
}

export function renderAttachments() {
  el.attachmentsDiv.innerHTML = '';
  let imageNum = 0;
  const totalImages = state.pendingAttachments.filter(a => a.mediaType === 'image').length;
  state.pendingAttachments.forEach((att, idx) => {
    const chip = document.createElement('div');
    chip.className = 'attachment-chip';
    let icon = '&#x1F4C4;';
    let label = att.fileName;
    if (att.mediaType === 'image') {
      icon = '&#x1F5BC;';
      // With several images attached, show the "Picture N" order the edit prompt can reference.
      imageNum++;
      if (totalImages > 1) label = `${imageNum}⃣ ${att.fileName}`;
    }
    else if (att.mediaType === 'video') icon = '&#x1F3AC;';
    else if (att.mediaType === 'audio') icon = '&#x1F3B5;';
    else if (att.mediaType === 'text') icon = '&#x1F4DD;';
    else if (att.mediaType === 'pdf') {
      icon = '&#x1F4C4;';
      if (att.renderedAsImages && Number.isInteger(att.extractedPageCount) && Number.isInteger(att.pageCount)) {
        label = `${att.fileName} (${att.extractedPageCount}/${att.pageCount} pages)`;
      }
    }
    chip.innerHTML = `${icon} ${label} <span class="remove" data-action="remove-attachment" data-idx="${idx}">&times;</span>`;
    el.attachmentsDiv.appendChild(chip);
  });
}

export function removeAttachment(idx) {
  state.pendingAttachments.splice(idx, 1);
  renderAttachments();
}

export function describeAttachments() {
  const types = state.pendingAttachments.map(a => a.mediaType);
  if (types.includes('video')) return 'What is happening in this video?';
  if (types.includes('audio')) return 'Listen to this audio and describe what you hear.';
  if (types.includes('image')) return 'What is in this image?';
  if (types.includes('pdf')) return 'Please analyze the attached PDF document and summarize its content.';
  if (types.includes('text')) return 'Please analyze the attached text file and summarize its content.';
  return 'Describe the attached file.';
}

export function uploadUrlForAttachment(att) {
  // Formats the browser can't render (HEIC/HEIF) get a server-generated PNG preview.
  if (att && att.previewUrl) return att.previewUrl;
  if (att && att.url) return att.url;
  const raw = String((att && (att.file || att.fileName)) || '');
  if (!raw) return '';
  const normalized = raw.replace(/\\/g, '/');
  const fileName = normalized.split('/').filter(Boolean).pop() || raw;
  return '/uploads/' + encodeURIComponent(fileName);
}

export function initAttachments() {
  el.attachmentsDiv.addEventListener('click', (event) => {
    const t = event.target.closest('[data-action="remove-attachment"]');
    if (!t || !el.attachmentsDiv.contains(t)) return;
    const idx = parseInt(t.dataset.idx, 10);
    removeAttachment(idx);
  });
}
