import React from "react";
import { AbsoluteFill, Img, staticFile, useCurrentFrame } from "remotion";
import { Key, clamp01, easeInOut, easeOut, kf } from "../lib";
import { HAND, Kinetic, LOGO } from "./Type";

/**
 * Title art split into parallax planes (backdrop, princess, prince, logo) with a rack focus
 * pulling from the prince to the princess before the logo is assembled in 3D.
 */
export const EndCard: React.FC<{ pre: number; tagAt: number }> = ({ pre, tagAt }) => {
  const t = useCurrentFrame() - pre;
  const dolly = easeInOut(clamp01((t + pre) / 200));
  // focus pull: prince sharp -> princess sharp -> logo sharp (both characters soft)
  const keys: Key[] = [
    { t: -pre, prince: 6, princess: 14, back: 10 },
    { t: 6, prince: 0, princess: 12, back: 8 },
    { t: 30, prince: 0, princess: 12 },
    { t: 54, prince: 10, princess: 0 },
    { t: 74, prince: 10, princess: 0 },
    { t: 96, prince: 5, princess: 4, back: 5 },
  ];
  const bPrince = kf(keys, "prince", t);
  const bPrincess = kf(keys, "princess", t);
  const bBack = kf(keys, "back", t);
  const breathe = Math.sin(t / 22);
  const charIn = easeOut(clamp01((t + pre) / 26));

  const logoFrom = 34;
  return (
    <AbsoluteFill style={{ perspective: 1600, perspectiveOrigin: "50% 45%", background: "#050302", overflow: "hidden" }}>
      {/* backdrop */}
      <div
        style={{
          position: "absolute",
          inset: 0,
          transform: `translate3d(${-30 * dolly}px, ${10 * dolly}px, ${-500 + 120 * dolly}px) scale(1.42)`,
          filter: `blur(${bBack.toFixed(2)}px) brightness(0.8)`,
        }}
      >
        <Img src={staticFile("art/Title_Backdrop.png")} style={{ width: "100%", height: "100%", objectFit: "cover" }} />
      </div>
      {/* princess (left) */}
      <div
        style={{
          position: "absolute",
          left: -40,
          bottom: -40,
          height: 1060,
          width: 725,
          transform: `translate3d(${-120 * (1 - charIn) - 40 * dolly}px, ${breathe * 4}px, ${-80 + 140 * dolly}px) rotateY(${8 - 4 * dolly}deg) scale(${1 + 0.012 * breathe})`,
          transformOrigin: "50% 100%",
          filter: `blur(${bPrincess.toFixed(2)}px) brightness(${0.78 + 0.12 * (1 - bPrincess / 14)})`,
          opacity: charIn,
        }}
      >
        <Img src={staticFile("art/Title_Princess.png")} style={{ width: "100%", height: "100%", objectFit: "contain", objectPosition: "bottom" }} />
      </div>
      {/* prince (right) */}
      <div
        style={{
          position: "absolute",
          right: -40,
          bottom: -40,
          height: 1060,
          width: 725,
          transform: `translate3d(${120 * (1 - charIn) + 40 * dolly}px, ${-breathe * 4}px, ${-40 + 160 * dolly}px) rotateY(${-8 + 4 * dolly}deg) scale(${1 - 0.012 * breathe})`,
          transformOrigin: "50% 100%",
          filter: `blur(${bPrince.toFixed(2)}px) brightness(${0.8 + 0.12 * (1 - bPrince / 12)})`,
          opacity: charIn,
        }}
      >
        <Img src={staticFile("art/Title_Prince.png")} style={{ width: "100%", height: "100%", objectFit: "contain", objectPosition: "bottom" }} />
      </div>
      {/* warm haze between the planes */}
      <AbsoluteFill style={{ background: "radial-gradient(ellipse 50% 40% at 50% 40%, rgba(255,190,110,0.16) 0%, rgba(0,0,0,0) 70%)", mixBlendMode: "screen" }} />
      <AbsoluteFill style={{ background: "linear-gradient(180deg, rgba(5,3,2,0.5) 0%, rgba(5,3,2,0) 30%, rgba(5,3,2,0) 70%, rgba(5,3,2,0.6) 100%)" }} />
      {/* logo */}
      <div style={{ position: "absolute", left: 0, right: 0, top: 250, transformStyle: "preserve-3d", transform: `translateZ(${60 * dolly}px)` }}>
        <Kinetic text="DUSK PRINCESS" from={logoFrom} to={9999} size={92} font={LOGO} color="#EBD39A" glow={0.8} stagger={1.3} arrive={18} depth={900} letterSpacing={2} seed={31} />
        <div style={{ height: 6 }} />
        <Kinetic text="and" from={logoFrom + 14} to={9999} size={40} font={LOGO} color="#D9C08A" glow={0.5} stagger={2} arrive={14} depth={500} seed={32} />
        <div style={{ height: 4 }} />
        <Kinetic text="TWILIGHT PRINCE" from={logoFrom + 10} to={9999} size={92} font={LOGO} color="#EBD39A" glow={0.8} stagger={1.3} arrive={18} depth={900} letterSpacing={2} seed={33} />
      </div>
      <div style={{ position: "absolute", left: 0, right: 0, top: 640 }}>
        <Kinetic text="목소리로 푸는 2인 퍼즐 동화" from={tagAt} to={9999} size={58} font={HAND} color="#EADBB8" stagger={1.5} arrive={14} depth={400} seed={34} />
      </div>
    </AbsoluteFill>
  );
};
