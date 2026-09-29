// Contract checks for the WebGL interstitial bridge without a live ad network.
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
import assert from 'node:assert/strict';

const api = {};
const page = {};
const sent = [];
const context = vm.createContext({
  window: page, LibraryManager: { library: api },
  mergeInto: Object.assign, setTimeout: fn => fn(),
});
vm.runInContext(readFileSync(new URL('../Assets/Plugins/WebGL/Yandex.jslib', import.meta.url), 'utf8'), context);
function reset() {
  sent.length = 0;
  delete page.nubikSDK; delete page.nubikFakeAd;
  page.unityInstance = { SendMessage: (object, method, value) => sent.push([object, method, value]) };
}
const methods = () => sent.map(x => x[1]);
let callbacks;
reset();
page.nubikSDK = { adv: { showFullscreenAdv: options => { callbacks = options.callbacks; } } };
api.NubikShowInterstitial();
callbacks.onOpen(); callbacks.onClose(true); callbacks.onClose(true);
assert.deepEqual(methods(), ['OnInterstitialOpen', 'OnInterstitialClose']);

reset();
page.nubikSDK = { adv: { showFullscreenAdv: options => { callbacks = options.callbacks; } } };
api.NubikShowInterstitial(); callbacks.onError(new Error('offline')); callbacks.onClose(false);
assert.deepEqual(methods(), ['OnInterstitialError']);

reset();
page.nubikSDK = { adv: { showFullscreenAdv: () => { throw new Error('SDK unavailable'); } } };
api.NubikShowInterstitial();
assert.deepEqual(methods(), ['OnInterstitialError']);

reset(); api.NubikShowInterstitial();
assert.deepEqual(methods(), ['OnInterstitialError']);

reset();
page.nubikFakeAd = (ticket, send) => {
  assert.equal(ticket, null);
  send('OnAdOpen', ''); send('OnAdRewarded', '1'); send('OnAdClose', '');
};
api.NubikShowInterstitial();
assert.deepEqual(methods(), ['OnInterstitialOpen', 'OnInterstitialClose']);

reset();
page.nubikSDK = { adv: { showRewardedVideo: options => { callbacks = options.callbacks; } } };
api.NubikShowRewarded(7); callbacks.onOpen(); callbacks.onRewarded(); callbacks.onClose();
assert.deepEqual(methods(), ['OnAdOpen', 'OnAdRewarded', 'OnAdClose']);
assert.equal(sent[1][2], '7');
console.log('Ad bridge: 6 callback scenarios passed.');
