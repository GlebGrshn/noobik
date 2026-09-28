mergeInto(LibraryManager.library, {
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
