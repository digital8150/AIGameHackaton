import React from "react";
import { AbsoluteFill, OffthreadVideo, staticFile, useCurrentFrame } from "remotion";
import { H, W, Key, kf } from "../lib";

/** Camera/plane pose at local frame t. Units: px, degrees. */
export const pose = (keys: Key[], t: number) => ({
  x: kf(keys, "x", t, 0),
  y: kf(keys, "y", t, 0),
  z: kf(keys, "z", t, 0),
  rx: kf(keys, "rx", t, 0),
  ry: kf(keys, "ry", t, 0),
  rz: kf(keys, "rz", t, 0),
  s: kf(keys, "s", t, 1),
  blur: kf(keys, "blur", t, 0),
  bright: kf(keys, "bright", t, 1),
  sat: kf(keys, "sat", t, 1),
  ox: kf(keys, "ox", t, 50),
  oy: kf(keys, "oy", t, 50),
  op: kf(keys, "op", t, 1),
  chroma: kf(keys, "chroma", t, 0),
  frame: kf(keys, "frame", t, 0),
});

export const transformOf = (p: ReturnType<typeof pose>) =>
  `translate3d(${p.x}px, ${p.y}px, ${p.z}px) rotateX(${p.rx}deg) rotateY(${p.ry}deg) rotateZ(${p.rz}deg) scale(${p.s})`;

/** SVG chromatic fringe filter (red channel pushed one way, blue the other). */
export const ChromaDefs: React.FC<{ id: string; dx: number }> = ({ id, dx }) => (
  <svg width="0" height="0" style={{ position: "absolute" }}>
    <filter id={id} x="-5%" y="-5%" width="110%" height="110%" colorInterpolationFilters="sRGB">
      <feColorMatrix in="SourceGraphic" type="matrix" values="1 0 0 0 0  0 0 0 0 0  0 0 0 0 0  0 0 0 1 0" result="r" />
      <feOffset in="r" dx={dx} dy="0" result="ro" />
      <feColorMatrix in="SourceGraphic" type="matrix" values="0 0 0 0 0  0 1 0 0 0  0 0 0 0 0  0 0 0 1 0" result="g" />
      <feColorMatrix in="SourceGraphic" type="matrix" values="0 0 0 0 0  0 0 0 0 0  0 0 1 0 0  0 0 0 1 0" result="b" />
      <feOffset in="b" dx={-dx} dy="0" result="bo" />
      <feBlend in="ro" in2="g" mode="screen" result="rg" />
      <feBlend in="rg" in2="bo" mode="screen" />
    </filter>
  </svg>
);

export type PlaneProps = {
  /** clip file id under public/clips, or an image under public/ via `image` */
  clip?: string;
  image?: string;
  /** frames of the clip to skip (handle offset) */
  trimBefore?: number;
  /** keyframes, t = frames relative to the shot's in-point */
  cam: Key[];
  /** frames between Sequence start and the in-point */
  pre?: number;
  id: string;
  perspective?: number;
  /** extra content drawn on the plane (moves with it) */
  children?: React.ReactNode;
  shadow?: boolean;
  /** plane size relative to frame */
  border?: boolean;
};

/** A footage (or image) plane floating in 3D space with lens blur and optional chromatic fringe. */
export const Plane: React.FC<PlaneProps> = ({ clip, image, trimBefore = 0, cam, pre = 0, id, perspective = 1800, children, shadow = true, border = false }) => {
  const t = useCurrentFrame() - pre;
  const p = pose(cam, t);
  const chroma = Math.abs(p.chroma) > 0.2;
  const filters = [
    chroma ? `url(#chroma-${id})` : "",
    p.blur > 0.15 ? `blur(${p.blur.toFixed(2)}px)` : "",
    p.bright !== 1 ? `brightness(${p.bright.toFixed(3)})` : "",
    p.sat !== 1 ? `saturate(${p.sat.toFixed(3)})` : "",
  ].join(" ");
  return (
    <AbsoluteFill style={{ perspective, perspectiveOrigin: "50% 50%", opacity: p.op }}>
      {chroma ? <ChromaDefs id={`chroma-${id}`} dx={p.chroma} /> : null}
      <div
        style={{
          position: "absolute",
          left: 0,
          top: 0,
          width: W,
          height: H,
          transformOrigin: `${p.ox}% ${p.oy}%`,
          transform: transformOf(p),
          filter: filters.trim() || undefined,
          boxShadow: shadow ? "0 40px 120px rgba(0,0,0,0.75), 0 0 0 1px rgba(234,219,184,0.08)" : undefined,
          overflow: "hidden",
          backfaceVisibility: "hidden",
          outline: border || p.frame > 0 ? `${(p.frame || 1) * 10}px solid rgba(217,198,156,${Math.min(1, p.frame || 0.6)})` : undefined,
        }}
      >
        {clip ? (
          <OffthreadVideo src={staticFile(`clips/${clip}.mp4`)} trimBefore={trimBefore} muted style={{ width: W, height: H, display: "block" }} />
        ) : image ? (
          <img src={staticFile(image)} style={{ width: W, height: H, objectFit: "cover", display: "block" }} />
        ) : null}
        {children}
      </div>
    </AbsoluteFill>
  );
};
