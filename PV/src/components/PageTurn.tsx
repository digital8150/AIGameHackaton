import React from "react";
import { AbsoluteFill, useCurrentFrame } from "remotion";
import { H, W, clamp01, easeInOut } from "../lib";
import { PaperTexture } from "./Paper";

/**
 * Wraps the OUTGOING scene. Between frames `start` and `start+dur` (Sequence-local)
 * the scene becomes a page hinged on the left spine and turns over in 3D,
 * showing its paper back and casting a shadow on whatever lies below.
 */
export const PageTurn: React.FC<{ start: number; dur: number; children: React.ReactNode; hinge?: "left" | "right" }> = ({ start, dur, children, hinge = "left" }) => {
  const f = useCurrentFrame();
  const raw = clamp01((f - start) / dur);
  if (raw >= 1) return null;
  const p = easeInOut(raw);
  const sign = hinge === "left" ? -1 : 1;
  const angle = sign * 180 * p;
  // the page bows: lift the free edge toward camera a little mid-turn
  const lift = Math.sin(p * Math.PI);
  const shade = Math.sin(Math.min(1, p * 1.15) * Math.PI * 0.5);
  return (
    <AbsoluteFill style={{ perspective: 2200, perspectiveOrigin: hinge === "left" ? "0% 50%" : "100% 50%" }}>
      {/* shadow the turning page casts on the page below */}
      {raw > 0 ? (
        <AbsoluteFill
          style={{
            background:
              hinge === "left"
                ? `linear-gradient(90deg, rgba(0,0,0,${0.55 * lift}) 0%, rgba(0,0,0,${0.35 * lift}) ${Math.max(5, 100 * (1 - p))}%, rgba(0,0,0,0) ${Math.min(100, 100 * (1 - p) + 18)}%)`
                : `linear-gradient(270deg, rgba(0,0,0,${0.55 * lift}) 0%, rgba(0,0,0,0) ${Math.min(100, 100 * (1 - p) + 18)}%)`,
          }}
        />
      ) : null}
      <div
        style={{
          position: "absolute",
          inset: 0,
          width: W,
          height: H,
          transformStyle: "preserve-3d",
          transformOrigin: hinge === "left" ? "0% 50%" : "100% 50%",
          transform: `translateZ(${lift * 90}px) rotateY(${angle}deg) rotateZ(${sign * -lift * 1.6}deg)`,
        }}
      >
        {/* front: the scene */}
        <div style={{ position: "absolute", inset: 0, backfaceVisibility: "hidden", overflow: "hidden" }}>
          {children}
          <div
            style={{
              position: "absolute",
              inset: 0,
              background: `linear-gradient(${hinge === "left" ? 270 : 90}deg, rgba(255,226,170,${0.22 * lift}) 0%, rgba(0,0,0,${0.5 * shade}) 70%, rgba(0,0,0,${0.65 * shade}) 100%)`,
            }}
          />
        </div>
        {/* back: plain parchment */}
        <div style={{ position: "absolute", inset: 0, backfaceVisibility: "hidden", transform: "rotateY(180deg)" }}>
          <PaperTexture />
          <div style={{ position: "absolute", inset: 0, background: `linear-gradient(90deg, rgba(0,0,0,${0.45 * (1 - p)}) 0%, rgba(0,0,0,0) 60%)` }} />
        </div>
      </div>
    </AbsoluteFill>
  );
};
