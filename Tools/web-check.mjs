// Drives the local WebGL build in headless Edge/Chrome over the DevTools protocol and saves
// screenshots plus console errors, for checks when no visible browser is available.
//
//   py -3 -m http.server 8080 --directory Builds/WebGL      (in another terminal)
//   node Tools/web-check.mjs [url] [outDir]
//
// Steps: desktop start screen, start, dig, the house at the ore buyer, zone announcement at 35 m,
// pause menu; then a portrait phone layout with touch controls.
import { spawn } from 'node:child_process';
import { existsSync, mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

const url = process.argv[2] ?? 'http://localhost:8080/';
const out = process.argv[3] ?? 'TestResults/Web';
const port = 9333;
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
async function load(width, height, mobile, address = url) {
  await send('Emulation.setDeviceMetricsOverride', { width, height, deviceScaleFactor: 1, mobile });
  await send('Emulation.setTouchEmulationEnabled', { enabled: mobile, maxTouchPoints: mobile ? 5 : 0 });
  await send('Emulation.setUserAgentOverride', {
    userAgent: mobile ? 'Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/128.0 Mobile Safari/537.36' : '',
  });
  await send('Page.navigate', { url: address });
  for (let i = 0; i < 240 && !(await evaluate('!!window.unityInstance')); i++) await sleep(500);
  await sleep(3000);
}

try {
  await send('Page.enable');
  await send('Runtime.enable');
  // Fresh progress for a repeatable first session.
  await send('Page.navigate', { url });
  await sleep(1500);
  await evaluate(`(async () => { for (const db of await indexedDB.databases()) if (db.name?.includes('idbfs')) await new Promise(r => { const q = indexedDB.deleteDatabase(db.name); q.onsuccess = q.onerror = q.onblocked = r; }); })()`);

  const withParam = extra => url + (url.includes('?') ? '&' : '?') + extra;
  await load(1280, 720, false);
  await shot('desktop_01_start');
  await click(640, 391);
  await sleep(800);
  await shot('desktop_02_playing');
  await mouse('mouseMoved', 640, 360);
  await mouse('mousePressed', 640, 360);
  await sleep(4000);
  await mouse('mouseReleased', 640, 360);
  await sleep(300);
  await shot('desktop_03_dug');

  // Leaving the page must save quietly.
  step = 'leaving the page';
  await send('Page.navigate', { url: 'about:blank' });
  await sleep(1500);
  // Walk-in house: stand at the ore buyer (localhost test helper) and open it with E.
  step = 'house';
  await load(1280, 720, false, withParam('at=counter'));
  await sleep(2500);
  await click(640, 391);
  await sleep(700);
  await shot('desktop_04_at_counter');
  await key('KeyE', 'e');
  await sleep(800);
  await shot('desktop_05_house');
  await key('Escape', 'Escape');
  await sleep(400);

  // Drop to the stone zone, start, catch the zone announcement, then open the pause menu.
  step = 'loading at depth';
  await load(1280, 720, false, withParam('depth=35'));
  await shot('desktop_06_welcome_back');
  await click(640, 391);
  await sleep(700);
  await shot('desktop_07_zone');
  await key('Escape', 'Escape');
  await sleep(500);
  await shot('desktop_08_pause');

  await load(390, 844, true);
  await shot('phone_01_start');
  const start = await evaluate(`(() => { const c = document.getElementById('unity-canvas').getBoundingClientRect(); return [c.width, c.height]; })()`);
  await send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x: start[0] / 2, y: start[1] * 0.56 }] });
  await sleep(100);
  await send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  await sleep(1000);
  await shot('phone_02_playing');
} finally {
  writeFileSync(join(out, 'console.txt'), problems.join('\n'));
  console.log(problems.length ? problems.join('\n') : 'no console errors');
  socket.close();
  browser.kill();
  await sleep(500);
  try { rmSync(profile, { recursive: true, force: true }); } catch { /* profile still locked */ }
}
