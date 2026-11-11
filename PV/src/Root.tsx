import React from "react";
import { Composition, staticFile } from "remotion";
import { loadFont } from "@remotion/fonts";
import { DuskPV } from "./Video";
import { DURATION_F } from "./timeline";
import { FPS, H, W } from "./lib";

loadFont({ family: "Ryuryu", url: staticFile("fonts/ryuryu.ttf") });
loadFont({ family: "Cinzel Decorative", url: staticFile("fonts/CinzelDecorative-Bold.ttf"), weight: "700" });
loadFont({ family: "Cinzel Decorative", url: staticFile("fonts/CinzelDecorative-Regular.ttf"), weight: "400" });
loadFont({ family: "Gowun Batang", url: staticFile("fonts/GowunBatang-Regular.ttf") });

export const Root: React.FC = () => (
  <Composition id="DuskPV" component={DuskPV} durationInFrames={DURATION_F} fps={FPS} width={W} height={H} />
);
