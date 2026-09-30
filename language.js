(() => {
  const copy = {
    en: {
      title: 'Nubik Miner', badge: 'YOUR NEXT DISCOVERY AWAITS', heading: 'NUBIK MINER',
      loading: 'Preparing your next expedition…', progress: 'Loading game',
      rotate: 'Turn your phone sideways', landscape: 'This game uses landscape mode',
      controls: 'Nubik Miner. WASD: move, left mouse: dig, Space: jump and jetpack, E: house, F: scanner, Q: medkit, Esc: pause',
      launchError: 'Could not start the game. Refresh the page. ',
      loadError: 'Could not load the game. Check your connection and refresh the page.',
      ad: 'ADVERTISEMENT', testAd: 'Test video in place of a Yandex ad'
    },
    ru: {
      title: 'Нубик Шахтёр', badge: 'НАВСТРЕЧУ НАХОДКАМ', heading: 'НУБИК ШАХТЁР',
      loading: 'Готовим твою следующую вылазку…', progress: 'Загрузка игры',
      rotate: 'Поверни телефон горизонтально', landscape: 'Игра рассчитана на горизонтальный экран',
      controls: 'Нубик Шахтёр. WASD — движение, ЛКМ — копать, пробел — прыжок и джетпак, E — дом, F — сканер, Q — аптечка, Esc — пауза',
      launchError: 'Не удалось запустить игру. Обновите страницу. ',
      loadError: 'Не удалось загрузить игру. Проверьте соединение и обновите страницу.',
      ad: 'РЕКЛАМА', testAd: 'Тестовый ролик вместо рекламы Яндекса'
    }
  };
  let language = navigator.language?.toLowerCase().startsWith('ru') ? 'ru' : 'en', chosen = false;
  try { const saved = localStorage.getItem('nubik.language'); if (copy[saved]) { language = saved; chosen = true; } } catch {}
  window.nubikPageText = key => copy[language][key];
  function apply() {
    const text = copy[language];
    document.documentElement.lang = language;
    document.title = text.title;
    for (const [id, key] of Object.entries({ 'loading-badge': 'badge', 'loading-heading': 'heading',
      'loading-caption': 'loading', 'rotate-title': 'rotate', 'rotate-note': 'landscape' })) {
      const element = document.getElementById(id); if (element) element.textContent = text[key];
    }
    document.getElementById('unity-canvas')?.setAttribute('aria-label', text.controls);
    document.getElementById('progress')?.setAttribute('aria-label', text.progress);
  }
  // The platform's language (Yandex Games: ru for ru/be/kk/uk/uz, otherwise en) replaces the browser's guess
  // until the game reports its own choice; the game reads the same answer from nubikPlatformLanguage.
  window.nubikPlatformLanguageFrom = code => {
    const value = !code ? null : ['ru', 'be', 'kk', 'uk', 'uz'].includes(String(code).toLowerCase()) ? 'ru' : 'en';
    window.nubikPlatformLanguage = value === 'ru' ? 2 : value === 'en' ? 3 : 0;
    if (value && !chosen) { language = value; apply(); }
  };
  window.nubikSetLanguage = value => {
    if (!copy[value]) return;
    language = value; chosen = true;
    try { localStorage.setItem('nubik.language', value); } catch {}
    apply();
  };
  document.addEventListener('DOMContentLoaded', apply);
})();
