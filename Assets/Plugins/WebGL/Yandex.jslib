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
  }
});
