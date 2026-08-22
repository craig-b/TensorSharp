import { state, el } from './state.js';

export function updateStatusBadge(data) {
  state.currentLoadedModel = data.loaded || null;
  state.currentArchitecture = data.architecture || null;

  if (!state.currentLoadedModel) {
    el.statusBadge.textContent = 'No model configured';
    el.statusBadge.classList.remove('loaded');
    return;
  }

  let statusText = state.currentLoadedModel + ' (' + (data.architecture || '?') + ')';
  if (data.loadedBackend) {
    statusText += ' | ' + data.loadedBackend;
  }
  if (data.loadedMmProj) {
    statusText += ' + ' + data.loadedMmProj;
  }

  el.statusBadge.textContent = statusText;
  el.statusBadge.classList.add('loaded');
}
