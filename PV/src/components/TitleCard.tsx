import React from "react";
import { AbsoluteFill, Img, staticFile, useCurrentFrame } from "remotion";
import { clamp01, easeInOut, easeOut, rand } from "../lib";
import { Kinetic } from "./Type";

const tornPolygon = (seed: number, n = 26, amp = 1.6) => {
  const pts: string[] = [];
  const edge = (from: [number, number], to: [number, number], k: number) => {
    for (let i = 0; i < n; i++) {
      const t = i / n;
      const x = from[0] + (to[0] - from[0]) * t;
      const y = from[1] + (to[1] - from[1]) * t;
      const j = (rand(seed, i, k) - 0.5) * 2 * amp;
      const nx = from[1] === to[1] ? 0 : 1;
      pts.push(`${(x + (nx ? j : 0)).toFixed(2)}% ${(y + (nx ? 0 : j * 1.6)).toFixed(2)}%`);
    }
  };
  edge([2, 3], [98, 3], 1);
  edge([98, 3], [98, 97], 2);
  edge([98, 97], [2, 97], 3);
  edge([2, 97], [2, 3], 4);
  return `polygon(${pts.join(",")})`;
};
const POLY = tornPolygon(9);

/** Parchment card floating in 3D with 3D-assembled hand lettering. */
export const TitleCard: React.FC<{ text: string; textFrom: number; hit: number; exitAt: number }> = ({ text, textFrom, hit, exitAt }) => {
  const f = useCurrentFrame();
  const inK = easeOut(clamp01(f / 22));
  const outK = easeInOut(clamp01((f - exitAt) / 12));
  const breathe = Math.sin(f / 18) * 1.2;
  const pulse = Math.max(0, 1 - Math.abs(f - hit) / 10);
  const z = -1300 * (1 - inK) + 60 * pulse - 900 * outK;
  const rx = 22 * (1 - inK) + breathe * 0.6 + 50 * outK;
  const ry = -28 * (1 - inK) + breathe - 20 * outK;
  const y = -700 * outK;
  const glow = 0.15 + 0.85 * clamp01((f - hit + 2) / 4) * (0.55 + 0.45 * Math.max(0, 1 - (f - hit) / 30));
  return (
    <AbsoluteFill style={{ perspective: 1500, perspectiveOrigin: "50% 45%" }}>
      <div
        style={{
          position: "absolute",
          left: 260,
          top: 300,
          width: 1400,
          height: 480,
          transformStyle: "preserve-3d",
          transform: `translate3d(0, ${y}px, ${z}px) rotateX(${rx}deg) rotateY(${ry}deg)`,
          filter: outK > 0 ? `blur(${outK * 10}px)` : undefined,
          opacity: 1 - outK,
        }}
      >
        <div style={{ position: "absolute", inset: 0, clipPath: POLY, boxShadow: "0 50px 120px rgba(0,0,0,0.8)" }}>
          <Img src={staticFile("art/paper.png")} style={{ width: "100%", height: "100%", objectFit: "cover", filter: "brightness(0.42) sepia(0.6)" }} />
          <div style={{ position: "absolute", inset: 0, background: "radial-gradient(ellipse at 50% 50%, rgba(80,50,20,0) 30%, rgba(20,10,4,0.75) 100%)" }} />
          <div style={{ position: "absolute", left: "8%", right: "8%", top: "16%", height: 2, background: "rgba(233,196,106,0.35)" }} />
          <div style={{ position: "absolute", left: "8%", right: "8%", bottom: "16%", height: 2, background: "rgba(233,196,106,0.35)" }} />
        </div>
        <div style={{ position: "absolute", left: 0, right: 0, top: 150, transformStyle: "preserve-3d", transform: "translateZ(40px)" }}>
          <Kinetic text={text} from={textFrom} to={exitAt + 40} size={120} color="#F1DFB4" glow={glow} stagger={1.6} arrive={16} depth={700} seed={21} />
        </div>
      </div>
    </AbsoluteFill>
  );
};
