import assert from 'node:assert/strict';
import fs from 'node:fs';
import vm from 'node:vm';

const source = fs.readFileSync(new URL('./Platforms/portal.js', import.meta.url), 'utf8');
const bridge = fs.readFileSync(new URL('../Assets/Plugins/WebGL/Yandex.jslib', import.meta.url), 'utf8');
const pageLanguage = fs.readFileSync(new URL('../Assets/WebGLTemplates/Nubik/language.js', import.meta.url), 'utf8');
let passed = 0;
const tick = async () => { for (let i = 0; i < 12; i++) await Promise.resolve(); };
async function harness(platform, options = {}) {
  const messages = [], calls = [], timers = new Map(), events = {}, api = {};
  let ad, resolveAd, rejectAd, settingsListener, nextTimer = 0;
  const later = () => new Promise((resolve, reject) => { resolveAd = resolve; rejectAd = reject; });
  const canvas = { style: {}, setAttribute() {} };
  const sdkScript = { dataset: { loaded: '1' } };
  const sdk = {
    features: { LoadingAPI: { ready: () => calls.push('ready') }, GameplayAPI: { start: () => calls.push('start'), stop: () => calls.push('stop') } },
    adv: { showFullscreenAdv: x => { ad = x.callbacks; }, showRewardedVideo: x => { ad = x.callbacks; } },
    on: (name, fn) => { events[name] = fn; },
    init: () => options.initError ? Promise.reject(new Error('offline')) : Promise.resolve(),
    environment: platform === 'YandexGames' ? { i18n: { lang: options.lang } } : 'local',
    game: { loadingStart: () => calls.push('loading'), loadingStop: () => calls.push('ready'),
      gameplayStart: () => calls.push('start'), gameplayStop: () => calls.push('stop'),
      settings: { muteAudio: true }, addSettingsChangeListener: fn => { settingsListener = fn; } },
    ad: { requestAd: (type, callbacks) => { ad = callbacks; calls.push(type); } },
    gameLoadingFinished: () => calls.push('ready'), gameplayStart: () => calls.push('start'), gameplayStop: () => calls.push('stop'),
    rewardedBreak: open => { ad = { open }; return later(); }, commercialBreak: open => { ad = { open }; return later(); },
    loading: value => calls.push('loading:' + value), loaded: () => calls.push('ready'),
    interstitialAd: later, rewardAd: later, showAd: type => { calls.push(type); return later(); },
    showBanner: () => calls.push('banner')
  };
  const page = {
    NUBIK_PLATFORM: { platform, sdkUrl: '/sdk.js', gameId: options.missingId ? '' : 'test-id-only',
      rewarded: platform !== 'GameMonetize', menuAds: platform !== 'CrazyGames' },
    CrazyGames: { SDK: sdk }, PokiSDK: sdk, GamePix: sdk, gdsdk: sdk, sdk,
    YaGames: { init: () => options.initError ? Promise.reject(new Error('offline')) : Promise.resolve(sdk) },
    unityInstance: { SendMessage: (object, method, value) => messages.push([method, value]) },
    addEventListener: (name, fn) => { events[name] = fn; }
  };
  const context = vm.createContext({ window: page, document: {
    getElementById: id => id === 'unity-canvas' ? canvas : sdkScript,
    pointerLockElement: null, documentElement: {}, addEventListener() {}
  }, navigator: { language: 'de-DE' }, localStorage: { getItem: () => options.savedLanguage ?? null, setItem() {} },
  console: { warn() {} }, setTimeout: (fn, ms) => { const id = ++nextTimer; timers.set(id, { fn, ms }); return id; },
  clearTimeout: id => timers.delete(id), LibraryManager: { library: api }, mergeInto: Object.assign });
  vm.runInContext(source, context);
  vm.runInContext(pageLanguage, context); // Loaded after platform.js in the packages, before the SDK answers.
  vm.runInContext(bridge, context);
  if (!options.missingId) (page.GD_OPTIONS || page.SDK_OPTIONS)?.onEvent({ name: 'SDK_READY' });
  api.NubikReady(); api.NubikGameplay(1); // Unity can become ready before async SDK init.
  await tick();
  return { page, sdk, calls, messages, events, api, canvas, timers,
    ad: () => ad, resolve: value => resolveAd(value), reject: () => rejectAd(new Error('offline')),
    event: name => (page.GD_OPTIONS || page.SDK_OPTIONS).onEvent({ name }),
    settings: value => settingsListener({ muteAudio: value }),
    methods: () => messages.map(x => x[0]),
    expire: () => { for (const { fn, ms } of [...timers.values()]) if (ms === 30000) fn(); }
  };
}
async function check(name, fn) { await fn(); passed++; console.log('PASS ' + name); }

