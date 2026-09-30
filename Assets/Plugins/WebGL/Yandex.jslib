mergeInto(LibraryManager.library, {
  NubikPlatformCapabilities: function () {
    return window.nubikPortal ? window.nubikPortal.capabilities() : 3;
  },
  NubikSafeInset: function (side) {
    return (window.nubikSafeInsets || [0, 0, 0, 0])[side] || 0;
  },
  // Music streams from plain MP3 files through Web Audio gains. Each player is unlocked by the first tap or click
  // (iPhone plays media only when a gesture started it once); afterwards the game only moves the gains. The page
  // silences it by itself while hidden: the game's frames stop there, so the game cannot do it in time.
  NubikMusicInit: function (urlsPtr) {
    var urls = UTF8ToString(urlsPtr).split(',');
    var m = window.nubikMusic = window.nubikMusic || {};
    if (m.tracks) return;
    var Context = window.AudioContext || window.webkitAudioContext;
    if (!Context) return;
    m.context = new Context();
    m.master = m.context.createGain();
    m.master.connect(m.context.destination);
    m.tracks = urls.map(function (url) {
      var element = new Audio();
      element.src = url; element.loop = true; element.preload = 'auto';
      element.setAttribute('playsinline', '');
      var gain = m.context.createGain();
      gain.gain.value = 0;
      m.context.createMediaElementSource(element).connect(gain);
      gain.connect(m.master);
      return { element: element, gain: gain, level: 0, unlocked: false };
    });
    var unlock = function () {
      if (m.context.state !== 'running') m.context.resume();
      m.tracks.forEach(function (track) {
        if (track.unlocked) return;
        track.unlocked = true;
        var started = track.element.play();
        if (started && started.then) started.then(function () {
          if (track.level <= 0 || m.paused) track.element.pause();
        }).catch(function () { track.unlocked = false; });
      });
    };
    ['touchend', 'mousedown', 'keydown'].forEach(function (name) { window.addEventListener(name, unlock, true); });
    m.gamePaused = false;
    m.apply = function () {
      var paused = m.gamePaused || document.hidden;
      if (paused === m.paused) return;
      m.paused = paused;
      if (paused) { if (m.context.state === 'running') m.context.suspend(); m.tracks.forEach(function (track) { track.element.pause(); }); return; }
      m.context.resume();
      m.tracks.forEach(function (track) {
        if (track.level > 0 && track.unlocked) { var p = track.element.play(); if (p && p.catch) p.catch(function () {}); }
      });
    };
    m.paused = false;
    document.addEventListener('visibilitychange', m.apply);
    window.addEventListener('pagehide', m.apply);
    m.apply();
  },
  NubikMusicLevel: function (index, level) {
    var m = window.nubikMusic;
    if (!m || !m.tracks || !m.tracks[index]) return;
    var track = m.tracks[index];
    track.level = level;
    track.gain.gain.setTargetAtTime(level, m.context.currentTime, 0.05);
    if (level > 0 && track.unlocked && !m.paused && track.element.paused) { var p = track.element.play(); if (p && p.catch) p.catch(function () {}); }
    if (level <= 0 && !track.element.paused) track.element.pause();
  },
  NubikMusicPause: function (paused) {
    var m = window.nubikMusic;
    if (!m || !m.tracks) return;
    m.gamePaused = !!paused;
    m.apply();
  },
  NubikPixelRatio: function () {
    return window.nubikPixelRatio || window.devicePixelRatio || 1;
  },
  NubikReady: function () {
    window.nubikReady = true;
    if (window.nubikSDK) window.nubikSDK.features.LoadingAPI?.ready();
  },
  NubikGameplay: function (active) {
    window.nubikPlaying = !!active;
    if (window.nubikSDK) {
      var gameplay = window.nubikSDK.features.GameplayAPI;
      if (active) gameplay?.start(); else gameplay?.stop();
    }
  },
  // Periodic interstitials never share reward callbacks with the voluntary videos.
  NubikShowInterstitial: function () {
    var ended = false;
    var send = function (method) { if (window.unityInstance) window.unityInstance.SendMessage('NubikGame', method, ''); };
    var finish = function (failed) {
      if (ended) return;
      ended = true;
      send(failed ? 'OnInterstitialError' : 'OnInterstitialClose');
    };
    var adv = window.nubikSDK && window.nubikSDK.adv;
    try {
      if (adv) adv.showFullscreenAdv({ callbacks: {
        onOpen: function () { if (!ended) send('OnInterstitialOpen'); },
        onClose: function () { finish(false); },
        onError: function () { finish(true); }
      } });
      else if (window.nubikFakeAd) window.nubikFakeAd(null, function (method) {
        if (method === 'OnAdOpen') send('OnInterstitialOpen');
        else if (method === 'OnAdClose') finish(false);
        else if (method === 'OnAdError') finish(true);
      });
      else finish(true);
    } catch (error) { finish(true); }
  },
  // Rewarded video. The ticket comes back with onRewarded so the game pays each ad once.
  NubikShowRewarded: function (ticket) {
    var id = String(ticket);
    var send = function (method, value) { if (window.unityInstance) window.unityInstance.SendMessage('NubikGame', method, value); };
    var adv = window.nubikSDK && window.nubikSDK.adv;
    if (adv) {
      adv.showRewardedVideo({ callbacks: {
        onOpen: function () { send('OnAdOpen', ''); },
        onRewarded: function () { send('OnAdRewarded', id); },
        onClose: function () { send('OnAdClose', ''); },
        onError: function () { send('OnAdError', ''); }
      } });
    } else if (window.nubikFakeAd) window.nubikFakeAd(id, send);
    else setTimeout(function () { send('OnAdError', ''); }, 0);
  },
  // 1: pointer locked, 2: the page cannot lock the pointer, 0: not locked yet.
  NubikPointerState: function () {
    if (document.pointerLockElement === document.getElementById('unity-canvas')) return 1;
    return window.nubikLockFailed ? 2 : 0;
  },
  // The template requests the lock from its own click handler, which counts as a user gesture.
  NubikWantLock: function (want) {
    window.nubikWantLock = !!want;
    if (!want && document.pointerLockElement) document.exitPointerLock();
    // Right after the click that closed a window the browser still treats it as the player's gesture: lock at once,
    // so the game continues without a second click.
    if (want && window.nubikRequestLock && navigator.userActivation && navigator.userActivation.isActive) window.nubikRequestLock();
  },
  NubikTakeMouseX: function () {
    var value = window.nubikMouseX || 0;
    window.nubikMouseX = 0;
    return value;
  },
  NubikTakeMouseY: function () {
    var value = window.nubikMouseY || 0;
    window.nubikMouseY = 0;
    return value;
  }
});
