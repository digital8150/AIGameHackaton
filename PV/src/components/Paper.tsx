import React from "react";
import { AbsoluteFill, Img, staticFile } from "remotion";

export const PaperTexture: React.FC<{ tone?: number; style?: React.CSSProperties }> = ({ tone = 1, style }) => (
  <AbsoluteFill style={{ background: "#c8b083", ...style }}>
    <Img src={staticFile("art/paper.png")} style={{ width: "100%", height: "100%", objectFit: "cover", filter: `brightness(${tone})` }} />
    <AbsoluteFill
      style={{
        background:
          "radial-gradient(ellipse at 50% 45%, rgba(255,240,205,0.18) 0%, rgba(0,0,0,0) 55%), radial-gradient(ellipse at 50% 50%, rgba(0,0,0,0) 55%, rgba(60,36,14,0.55) 100%)",
      }}
    />
  </AbsoluteFill>
);
