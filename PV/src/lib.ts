import { Easing, interpolate } from "remotion";

export const FPS = 30;
export const W = 1920;
export const H = 1080;
export const sec = (s: number) => Math.round(s * FPS);

// ---- deterministic randomness: pure function of (seed, index) -----------------------------
export const hash = (n: number) => {
  let x = Math.imul((n | 0) ^ 0x9e3779b9, 0x85ebca6b);
  x ^= x >>> 13;
  x = Math.imul(x, 0xc2b2ae35);
  x ^= x >>> 16;
  return (x >>> 0) / 4294967296;
};
export const rand = (seed: number, i: number, k = 0) => hash(seed * 7919 + i * 104729 + k * 1299709);

// ---- keyframes --------------------------------------------------------------------------
export type Key = { t: number; [prop: string]: number };
export const easeInOut = Easing.bezier(0.65, 0, 0.35, 1);
export const easeOut = Easing.bezier(0.16, 1, 0.3, 1);
export const easeIn = Easing.bezier(0.7, 0, 0.84, 0);

/** Interpolate a property across keyframes (t in frames, local to the shot). */
export const kf = (keys: Key[], prop: string, t: number, fallback = 0, ease = easeInOut): number => {
  const ks = keys.filter((k) => k[prop] !== undefined);
  if (ks.length === 0) return fallback;
  if (t <= ks[0].t) return ks[0][prop];
  for (let i = 0; i < ks.length - 1; i++) {
    const a = ks[i];
    const b = ks[i + 1];
    if (t <= b.t) return interpolate(t, [a.t, b.t], [a[prop], b[prop]], { easing: ease });
  }
  return ks[ks.length - 1][prop];
};

export const clamp01 = (v: number) => Math.max(0, Math.min(1, v));
export const prog = (f: number, a: number, b: number, ease: (x: number) => number = (x) => x) =>
  ease(clamp01((f - a) / (b - a)));

export const PALETTE = {
  ink: "#EADBB8",
  gold: "#E9C46A",
  goldDeep: "#C9973E",
  ember: "#F0B060",
  cold: "#A9B8C8",
  void: "#0b0806",
  paper: "#D9C69C",
};
