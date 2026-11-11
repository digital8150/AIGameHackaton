import React from "react";
import { Img, staticFile, useCurrentFrame, interpolate } from "remotion";
import { PALETTE, clamp01, easeOut, easeIn, rand } from "../lib";

export const HAND = "Ryuryu, 'Gowun Batang', serif";
export const LOGO = "'Cinzel Decorative', serif";

/**
 * Kinetic 3D type: each glyph arrives from in front of the lens (blurred, tilted),
 * settles sharp on its plane (rack focus) while an ink mask writes it left to right,
 * then drifts back out of focus on exit.
 */
export const Kinetic: React.FC<{
  text: string;
  from: number; // local frame the text starts arriving
  to: number; // local frame it is fully gone
  size: number;
  color?: string;
  font?: string;
  stagger?: number;
  glow?: number; // bloom strength 0..1
  tilt?: number;
  letterSpacing?: number;
  arrive?: number; // frames per glyph arrival
  depth?: number; // px the glyphs travel in z
  exitStyle?: "focus" | "rise";
  style?: React.CSSProperties;
  seed?: number;
}> = ({ text, from, to, size, color = PALETTE.ink, font = HAND, stagger = 1.6, glow = 0, tilt = 0, letterSpacing = 0, arrive = 14, depth = 520, exitStyle = "focus", style, seed = 3 }) => {
  const f = useCurrentFrame();
  if (f < from - 1 || f > to + 1) return null;
  const chars = Array.from(text);
  const exitDur = 12;
  const exitK = clamp01((f - (to - exitDur)) / exitDur);
  const exitE = easeIn(exitK);
  const bloom =
    glow > 0
      ? `0 0 ${6 * glow}px rgba(255,236,170,${0.9 * glow}), 0 0 ${22 * glow}px rgba(233,196,106,${0.75 * glow}), 0 0 ${60 * glow}px rgba(214,150,60,${0.55 * glow}), 0 0 ${120 * glow}px rgba(160,100,40,${0.35 * glow})`
      : "0 2px 12px rgba(0,0,0,0.85), 0 0 2px rgba(0,0,0,0.9)";
  return (
    <div
      style={{
        display: "flex",
        justifyContent: "center",
        alignItems: "baseline",
        transformStyle: "preserve-3d",
        transform: `rotateZ(${tilt}deg) translateZ(${exitStyle === "focus" ? -exitE * 260 : 0}px) translateY(${exitStyle === "rise" ? -exitE * 40 : 0}px)`,
        filter: exitK > 0 ? `blur(${(exitE * 14).toFixed(2)}px)` : undefined,
        opacity: 1 - exitE,
        ...style,
      }}
    >
      {chars.map((c, i) => {
        const t0 = from + i * stagger;
        const k = clamp01((f - t0) / arrive);
        const e = easeOut(k);
        const z = (1 - e) * depth;
        const rx = (1 - e) * (-38 + 20 * rand(seed, i, 1));
        const ry = (1 - e) * (30 * (rand(seed, i, 2) - 0.5));
        const y = (1 - e) * (30 + 30 * rand(seed, i, 3));
        const blur = (1 - e) * 16;
        // ink write-on: glyph opacity grows a little after it lands
        const ink = clamp01((f - t0) / (arrive * 0.6));
        return (
          <span
            key={i}
            style={{
              display: "inline-block",
              fontFamily: font,
              fontSize: size,
              color,
              whiteSpace: "pre",
              letterSpacing,
              lineHeight: 1.1,
              transform: `translate3d(0, ${y}px, ${z}px) rotateX(${rx}deg) rotateY(${ry}deg)`,
              filter: blur > 0.2 ? `blur(${blur.toFixed(2)}px)` : undefined,
              opacity: ink,
              textShadow: bloom,
            }}
          >
            {c}
          </span>
        );
      })}
    </div>
  );
};

/** Body caption in the lower third: warm ivory hand lettering over a dark ink smudge. */
export const Caption: React.FC<{ text: string; from: number; to: number; small?: boolean; y?: number; seed?: number }> = ({ text, from, to, small, y = 860, seed = 5 }) => {
  const f = useCurrentFrame();
  if (f < from - 1 || f > to + 1) return null;
  const size = small ? 54 : 74;
  const back = interpolate(f, [from, from + 10, to - 10, to], [0, 1, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const w = Math.max(700, Array.from(text).length * size * 0.95 + 260);
  return (
    <div style={{ position: "absolute", left: 0, right: 0, top: y - size * 0.75, height: size * 1.6, perspective: 1100, perspectiveOrigin: "50% 50%" }}>
      <Img
        src={staticFile("art/CaptionBacking.png")}
        style={{ position: "absolute", left: (1920 - w) / 2, top: -size * 0.55, width: w, height: size * 2.7, opacity: 0.95 * back, filter: "brightness(0.55)" }}
      />
      <div style={{ position: "absolute", left: (1920 - w * 0.9) / 2, top: -size * 0.3, width: w * 0.9, height: size * 2.2, borderRadius: "50%", background: "radial-gradient(ellipse at center, rgba(10,6,3,0.78) 0%, rgba(10,6,3,0.5) 45%, rgba(10,6,3,0) 72%)", opacity: back }} />
      <div style={{ position: "absolute", inset: 0, transformStyle: "preserve-3d" }}>
        <Kinetic text={text} from={from} to={to} size={size} stagger={small ? 1.2 : 1.8} seed={seed} depth={small ? 300 : 560} />
      </div>
    </div>
  );
};

/** The spoken word: big, gold, tilted, blooming — pops on the frame the object reacts. */
export const WordPop: React.FC<{ text: string; at: number; to: number; x?: number; y?: number; size?: number; tilt?: number }> = ({ text, at, to, x = 960, y = 800, size = 150, tilt = -5 }) => {
  const f = useCurrentFrame();
  if (f < at - 4 || f > to + 1) return null;
  const k = clamp01((f - (at - 4)) / 10);
  // overshoot pop
  const s = 1 + 0.9 * Math.pow(1 - k, 3) - 0.12 * Math.sin(k * Math.PI) * (1 - k);
  const flash = clamp01(1 - (f - at) / 14);
  const glow = 0.75 + 0.6 * flash;
  const exitK = clamp01((f - (to - 10)) / 10);
  return (
    <div style={{ position: "absolute", left: x - 900, top: y - size * 0.7, width: 1800, height: size * 1.5, perspective: 900, perspectiveOrigin: "50% 50%" }}>
      <div
        style={{
          position: "absolute",
          inset: 0,
          transformStyle: "preserve-3d",
          transform: `scale(${s}) rotateX(${(1 - k) * -25}deg) translateZ(${exitK * 200}px)`,
          opacity: clamp01(k * 2.5) * (1 - exitK),
          filter: exitK > 0 ? `blur(${exitK * 18}px)` : undefined,
        }}
      >
        <Kinetic text={text} from={at - 4} to={to + 20} size={size} color="#F2D27E" glow={glow} tilt={tilt} stagger={0.7} arrive={7} depth={260} seed={11} />
      </div>
    </div>
  );
};
