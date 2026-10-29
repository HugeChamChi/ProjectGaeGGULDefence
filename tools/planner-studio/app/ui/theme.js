'use strict';
(() => {
  const key = 'ggd-planner-theme';
  let saved;
  try { saved = localStorage.getItem(key); } catch {}
  function apply(theme) {
    document.documentElement.dataset.theme = theme;
    document.querySelectorAll('iframe').forEach(frame => {
      if (frame.contentDocument) frame.contentDocument.documentElement.dataset.theme = theme;
    });
    const button = document.getElementById('themeToggle');
    if (button) {
      button.textContent = theme === 'dark' ? 'ON' : 'OFF';
      button.setAttribute('aria-checked', String(theme === 'dark'));
    }
  }
  apply(saved === 'dark' ? 'dark' : 'light');
  document.addEventListener('DOMContentLoaded', () => {
    if (window.parent !== window) {
      apply(window.parent.document.documentElement.dataset.theme || 'light');
      return;
    }
    const button = document.getElementById('themeToggle');
    button.addEventListener('click', () => {
      const theme = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
      try { localStorage.setItem(key, theme); } catch {}
      apply(theme);
    });
    apply(document.documentElement.dataset.theme);
  });
})();