for (const platform of ['CrazyGames', 'Poki', 'GameDistribution', 'GameMonetize', 'GamePix', 'YandexGames']) {
  await check(platform + ': initialize, late-ready, capabilities', async () => {
    const h = await harness(platform);
    assert.equal(h.page.nubikPortal.status, 'ready');
    assert.equal(h.api.NubikPlatformCapabilities() & 1, platform === 'GameMonetize' ? 0 : 1);
    assert.equal(h.api.NubikPlatformCapabilities() & 2, platform === 'CrazyGames' ? 0 : 2);
    h.api.NubikReady(); h.api.NubikGameplay(1); h.api.NubikGameplay(1);
    if (!['GameDistribution', 'GameMonetize'].includes(platform)) assert.equal(h.calls.filter(x => x === 'ready').length, 1);
    if (['CrazyGames', 'Poki', 'YandexGames'].includes(platform)) assert.equal(h.calls.filter(x => x === 'start').length, 1);
  });
}
for (const platform of ['CrazyGames', 'Poki', 'GameDistribution', 'GamePix', 'YandexGames']) {
  await check(platform + ': confirmed reward delivered once through Unity ABI', async () => {
    const h = await harness(platform); h.messages.length = 0;
    h.api.NubikShowRewarded(17);
    assert.equal(h.canvas.style.pointerEvents, 'none');
    if (platform === 'CrazyGames') { h.ad().adStarted(); h.ad().adFinished(); h.ad().adFinished(); }
    if (platform === 'YandexGames') { h.ad().onOpen(); h.ad().onRewarded(); h.ad().onRewarded(); h.ad().onClose(); h.ad().onClose(); }
    if (platform === 'Poki') { h.ad().open(); h.resolve(true); }
    if (platform === 'GamePix') h.resolve({ success: true });
    if (platform === 'GameDistribution') { h.event('SDK_GAME_PAUSE'); h.event('SDK_GAME_START'); h.event('SDK_REWARDED_WATCH_COMPLETE'); h.event('SDK_REWARDED_WATCH_COMPLETE'); h.resolve(); }
    await tick();
    assert.equal(h.methods().filter(x => x === 'OnAdRewarded').length, 1);
    assert.equal(h.messages.find(x => x[0] === 'OnAdRewarded')[1], '17');
    assert.equal(h.methods().filter(x => x === 'OnAdClose').length, 1);
    assert.equal(h.canvas.style.pointerEvents, '');
  });
  await check(platform + ': failure has no reward and restores input', async () => {
    const h = await harness(platform); h.messages.length = 0;
    h.api.NubikShowRewarded(9);
    if (platform === 'CrazyGames') { h.ad().adError(new Error()); h.ad().adFinished(); }
    else if (platform === 'YandexGames') { h.ad().onError(new Error()); h.ad().onRewarded(); h.ad().onClose(); }
    else h.reject();
    await tick();
    assert(!h.methods().includes('OnAdRewarded'));
    assert.equal(h.methods().filter(x => x === 'OnAdError').length, 1);
    assert.equal(h.canvas.style.pointerEvents, '');
  });
  await check(platform + ': interstitial never pays a reward', async () => {
    const h = await harness(platform); h.messages.length = 0;
    h.api.NubikShowInterstitial();
    if (platform === 'CrazyGames') h.ad().adFinished();
    if (platform === 'YandexGames') { h.ad().onRewarded(); h.ad().onClose(); }
    if (platform === 'Poki') h.resolve(true);
    if (platform === 'GamePix') h.resolve({ success: true });
    if (platform === 'GameDistribution') { h.event('SDK_REWARDED_WATCH_COMPLETE'); h.resolve(); }
    await tick();
    assert(!h.methods().includes('OnAdRewarded'));
    assert(h.methods().includes('OnInterstitialClose'));
  });
}
for (const platform of ['Poki', 'GamePix', 'GameDistribution']) await check(platform + ': skipped video earns nothing', async () => {
  const h = await harness(platform); h.messages.length = 0;
  h.api.NubikShowRewarded(5); h.resolve(platform === 'GamePix' ? { success: false } : false); await tick();
  assert(!h.methods().includes('OnAdRewarded')); assert(h.methods().includes('OnAdClose'));
});
for (const platform of ['GameDistribution', 'GameMonetize']) await check(platform + ': absent ID fails closed, game remains playable', async () => {
  const h = await harness(platform, { missingId: true }); h.messages.length = 0;
  assert.equal(h.page.nubikPortal.status, 'unavailable');
  h.api.NubikShowInterstitial(); assert.deepEqual(h.methods(), ['OnInterstitialError']);
  assert.equal(h.api.NubikPlatformCapabilities() & 1, 0);
});
await check('GameMonetize: event-based interstitial and no rewarded', async () => {
  const h = await harness('GameMonetize'); h.messages.length = 0;
  h.api.NubikShowInterstitial(); h.event('SDK_GAME_PAUSE'); h.event('SDK_GAME_START'); h.event('SDK_GAME_START');
  assert.equal(h.methods().filter(x => x === 'OnInterstitialClose').length, 1);
  h.api.NubikShowRewarded(1); assert(h.methods().includes('OnAdError')); assert(!h.methods().includes('OnAdRewarded'));
});
await check('CrazyGames: audio setting is independent of gameplay pause', async () => {
  const h = await harness('CrazyGames'); h.page.nubikPortal.attach();
  assert(h.messages.some(x => x[0] === 'OnPlatformMute' && x[1] === '1'));
  h.settings(false); assert.deepEqual(h.messages.at(-1), ['OnPlatformMute', '0']);
});
await check('Timeout before ad opens: late completion cannot pay', async () => {
  const h = await harness('CrazyGames'); h.messages.length = 0;
  h.api.NubikShowRewarded(3); h.expire(); h.ad().adFinished();
  assert.deepEqual(h.methods(), ['OnAdError']); assert.equal(h.canvas.style.pointerEvents, '');
});
await check('Timeout never resumes gameplay underneath an opened ad', async () => {
  const h = await harness('CrazyGames'); h.messages.length = 0;
  h.api.NubikShowRewarded(3); h.ad().adStarted(); h.expire();
  assert(!h.methods().includes('OnAdError')); assert.equal(h.canvas.style.pointerEvents, 'none');
  h.ad().adFinished();
});
await check('YandexGames: language comes from the SDK (requirement 2.14)', async () => {
  for (const [lang, expected, page] of [['kk', 2, 'ru'], ['ru', 2, 'ru'], ['tr', 3, 'en'], [undefined, 0, 'en']]) {
    const h = await harness('YandexGames', { lang });
    assert.equal(h.api.NubikPlatformLanguage(), expected, String(lang));
    assert.equal(h.page.nubikPageText('title'), page === 'ru' ? 'Нубик Шахтёр' : 'Nubik Miner');
  }
  const saved = await harness('YandexGames', { lang: 'en', savedLanguage: 'ru' });
  assert.equal(saved.api.NubikPlatformLanguage(), 3);
  assert.equal(saved.page.nubikPageText('title'), 'Нубик Шахтёр', 'A language the player chose keeps the page text.');
});
await check('YandexGames: no SDK means the player is asked', async () => {
  const h = await harness('YandexGames', { initError: true });
  assert.equal(h.page.nubikPortal.status, 'unavailable');
  assert.equal(h.api.NubikPlatformLanguage(), 0);
});
await check('Other portals ask the player for the language', async () => {
  for (const platform of ['CrazyGames', 'Poki', 'GamePix']) assert.equal((await harness(platform)).api.NubikPlatformLanguage(), 0);
});
await check('Poki SDK rejection leaves ads disabled', async () => {
  const h = await harness('Poki', { initError: true }); h.messages.length = 0;
  assert.equal(h.page.nubikPortal.status, 'unavailable'); h.api.NubikShowRewarded(2);
  assert.deepEqual(h.methods(), ['OnAdError']);
});
console.log(`Portal integration: ${passed} scenarios passed (mock SDKs, no real ads requested).`);
