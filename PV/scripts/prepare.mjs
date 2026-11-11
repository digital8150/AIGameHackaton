#!/usr/bin/env node
// Cuts the PV clips out of the raw capture and copies the game assets the PV uses into public/.
// Usage: node scripts/prepare.mjs "<path to capture.mp4>" [--only id1,id2] [--force]
import { spawn } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const here = path.dirname(fileURLToPath(import.meta.url));
const pv = path.resolve(here, "..");
const repo = path.resolve(pv, "..");
const pub = path.join(pv, "public");

const args = process.argv.slice(2);
const capture = args.find((a) => !a.startsWith("--"));
const force = args.includes("--force");
const onlyIdx = args.indexOf("--only");
const only = onlyIdx >= 0 ? new Set(args[onlyIdx + 1].split(",")) : null;
if (!capture || !fs.existsSync(capture)) {
  console.error('usage: node scripts/prepare.mjs "<capture.mp4>" [--only ids] [--force]');
  process.exit(1);
}

const { handle, segments } = JSON.parse(fs.readFileSync(path.join(pv, "src", "segments.json"), "utf8"));
const FPS = 30;

// ---- assets ------------------------------------------------------------------------------
const assets = [
  ["Assets/fonts/온글잎 류류체.ttf", "fonts/ryuryu.ttf"],
  ["Assets/_Project/Fonts/CinzelDecorative-Bold.ttf", "fonts/CinzelDecorative-Bold.ttf"],
  ["Assets/_Project/Fonts/CinzelDecorative-Regular.ttf", "fonts/CinzelDecorative-Regular.ttf"],
  ["Assets/_Project/Fonts/GowunBatang-Regular.ttf", "fonts/GowunBatang-Regular.ttf"],
  ["Assets/_Project/Audio/Music/cutscene_you_are_my_ears.mp3", "music/you_are_my_ears.mp3"],
  ["Assets/_Project/Art/Title/Title_Backdrop.png", "art/Title_Backdrop.png"],
  ["Assets/_Project/Art/Title/Title_Prince.png", "art/Title_Prince.png"],
  ["Assets/_Project/Art/Title/Title_Princess.png", "art/Title_Princess.png"],
  ["Assets/_Project/Art/Story/CaptionBacking.png", "art/CaptionBacking.png"],
];
for (const [from, to] of assets) {
  const dst = path.join(pub, to);
  fs.mkdirSync(path.dirname(dst), { recursive: true });
  fs.copyFileSync(path.join(repo, from), dst);
}
console.log(`copied ${assets.length} assets`);

// generated parchment texture for page backs and title cards
{
  const run0 = (argv) => new Promise((res, rej) => spawn("ffmpeg", argv, { stdio: "ignore" }).on("close", (c) => (c === 0 ? res() : rej(new Error("paper texture failed")))));
  await run0([
    "-v", "error", "-y", "-f", "lavfi", "-i", "color=c=0xcbb386:s=1920x1080",
    "-vf", "noise=alls=22:allf=u,gblur=sigma=1.4,noise=alls=6:allf=u,eq=contrast=1.05,vignette=angle=0.9",
    "-frames:v", "1", path.join(pub, "art", "paper.png"),
  ]);
}

// ---- clips -------------------------------------------------------------------------------
const run = (cmd, argv) =>
  new Promise((res, rej) => {
    const p = spawn(cmd, argv, { stdio: ["ignore", "ignore", "pipe"] });
    let err = "";
    p.stderr.on("data", (d) => (err += d));
    p.on("close", (code) => (code === 0 ? res() : rej(new Error(`${cmd} exited ${code}\n${err.slice(-2000)}`))));
  });

function clipArgs(seg, out) {
  const pieces = seg.pieces.map((p) => ({ ...p }));
  // handles: extend first piece backwards and last piece forwards (in output seconds)
  pieces[0].in -= handle * pieces[0].speed;
  pieces[pieces.length - 1].out += handle * pieces[pieces.length - 1].speed;
  const t0 = Math.max(0, pieces[0].in);
  const t1 = pieces[pieces.length - 1].out;
  const parts = pieces.map((p, i) => {
    const a = (Math.max(p.in, t0) - t0).toFixed(4);
    const b = (p.out - t0).toFixed(4);
    return `[0:v]trim=start=${a}:end=${b},setpts=(PTS-STARTPTS)/${p.speed}[p${i}]`;
  });
  const concat = pieces.map((_, i) => `[p${i}]`).join("") + `concat=n=${pieces.length}:v=1:a=0,fps=${FPS},format=yuv420p[v]`;
  return [
    "-v", "error", "-y",
    "-ss", t0.toFixed(4), "-t", (t1 - t0 + 0.5).toFixed(4), "-i", capture,
    "-filter_complex", parts.join(";") + ";" + concat,
    "-map", "[v]", "-an",
    "-c:v", "libx264", "-preset", "medium", "-crf", "15",
    "-g", "15", "-keyint_min", "15", "-sc_threshold", "0", "-bf", "0",
    "-movflags", "+faststart",
    out,
  ];
}

const clipDir = path.join(pub, "clips");
fs.mkdirSync(clipDir, { recursive: true });
const todo = segments.filter((s) => !only || only.has(s.id));
let next = 0;
async function worker() {
  while (next < todo.length) {
    const seg = todo[next++];
    const out = path.join(clipDir, `${seg.id}.mp4`);
    if (fs.existsSync(out) && !force) {
      console.log(`skip ${seg.id} (exists)`);
      continue;
    }
    const t = Date.now();
    await run("ffmpeg", clipArgs(seg, out));
    console.log(`cut ${seg.id} in ${((Date.now() - t) / 1000).toFixed(1)}s`);
  }
}
await Promise.all([worker(), worker(), worker()]);
console.log("done");
