// Render stills for review with one bundle: node scripts/stills.mjs <outDir> <frame|a-b|a-b:step> ...
import { bundle } from "@remotion/bundler";
import { renderStill, selectComposition, openBrowser } from "@remotion/renderer";
import path from "node:path";
import fs from "node:fs";

const [outDir, ...specs] = process.argv.slice(2);
const frames = [];
for (const s of specs) {
  const m = s.match(/^(\d+)(?:-(\d+)(?::(\d+))?)?$/);
  if (!m) throw new Error("bad frame spec " + s);
  const a = +m[1], b = m[2] ? +m[2] : a, st = m[3] ? +m[3] : 1;
  for (let f = a; f <= b; f += st) frames.push(f);
}
fs.mkdirSync(outDir, { recursive: true });
const serveUrl = await bundle({ entryPoint: path.resolve("src/index.ts") });
const browser = await openBrowser("chrome", { chromiumOptions: { gl: "angle" } });
const composition = await selectComposition({ serveUrl, id: "DuskPV", puppeteerInstance: browser });
for (const frame of frames) {
  const t = Date.now();
  await renderStill({ serveUrl, composition, frame, output: path.join(outDir, `f${String(frame).padStart(4, "0")}.jpg`), imageFormat: "jpeg", jpegQuality: 85, puppeteerInstance: browser, timeoutInMilliseconds: 180000, chromiumOptions: { gl: "angle" } });
  console.log(frame, Date.now() - t, "ms");
}
await browser.close({ silent: true });
