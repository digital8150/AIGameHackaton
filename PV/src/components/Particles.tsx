import React, { useLayoutEffect, useRef } from "react";
import { useCurrentFrame } from "remotion";
import { H, W, rand, clamp01 } from "../lib";

// Everything here is a pure function of the frame number -> stable, flicker-free renders.

export type Burst = { f: number; x: number; y: number; n?: number; speed?: number; seed?: number; color?: string; life?: number; spread?: number; gravity?: number };
export type Ring = { f: number; x: number; y: number; r?: number; seed?: number; color?: string; life?: number; n?: number };
export type Scraps = { f: number; x: number; y: number; n?: number; seed?: number; life?: number; dir?: number };
export type AmbientMode = { from: number; to: number; warm: number; cold: number; density?: number };

export type ParticleProps = {
  seed?: number;
  /** ambient motes: count of particles over the whole field */
  count?: number;
  /** colour mix per frame (0..1 weights) */
  mode?: (f: number) => { warm: number; cold: number; density: number };
  /** depth that is in focus (0 near .. 1 far) */
  focus?: (f: number) => number;
  /** horizontal camera drift in px, applied with parallax */
  pan?: (f: number) => number;
  bursts?: Burst[];
  rings?: Ring[];
  scraps?: Scraps[];
  /** only draw particles nearer than this depth (front layer) or farther (back layer) */
  depthRange?: [number, number];
  opacity?: number;
};

