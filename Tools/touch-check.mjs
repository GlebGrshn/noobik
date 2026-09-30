// Mobile gesture smoke test; run node Tools/touch-check.mjs [url] [output directory].
// Uses an isolated browser profile. Inspect the saved screenshots; Safari insets are simulated.
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const url = process.argv[2] ?? 'http://localhost:8080/';
const out = process.argv[3] ?? 'TestResults/Touch';
const port = 9335;
const browserPath = [
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Microsoft/Edge/Application/msedge.exe',
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
].find(existsSync);
if (!browserPath) throw new Error('Edge or Chrome not found');
mkdirSync(out, { recursive: true });
const profile = mkdtempSync(join(tmpdir(), 'nubik-web-'));
const browser = spawn(browserPath, [
  '--headless=new', `--remote-debugging-port=${port}`, '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
  '--no-first-run', '--mute-audio', `--user-data-dir=${profile}`, '--window-size=1280,720', 'about:blank',
], { stdio: 'ignore' });

const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const problems = [];
let step = 'loading';

async function connect() {
  for (let i = 0; i < 50; i++) {
    try {
      const pages = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
      const page = pages.find(target => target.type === 'page');
      if (page) return page.webSocketDebuggerUrl;
    } catch { /* browser still starting */ }
    await sleep(200);
  }
  throw new Error('DevTools endpoint did not start');
}

const socket = new WebSocket(await connect());
await new Promise(resolve => socket.addEventListener('open', resolve, { once: true }));
let nextId = 0;
const pending = new Map();
socket.addEventListener('message', event => {
  const message = JSON.parse(event.data);
  if (message.id && pending.has(message.id)) {
    pending.get(message.id)(message);
    pending.delete(message.id);
  } else if (message.method === 'Runtime.exceptionThrown') {
    problems.push(`[${step}] exception: ` + message.params.exceptionDetails.text + ' ' + (message.params.exceptionDetails.exception?.description ?? ''));
  } else if (message.method === 'Runtime.consoleAPICalled' && ['error', 'warning'].includes(message.params.type)) {
    problems.push(`[${step}] ${message.params.type}: ` + message.params.args.map(arg => arg.value ?? arg.description).join(' '));
  }
});
const send = (method, params = {}) => new Promise(resolve => {
  const id = ++nextId;
  pending.set(id, resolve);
  socket.send(JSON.stringify({ id, method, params }));
});
const evaluate = async expression => (await send('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true })).result?.result?.value;

async function shot(name) {
  step = 'after ' + name;
  const { result } = await send('Page.captureScreenshot', { format: 'png' });
  writeFileSync(join(out, name + '.png'), Buffer.from(result.data, 'base64'));
  console.log('saved', name);
}
const mouse = (type, x, y, button = 'left') => send('Input.dispatchMouseEvent', { type, x, y, button, buttons: type === 'mouseReleased' ? 0 : button === 'left' ? 1 : 2, clickCount: 1 });
async function click(x, y) { await mouse('mouseMoved', x, y); await sleep(60); await mouse('mousePressed', x, y); await sleep(120); await mouse('mouseReleased', x, y); }
async function key(code, text) {
  const keyCode = text === 'Escape' ? 27 : text.toUpperCase().charCodeAt(0);
  await send('Input.dispatchKeyEvent', { type: 'keyDown', code, key: text, windowsVirtualKeyCode: keyCode });
  await sleep(80);
  await send('Input.dispatchKeyEvent', { type: 'keyUp', code, key: text, windowsVirtualKeyCode: keyCode });
}
let firstLanguageChoice = true;
async function load(width, height, mobile, address = url) {
  await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile });
  await send('Emulation.setTouchEmulationEnabled', { enabled: mobile, maxTouchPoints: mobile ? 5 : 0 });
  await send('Emulation.setUserAgentOverride', {
    userAgent: mobile ? 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Mobile Safari/537.36' : '',
  });
  await send('Page.navigate', { url: address });
  for (let i = 0; i < 240 && !(await evaluate('!!window.unityInstance && window.nubikReady === true')); i++) await sleep(500);
  await sleep(3000);
  // This test uses a fresh browser profile: choose Russian once for the existing reference flows.
  if (firstLanguageChoice && width > height) {
    const scale = Math.min(width / 1280, height / 720);
    await click(width / 2 - 110 * scale, height / 2 + 43 * scale);
    await sleep(300);
    firstLanguageChoice = false;
  }
}

const touch = (type, points) => send('Input.dispatchTouchEvent', {type, touchPoints:points.map(([id,x,y])=>({id,x,y,radiusX:8,radiusY:8}))});
try {
  await send('Page.enable');
  await send('Runtime.enable');
  await load(844, 390, true);
  if (!(await evaluate('!!window.unityInstance && window.nubikReady === true'))) throw new Error('Unity failed to load');
  await shot('01_landscape');
  // Hold past the round digging target, continuing to turn the camera with the same finger.
  await touch('touchStart', [[1,760,311]]);
  await sleep(900);
  for(let n=1;n<=12;n++) { await touch('touchMove', [[1,760-n*12,311-n*2]]); await sleep(70); }
  await sleep(600);
  await shot('02_dig_drag_outside');
  await touch('touchEnd', []);
  // The left digging button holds while a second finger turns on open space.
  await touch('touchStart', [[2,108,207]]);
  await touch('touchStart', [[2,108,207],[3,570,180]]);
  for(let n=1;n<=10;n++) { await touch('touchMove', [[2,108,207],[3,570+n*10,180+n*2]]); await sleep(70); }
  await shot('03_left_dig_right_look');
  await touch('touchEnd', [[2,108,207]]);
  await sleep(400);
  await touch('touchEnd', []);
  // Begin walking well away from the resting joystick; a fresh stick appears under that thumb.
  await touch('touchStart', [[4,240,314]]);
  await touch('touchMove', [[4,290,280]]);
  await sleep(500);
  await shot('04_floating_stick');
  await touch('touchEnd', []);
  await sleep(300);
  await shot('05_released_stick');
  // Emulate CSS insets supplied by Safari. The WebGL bridge must move the HUD inside them.
  await evaluate(`document.getElementById('safe-area').style.padding='0px 47px 21px 47px'; window.dispatchEvent(new Event('resize'));`);
  await sleep(500);
  const insets = await evaluate('window.nubikSafeInsets');
  if(Math.abs(insets[0] - 47/844) > .001 || Math.abs(insets[1] - 21/390) > .001) throw new Error('Safe-area bridge failed');
  await shot('06_iphone_safe_area');
  await evaluate(`document.getElementById('safe-area').style.padding='0px';`);
  // Turned upright the game is covered by the "turn the phone" prompt and paused.
  await send('Emulation.setDeviceMetricsOverride', {width:390,height:844,deviceScaleFactor:1,mobile:true});
  await sleep(1200);
  await shot('07_portrait');
  await load(1280,720,false);
  await click(640,391);
  await sleep(500);
  await shot('08_desktop');
  if(problems.some(p => p.includes('exception:') || p.includes('] error:'))) throw new Error('Browser runtime errors');
  console.log('TOUCH_SMOKE_PASSED');
} finally {
  writeFileSync(join(out, 'console.txt'), problems.join('\n'));
  console.log(problems.length ? problems.join('\n') : 'no console errors');
  socket.close();
  browser.kill();
  await sleep(500);
  // Only remove the temporary profile created by this run.
  if (profile.startsWith(join(tmpdir(), 'nubik-web-'))) {
    try { rmSync(profile, { recursive: true, force: true }); } catch { /* browser may still hold a file */ }
  }
}
