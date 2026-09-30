// Renders chosen frames of a composition to PNGs for review, with one bundle for all of them.
//   node scripts/frames.mjs <composition> <outDir> <frame> [frame…]
import { bundle } from "@remotion/bundler";
import { renderStill, selectComposition } from "@remotion/renderer";
import { enableTailwind } from "@remotion/tailwind-v4";
import { mkdirSync } from "node:fs";
import { join, resolve } from "node:path";

const [id, out, ...frames] = process.argv.slice(2);
mkdirSync(out, { recursive: true });
const serveUrl = await bundle({ entryPoint: resolve("src/index.ts"), webpackOverride: enableTailwind });
const composition = await selectComposition({ serveUrl, id });
for (const frame of frames.map(Number)) {
  await renderStill({ serveUrl, composition, frame, output: join(out, `${id}_${String(frame).padStart(4, "0")}.png`) });
  console.log("frame", frame);
}
