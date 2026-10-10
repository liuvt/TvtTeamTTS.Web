// TVT Team v2.3: nhập liệu dài trên trình duyệt, tránh SignalR oninput full text.
window.tvtStudio = (() => {
  const MAX_CHARACTERS = 50000;
  const numberFormat = new Intl.NumberFormat('vi-VN');
  const wordRegex = /[\p{L}\p{M}\p{N}]+(?:['’\-][\p{L}\p{M}\p{N}]+)*/gu;
  let activeSession = null;
  let registered = false;
  let heartbeat = null;
  let editor = null;
  let dotNetCallback = null;
  let statsTimer = null;
  let lastStats = { characters: -1, words: -1 };
  let actionHandlers = null;
  let pasteTimer = null;

  function closeTab() {
    if (!activeSession) return;
    if (heartbeat) { clearInterval(heartbeat); heartbeat = null; }
    const target = `/api/session/${encodeURIComponent(activeSession)}/close`;
    try {
      if (navigator.sendBeacon) navigator.sendBeacon(target, new Blob([], { type: 'text/plain' }));
      else fetch(target, { method: 'POST', keepalive: true }).catch(() => {});
    } catch (_) { /* Có tiến trình dọn file hết hạn dự phòng. */ }
  }

  function editorElement() {
    // Không tham gia Blazor @bind: DOM giữ nội dung kể cả khi Blazor render lại.
    return editor || document.getElementById('tvt-text-editor');
  }

  function refreshEditorCount() {
    const el = editorElement();
    if (!el) return;
    const characters = el.value.length; // Khớp System.String.Length trên .NET.
    const over = characters > MAX_CHARACTERS;
    const count = document.getElementById('tvt-char-count');
    if (count) count.textContent = numberFormat.format(characters);
    document.querySelector('.site-shell')?.classList.toggle('input-too-long', over);
    const warning = document.getElementById('tvt-input-warning');
    const excessCount = document.getElementById('tvt-excess-count');
    // Explicit inline style + hidden: tránh .message{display:flex} hoặc CSS cache
    // khiến cảnh báo luôn hiển thị với "Vượt 0 ký tự".
    if (warning) {
      warning.hidden = !over;
      warning.style.display = over ? 'flex' : 'none';
    }
    if (excessCount) excessCount.textContent = numberFormat.format(Math.max(0, characters - MAX_CHARACTERS));
    const meter = document.getElementById('tvt-counter-bar');
    if (meter) meter.style.width = `${Math.min(100, (characters / MAX_CHARACTERS) * 100)}%`;
    const renderButton = document.getElementById('tvt-render-button');
    if (renderButton && over) renderButton.style.display = 'none';
    else if (renderButton) renderButton.style.display = '';

    // Đếm từ, cập nhật Blazor và hiển thị cảnh báo theo nhịp, không gửi text.
    if (statsTimer) clearTimeout(statsTimer);
    statsTimer = setTimeout(() => {
      statsTimer = null;
      const current = editorElement();
      if (!current) return;
      const value = current.value;
      const words = (value.match(wordRegex) || []).length;
      const now = { characters: value.length, words };
      const wordEl = document.getElementById('tvt-word-count');
      if (wordEl) wordEl.textContent = `${numberFormat.format(words)} từ`;
      if (now.characters === lastStats.characters && now.words === lastStats.words) return;
      lastStats = now;
      if (dotNetCallback) {
        dotNetCallback.invokeMethodAsync('UpdateEditorStats', now.characters, now.words)
          .catch(() => { /* Mất kết nối: vẫn giữ văn bản và bộ đếm ở trình duyệt. */ });
      }
    }, 180);
  }

  function showPasteStatus(message) {
    const area = document.getElementById('tvt-paste-status');
    if (!area) return;
    area.textContent = message;
    area.hidden = false;
    area.style.display = 'block';
    if (pasteTimer) clearTimeout(pasteTimer);
    pasteTimer = setTimeout(() => { area.hidden = true; area.style.display = 'none'; }, 3000);
  }

  async function pasteText() {
    const el = editorElement();
    if (!el) return;
    try {
      const text = await navigator.clipboard.readText();
      if (!text) { showPasteStatus('Bộ nhớ tạm đang trống.'); return; }
      const start = el.selectionStart ?? el.value.length;
      const end = el.selectionEnd ?? el.value.length;
      el.setRangeText(text, start, end, 'end');
      el.dispatchEvent(new Event('input', { bubbles: true }));
      el.focus();
    } catch (_) {
      showPasteStatus('Không thể đọc clipboard. Hãy nhấn Ctrl + V để dán văn bản.');
      el.focus();
    }
  }

  function clearText() {
    const el = editorElement();
    if (!el) return;
    el.value = '';
    el.dispatchEvent(new Event('input', { bubbles: true }));
    el.focus();
  }

  function attachActions() {
    const pasteButton = document.getElementById('tvt-paste-button');
    const clearButton = document.getElementById('tvt-clear-button');
    if (actionHandlers?.pasteButton === pasteButton && actionHandlers?.clearButton === clearButton) return;
    if (actionHandlers) {
      actionHandlers.pasteButton?.removeEventListener('click', pasteText);
      actionHandlers.clearButton?.removeEventListener('click', clearText);
    }
    pasteButton?.addEventListener('click', pasteText);
    clearButton?.addEventListener('click', clearText);
    actionHandlers = { pasteButton, clearButton };
  }

  return {
    register: (id, componentCallback) => {
      activeSession = id;
      dotNetCallback = componentCallback;
      const current = document.getElementById('tvt-text-editor');
      if (current && editor !== current) {
        if (editor) editor.removeEventListener('input', refreshEditorCount);
        editor = current;
        editor.addEventListener('input', refreshEditorCount);
      }
      attachActions();
      refreshEditorCount();
      if (heartbeat) clearInterval(heartbeat);
      heartbeat = setInterval(() => {
        if (activeSession)
          fetch(`/api/session/${encodeURIComponent(activeSession)}/touch`, { method: 'POST' }).catch(() => {});
      }, 5 * 60 * 1000);
      if (!registered) {
        window.addEventListener('pagehide', closeTab);
        registered = true;
      }
    },
    getEditorText: () => {
      const value = editorElement()?.value ?? '';
      if (value.length > MAX_CHARACTERS) {
        throw new Error('50000_CHARACTERS_LIMIT');
      }
      return value;
    },
    download: (url) => {
      const link = document.createElement('a');
      link.href = url;
      link.setAttribute('download', '');
      link.style.display = 'none';
      document.body.appendChild(link);
      link.click();
      link.remove();
    }
  };
})();
