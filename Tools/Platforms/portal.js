/* Portal adapters for the existing Unity message ABI. No simulated ads in these packages. */
(() => {
  'use strict';
  const w = window, config = w.NUBIK_PLATFORM;
  let sdk, initialized = false, ready = false, readySent = false;
  let playing = false, playingSent = false, pending = null, progress = 0;
  const warn = error => console.warn('[Nubik/' + config.platform + ']', error);
  // Yandex Games decides the language (requirement 2.14): the game waits for its SDK; other portals ask the player.
  w.nubikPlatformLanguage = config.platform === 'YandexGames' ? 1 : 0;
  const safe = fn => { try { const result = fn(); if (result?.catch) result.catch(warn); } catch (error) { warn(error); } };
  const send = (name, value) => w.unityInstance?.SendMessage('NubikGame', name, value ? '1' : '0');
  const pause = value => { w.nubikPlatformPaused = value; send('OnPlatformPause', value); };
  const mute = value => { w.nubikPlatformMuted = value; send('OnPlatformMute', value); };
  const canvasInput = enabled => {
    const canvas = document.getElementById('unity-canvas');
    if (canvas) canvas.style.pointerEvents = enabled ? '' : 'none';
    if (!enabled && document.pointerLockElement) document.exitPointerLock?.();
  };
  function flush() {
    if (!initialized) return;
    if (ready && !readySent) {
      readySent = true;
      safe(() => {
        if (config.platform === 'CrazyGames') sdk.game.loadingStop();
        if (config.platform === 'Poki') sdk.gameLoadingFinished();
        if (config.platform === 'GamePix') { sdk.loading(100); sdk.loaded(); }
        if (config.platform === 'YandexGames') sdk.features.LoadingAPI?.ready();
      });
    }
    if (!readySent || playing === playingSent) return;
    playingSent = playing;
    safe(() => {
      if (config.platform === 'CrazyGames') sdk.game[playing ? 'gameplayStart' : 'gameplayStop']();
      if (config.platform === 'Poki') sdk[playing ? 'gameplayStart' : 'gameplayStop']();
      if (config.platform === 'YandexGames') sdk.features.GameplayAPI?.[playing ? 'start' : 'stop']();
    });
  }
  function request(rewarded, callbacks) {
    if (!initialized || pending || (rewarded && !config.rewarded)) {
      callbacks.onError?.(new Error('Ad unavailable'));
      return;
    }
    let ended = false, opened = false, rewardedOnce = false, timer;
    const transaction = {
      rewarded,
      open() {
        if (ended || opened) return;
        opened = true;
        clearTimeout(timer); // Never resume underneath an ad that is still on screen.
        callbacks.onOpen?.();
      },
      reward() {
        if (ended || !rewarded || rewardedOnce) return;
        rewardedOnce = true;
        callbacks.onRewarded?.();
      },
      finish(error) {
        if (ended) return;
        ended = true;
        clearTimeout(timer);
        if (pending === transaction) pending = null;
        canvasInput(true);
        // GD/GM own pause events must be released even if the request rejects.
        if (config.platform === 'GameDistribution' || config.platform === 'GameMonetize') pause(false);
        if (error) callbacks.onError?.(error); else callbacks.onClose?.();
      }
    };
    pending = transaction;
    canvasInput(false);
    timer = setTimeout(() => transaction.finish(new Error('Ad did not start')), 30000);
    const finish = () => transaction.finish();
    const fail = error => transaction.finish(error || new Error('Ad failed'));
    const open = () => transaction.open();
    const reward = () => transaction.reward();
    try {
      switch (config.platform) {
        case 'YandexGames':
          sdk.adv[rewarded ? 'showRewardedVideo' : 'showFullscreenAdv']({ callbacks: {
            onOpen: open, onRewarded: reward, onClose: finish, onError: fail
          } });
          break;
        case 'CrazyGames':
          sdk.ad.requestAd(rewarded ? 'rewarded' : 'midgame', {
            adStarted: open, adError: fail,
            adFinished: () => { if (rewarded) reward(); finish(); }
          });
          break;
        case 'Poki':
          Promise.resolve(sdk[rewarded ? 'rewardedBreak' : 'commercialBreak'](open))
            .then(success => { if (rewarded && success === true) reward(); finish(); }, fail);
          break;
        case 'GamePix':
          // The API has no started event; its promise owns the full pause interval.
          open();
          Promise.resolve(sdk[rewarded ? 'rewardAd' : 'interstitialAd']())
            .then(result => { if (rewarded && result?.success === true) reward(); finish(); }, fail);
          break;
        case 'GameDistribution':
          Promise.resolve(sdk.showAd(rewarded ? 'rewarded' : 'interstitial')).then(finish, fail);
          break;
        case 'GameMonetize':
          // Its public API documents only showBanner + pause/start, no completed-reward signal.
          sdk.showBanner();
          break;
        default: fail(new Error('Unknown platform'));
      }
    } catch (error) { fail(error); }
  }
  function sdkEvent(event) {
    if (event.name === 'SDK_GAME_PAUSE') { pause(true); pending?.open(); }
    if (event.name === 'SDK_GAME_START') {
      pause(false);
      // GD closes via showAd's promise so a watch-complete event cannot race a start event.
      if (config.platform === 'GameMonetize') pending?.finish();
    }
    if (event.name === 'SDK_REWARDED_WATCH_COMPLETE' && config.platform === 'GameDistribution') pending?.reward();
    if (event.name === 'SDK_ERROR' || event.name === 'AD_ERROR') {
      pending?.finish(new Error(event.name));
      pause(false);
    }
  }
  // Compatibility facade: the Unity binary does not load any particular network itself.
  w.nubikSDK = {
    features: {
      LoadingAPI: { ready() { ready = true; flush(); } },
      GameplayAPI: {
        start() { playing = true; flush(); },
        stop() { playing = false; flush(); }
      }
    },
    adv: {
      showFullscreenAdv: options => request(false, options.callbacks),
      showRewardedVideo: options => request(true, options.callbacks)
    }
  };
  w.nubikPortal = {
    status: 'loading',
    capabilities: () => (initialized && config.rewarded ? 1 : 0) | (config.menuAds ? 2 : 0),
    progress(value) {
      const next = Math.min(100, Math.max(0, Math.round(value * 100)));
      if (progress === next) return;
      progress = next;
      if (initialized && !readySent && config.platform === 'GamePix') safe(() => sdk.loading(progress));
    },
    attach() { send('OnPlatformPause', !!w.nubikPlatformPaused); send('OnPlatformMute', !!w.nubikPlatformMuted); }
  };
  window.addEventListener('keydown', event => {
    if (['ArrowDown', 'ArrowUp', 'ArrowLeft', 'ArrowRight', ' '].includes(event.key)) event.preventDefault();
    if (pending && event.target === document.getElementById('unity-canvas')) event.stopImmediatePropagation();
  }, true);
  window.addEventListener('wheel', event => event.preventDefault(), { passive: false });
  function loadScript() {
    return new Promise((resolve, reject) => {
      // For GamePix/Poki/CrazyGames this script is already the first script in <head>.
      let script = document.getElementById('nubik-portal-sdk');
      const timer = setTimeout(() => reject(new Error('SDK loading timed out')), 20000);
      const done = () => { clearTimeout(timer); resolve(); };
      const failed = () => { clearTimeout(timer); reject(new Error('SDK download failed')); };
      if (script?.dataset.loaded === '1') { done(); return; }
      if (script?.dataset.failed === '1') { failed(); return; }
      if (!script) {
        script = document.createElement('script'); script.id = 'nubik-portal-sdk';
        script.src = config.sdkUrl; script.async = true;
        script.addEventListener('load', done, { once: true });
        script.addEventListener('error', failed, { once: true });
        document.head.appendChild(script);
      } else {
        script.addEventListener('load', done, { once: true });
        script.addEventListener('error', failed, { once: true });
      }
    });
  }
  async function initialize() {
    let sdkReady;
    if (['GameDistribution', 'GameMonetize'].includes(config.platform)) {
      if (!config.gameId) throw new Error('Configure gameId in platform-config.js and repack the ZIP');
      // These SDKs may finish their own asynchronous setup after the script's load event.
      sdkReady = new Promise((resolve, reject) => {
        const timer = setTimeout(() => reject(new Error('SDK_READY timed out')), 20000);
        w[config.platform === 'GameDistribution' ? 'GD_OPTIONS' : 'SDK_OPTIONS'] = {
          gameId: config.gameId,
          onEvent(event) {
            if (event.name === 'SDK_READY') { clearTimeout(timer); resolve(); }
            if (event.name === 'SDK_ERROR') { clearTimeout(timer); reject(new Error('SDK_ERROR')); }
            sdkEvent(event);
          }
        };
      });
    }
    await Promise.all([loadScript(), sdkReady]);
    switch (config.platform) {
      case 'CrazyGames':
        sdk = w.CrazyGames.SDK; await sdk.init();
        if (sdk.environment === 'disabled') throw new Error('Use CrazyGames preview or localhost');
        sdk.game.loadingStart();
        mute(!!sdk.game.settings?.muteAudio);
        sdk.game.addSettingsChangeListener(settings => mute(!!settings.muteAudio));
        break;
      case 'Poki': sdk = w.PokiSDK; await sdk.init(); break;
      case 'GameDistribution': sdk = w.gdsdk; break;
      case 'GameMonetize': sdk = w.sdk; break;
      case 'GamePix': sdk = w.GamePix; sdk.loading(progress); break;
      case 'YandexGames':
        sdk = await w.YaGames.init();
        w.nubikPlatformLanguageFrom?.(sdk.environment?.i18n?.lang);
        sdk.on?.('game_api_pause', () => pause(true));
        sdk.on?.('game_api_resume', () => pause(false));
        break;
      default: throw new Error('Unknown platform');
    }
    if (!sdk) throw new Error('SDK object missing');
    initialized = true;
    w.nubikPortal.status = 'ready';
    flush();
  }
  w.nubikPortal.initialization = initialize().catch(error => {
    w.nubikPortal.status = 'unavailable';
    if (w.nubikPlatformLanguage === 1) w.nubikPlatformLanguage = 0; // No SDK answer: the player chooses.
    warn(error); // Offline play remains possible; no free reward and no simulated ad.
  });
})();