const WARM = [240, 178, 92];
const GOLD = [233, 196, 106];
const COLD = [170, 188, 206];
const mix = (a: number[], b: number[], t: number) => a.map((v, i) => v + (b[i] - v) * t);
const rgba = (c: number[], a: number) => `rgba(${c[0] | 0},${c[1] | 0},${c[2] | 0},${a})`;
const parse = (hex?: string) => {
  if (!hex) return GOLD;
  const n = parseInt(hex.replace("#", ""), 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
};

function bokeh(ctx: CanvasRenderingContext2D, x: number, y: number, r: number, defocus: number, c: number[], a: number) {
  const R = r + defocus;
  if (R < 0.4 || a < 0.004) return;
  const energy = Math.max(0.06, (r * r) / (R * R));
  const alpha = a * energy;
  const g = ctx.createRadialGradient(x, y, 0, x, y, R);
  const core = defocus < 1.5 ? 0.35 : 0.0;
  g.addColorStop(0, rgba(c, alpha));
  g.addColorStop(Math.max(0.05, core), rgba(c, alpha * (defocus < 1.5 ? 1 : 0.9)));
  g.addColorStop(defocus > 4 ? 0.82 : 0.7, rgba(c, alpha * (defocus > 4 ? 0.75 : 0.35)));
  g.addColorStop(1, rgba(c, 0));
  ctx.fillStyle = g;
  ctx.beginPath();
  ctx.arc(x, y, R, 0, Math.PI * 2);
  ctx.fill();
}

export const drawParticles = (ctx: CanvasRenderingContext2D, f: number, p: ParticleProps) => {
  const seed = p.seed ?? 1;
  const [dMin, dMax] = p.depthRange ?? [0, 1];
  const focus = p.focus ? p.focus(f) : 0.55;
  const mode = p.mode ? p.mode(f) : { warm: 1, cold: 0, density: 1 };
  const pan = p.pan ? p.pan(f) : 0;
  const op = p.opacity ?? 1;
  ctx.clearRect(0, 0, W, H);
  ctx.globalCompositeOperation = "lighter";

  // ---- ambient embers / dust motes drifting in depth ----
  const count = p.count ?? 140;
  const nVisible = Math.round(count * clamp01(mode.density));
  for (let i = 0; i < nVisible; i++) {
    const z = rand(seed, i, 1); // 0 near .. 1 far
    if (z < dMin || z > dMax) continue;
    const near = 1 - z;
    const speed = 10 + 38 * near + 14 * rand(seed, i, 2); // px/s upward
    const t = f / 30;
    const span = H + 240;
    const y0 = rand(seed, i, 3) * span;
    const y = ((((y0 - speed * t) % span) + span) % span) - 120;
    const sway = Math.sin(t * (0.4 + rand(seed, i, 4)) + rand(seed, i, 5) * 6.28) * (14 + 30 * near);
    const xPar = pan * (0.25 + near * 1.1);
    const x = ((rand(seed, i, 6) * (W + 300) + sway - xPar) % (W + 300) + (W + 300)) % (W + 300) - 150;
    const r = 1.1 + 4.2 * near * near + 1.2 * rand(seed, i, 7);
    const defocus = Math.abs(z - focus) * (z < focus ? 46 : 16);
    const flicker = 0.65 + 0.35 * Math.sin(t * (2 + 3 * rand(seed, i, 8)) + i);
    const isGold = rand(seed, i, 9) > 0.55;
    const warmC = isGold ? GOLD : WARM;
    const c = mix(warmC, COLD, clamp01(mode.cold / Math.max(0.001, mode.cold + mode.warm)));
    const a = (0.22 + 0.5 * near) * flicker * op * clamp01(mode.warm + mode.cold);
    bokeh(ctx, x, y, r, defocus, c, a);
  }

  // ---- bursts: golden sparks thrown out from a point (spoken word lands) ----
  for (const b of p.bursts ?? []) {
    const life = b.life ?? 48;
    const age = f - b.f;
    if (age < 0 || age > life) continue;
    const n = b.n ?? 90;
    const c = parse(b.color);
    const bs = b.seed ?? b.f;
    for (let i = 0; i < n; i++) {
      const z = rand(bs, i, 11);
      if (z < dMin || z > dMax) continue;
      const ang = rand(bs, i, 12) * Math.PI * 2;
      const spd = (b.speed ?? 900) * (0.25 + 0.75 * Math.pow(rand(bs, i, 13), 0.6)) * (b.spread ?? 1);
      const t = age / 30;
      const drag = (1 - Math.exp(-3.2 * t)) / 3.2;
      const x = b.x + Math.cos(ang) * spd * drag;
      const y = b.y + Math.sin(ang) * spd * drag * 0.75 + (b.gravity ?? 160) * t * t - 40 * t;
      const lifeK = 1 - age / life;
      const r = (1.2 + 3.8 * (1 - z)) * (0.6 + 0.4 * lifeK);
      const defocus = Math.abs(z - focus) * 22;
      bokeh(ctx, x, y, r, defocus, c, op * Math.pow(lifeK, 1.3) * (0.55 + 0.45 * rand(bs, i, 14)));
      // short streak for the fastest sparks while young
      if (age < 10 && rand(bs, i, 15) > 0.6) {
        ctx.strokeStyle = rgba(c, op * 0.35 * lifeK);
        ctx.lineWidth = 1.4;
        ctx.beginPath();
        ctx.moveTo(x, y);
        ctx.lineTo(x - Math.cos(ang) * 26 * lifeK, y - Math.sin(ang) * 20 * lifeK);
        ctx.stroke();
      }
    }
  }

  // ---- echo shockwaves: expanding particle rings in the prince's cold blue-grey ----
  for (const rg of p.rings ?? []) {
    const life = rg.life ?? 40;
    const age = f - rg.f;
    if (age < 0 || age > life || dMin > 0.5) continue;
    const k = age / life;
    const ease = 1 - Math.pow(1 - k, 3);
    const R = (rg.r ?? 620) * ease;
    const c = parse(rg.color ?? "#AFC3D6");
    const fade = Math.pow(1 - k, 1.6);
    for (let w = 0; w < 3; w++) {
      const rr = R * (1 - w * 0.12);
      ctx.strokeStyle = rgba(c, op * fade * (w === 0 ? 0.5 : 0.18));
      ctx.lineWidth = w === 0 ? 2.5 : 7 + w * 6;
      ctx.beginPath();
      ctx.arc(rg.x, rg.y, Math.max(1, rr), 0, Math.PI * 2);
      ctx.stroke();
    }
    const n = rg.n ?? 70;
    const s = rg.seed ?? rg.f;
    for (let i = 0; i < n; i++) {
      const ang = (i / n) * Math.PI * 2 + rand(s, i, 21) * 0.2;
      const jitter = 1 + (rand(s, i, 22) - 0.5) * 0.18 * k;
      const x = rg.x + Math.cos(ang) * R * jitter;
      const y = rg.y + Math.sin(ang) * R * jitter;
      bokeh(ctx, x, y, 1.6 + 2.2 * rand(s, i, 23), 2 + 10 * k, c, op * fade * 0.8);
    }
  }

  // ---- paper scraps & ink flecks off a turning page ----
  ctx.globalCompositeOperation = "source-over";
  for (const sc of p.scraps ?? []) {
    const life = sc.life ?? 34;
    const age = f - sc.f;
    if (age < 0 || age > life) continue;
    const n = sc.n ?? 40;
    const s = sc.seed ?? sc.f;
    const t = age / 30;
    for (let i = 0; i < n; i++) {
      const z = rand(s, i, 31);
      if (z < dMin || z > dMax) continue;
      const dir = sc.dir ?? -1;
      const vx = dir * (300 + 900 * rand(s, i, 32)) * (1.2 - z);
      const vy = -200 + 500 * rand(s, i, 33);
      const x = sc.x + (rand(s, i, 34) - 0.5) * 120 + vx * t;
      const y = sc.y + (rand(s, i, 35) - 0.5) * H * 0.9 + vy * t + 520 * t * t;
      const ink = rand(s, i, 36) > 0.7;
      const size = ink ? 2 + 4 * rand(s, i, 37) : 6 + 18 * rand(s, i, 37) * (1.3 - z);
      const rot = rand(s, i, 38) * 6.28 + t * (4 + 8 * rand(s, i, 39));
      const a = (1 - age / life) * op;
      ctx.save();
      ctx.translate(x, y);
      ctx.rotate(rot);
      ctx.scale(1, Math.abs(Math.cos(rot * 1.7)) * 0.8 + 0.2);
      ctx.fillStyle = ink ? `rgba(30,20,12,${0.7 * a})` : `rgba(222,204,164,${0.85 * a})`;
      if (ink) {
        ctx.beginPath();
        ctx.arc(0, 0, size, 0, Math.PI * 2);
        ctx.fill();
      } else {
        ctx.beginPath();
        ctx.moveTo(-size / 2, -size / 3);
        ctx.lineTo(size / 2, -size / 2.5);
        ctx.lineTo(size / 2.3, size / 3);
        ctx.lineTo(-size / 2.2, size / 2.6);
        ctx.closePath();
        ctx.fill();
      }
      ctx.restore();
    }
  }
  ctx.globalCompositeOperation = "source-over";
};

export const Particles: React.FC<ParticleProps & { style?: React.CSSProperties; frameOffset?: number }> = ({ style, frameOffset = 0, ...p }) => {
  const frame = useCurrentFrame() + frameOffset;
  const ref = useRef<HTMLCanvasElement>(null);
  useLayoutEffect(() => {
    const ctx = ref.current?.getContext("2d");
    if (ctx) drawParticles(ctx, frame, p);
  });
  return <canvas ref={ref} width={W} height={H} style={{ position: "absolute", inset: 0, width: W, height: H, pointerEvents: "none", ...style }} />;
};
