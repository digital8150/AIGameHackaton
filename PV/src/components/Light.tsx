import React from "react";
import { AbsoluteFill, useCurrentFrame } from "remotion";

/** Warm light leak drifting across the frame (screen blend). intensity(f) in 0..1 */
export const LightLeak: React.FC<{ intensity: (f: number) => number; seed?: number; cold?: (f: number) => number }> = ({ intensity, seed = 0, cold }) => {
  const f = useCurrentFrame();
  const a = intensity(f);
  if (a <= 0.005) return null;
  const c = cold ? cold(f) : 0;
  const t = f / 30 + seed * 10;
  const x1 = 70 + 25 * Math.sin(t * 0.37);
  const y1 = 20 + 15 * Math.cos(t * 0.29);
  const x2 = 15 + 20 * Math.cos(t * 0.23 + 1);
  const y2 = 85 + 10 * Math.sin(t * 0.31);
  const warm = `rgba(255,170,80,${0.55 * a * (1 - c)})`;
  const amber = `rgba(255,214,140,${0.35 * a * (1 - c)})`;
  const blue = `rgba(150,180,215,${0.35 * a * c})`;
  return (
    <AbsoluteFill
      style={{
        mixBlendMode: "screen",
        pointerEvents: "none",
        background: `radial-gradient(ellipse 55% 45% at ${x1}% ${y1}%, ${warm} 0%, rgba(0,0,0,0) 70%), radial-gradient(ellipse 40% 60% at ${x2}% ${y2}%, ${amber} 0%, rgba(0,0,0,0) 70%), radial-gradient(ellipse 70% 50% at 50% 40%, ${blue} 0%, rgba(0,0,0,0) 70%)`,
      }}
    />
  );
};

/** Lens vignette + subtle grain-free film tone over everything. */
export const Lens: React.FC = () => (
  <AbsoluteFill
    style={{
      pointerEvents: "none",
      background: "radial-gradient(ellipse 75% 70% at 50% 50%, rgba(0,0,0,0) 60%, rgba(8,4,2,0.55) 100%)",
    }}
  />
);

/** A quick white-gold flash, peak at frame `at`. */
export const Flash: React.FC<{ at: number; dur?: number; strength?: number; color?: string }> = ({ at, dur = 10, strength = 0.6, color = "255,226,170" }) => {
  const f = useCurrentFrame();
  const d = f - at;
  if (d < -2 || d > dur) return null;
  const a = d < 0 ? strength * (1 + d / 2) * 0.5 : strength * Math.pow(1 - d / dur, 2);
  return <AbsoluteFill style={{ background: `rgba(${color},${a})`, mixBlendMode: "screen", pointerEvents: "none" }} />;
};
