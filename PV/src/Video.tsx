import React from "react";
import { AbsoluteFill, Audio, Sequence, interpolate, staticFile, useCurrentFrame } from "remotion";
import { H, Key, W, clamp01, easeInOut, easeOut, prog, sec } from "./lib";
import { HANDLE_F, MONTAGE, MUSIC_OFFSET, STORY, T, TOTAL, segLen, srcToLocal } from "./timeline";
import { Plane } from "./components/Plane";
import { PageTurn } from "./components/PageTurn";
import { Particles, Burst, Ring, Scraps } from "./components/Particles";
import { Caption, WordPop, Kinetic, HAND } from "./components/Type";
import { Flash, Lens, LightLeak } from "./components/Light";
import { TitleCard } from "./components/TitleCard";
import { EndCard } from "./components/EndCard";

const TURN = 18; // page-turn length in frames
const HALF = TURN / 2;

// -------------------------------------------------------------------------------------------
// A clip shot: in-point at timeline second `at`, visible for `len` seconds, plus `pre` frames
// before / `post` frames after taken from the clip's handles (for overlapping transitions).
// -------------------------------------------------------------------------------------------
type ShotProps = {
  clip: string;
  at: number;
  len: number;
  pre?: number;
  post?: number;
  cam: Key[];
  turnOut?: boolean; // page-turn away at the end (uses post frames)
  children?: React.ReactNode; // overlays inside the shot's Sequence (local frames from in-point via `pre`)
  z?: number;
};

const Shot: React.FC<ShotProps> = ({ clip, at, len, pre = 0, post = 0, cam, turnOut, children, z }) => {
  const from = sec(at) - pre;
  const dur = sec(len) + pre + post;
  const body = (
    <>
      <Plane id={clip} clip={clip} trimBefore={HANDLE_F - pre} cam={cam} pre={pre} />
      {children}
    </>
  );
  return (
    <Sequence from={from} durationInFrames={dur} layout="absolute-fill" style={{ zIndex: z }}>
      {turnOut ? <PageTurn start={dur - post - HALF} dur={TURN}>{body}</PageTurn> : body}
    </Sequence>
  );
};

/** helper: place local overlays at frames relative to the in-point inside a Shot */
const Local: React.FC<{ pre: number; children: React.ReactNode }> = ({ pre, children }) => (
  <Sequence from={pre} layout="absolute-fill">
    {children}
  </Sequence>
);

const F = (s: number) => sec(s);

export const DuskPV: React.FC = () => {
  const frame = useCurrentFrame();

  // ---------------- absolute event frames ----------------
  const echo1 = F(T.s3 + srcToLocal("s3_prince", 35.5));
  const echo2 = F(T.s3 + srcToLocal("s3_prince", 41.1));
  const popBridge = F(T.s6a + srcToLocal("s6a_bridge", 80.5));
  const popPillar = F(T.s6b + srcToLocal("s6b_pillar", 89.6));
  const popStone = F(T.s6c + srcToLocal("s6c_stone", 100.9));
  const popHeavy = F(T.s8b + srcToLocal("s8b_heavy", 390.15));
  const cardHit = F(26.1);

  // montage whip frames
  const mStarts = T.m.map(F);
  const whipDir = (i: number) => (i % 2 === 0 ? 1 : -1);

  // ---------------- particle world ----------------
  const bursts: Burst[] = [
    { f: cardHit, x: 960, y: 540, n: 160, speed: 1500, seed: 3, color: "#E9C46A", life: 60 },
    { f: popBridge, x: 1010, y: 640, n: 130, speed: 1100, seed: 5 },
    { f: popPillar, x: 900, y: 560, n: 130, speed: 1100, seed: 7 },
    { f: popStone, x: 930, y: 700, n: 170, speed: 1300, seed: 9, gravity: 420 },
    { f: popHeavy, x: 1250, y: 560, n: 200, speed: 1500, seed: 13, gravity: 420 },
    { f: mStarts[4] + 20, x: 770, y: 650, n: 110, speed: 1100, seed: 17, gravity: 300 },
    { f: F(T.s10) + 30, x: 960, y: 400, n: 240, speed: 1700, seed: 19, life: 70, gravity: -60 },
  ];
  // word bursts also throw sparks from the caption itself
  for (const [f0, s] of [[popBridge, 21], [popPillar, 23], [popStone, 25], [popHeavy, 27]] as const)
    bursts.push({ f: f0, x: 960, y: 800, n: 70, speed: 800, seed: s, color: "#F2D27E", life: 36, spread: 1.2 });

  const rings: Ring[] = [
    { f: echo1, x: 720, y: 560, r: 760, seed: 1 },
    { f: echo1 + 6, x: 720, y: 560, r: 520, seed: 2, n: 40 },
    { f: echo2, x: 790, y: 650, r: 820, seed: 3 },
    { f: mStarts[2] + 6, x: 640, y: 650, r: 700, seed: 4 },
    { f: mStarts[5] + 1, x: 860, y: 700, r: 760, seed: 5 },
    { f: F(T.s8c + srcToLocal("s8c_last_echo", 396.0)), x: 960, y: 420, r: 1100, seed: 6, life: 50 },
    { f: F(T.s6a + srcToLocal("s6a_bridge", 77.3)), x: 900, y: 640, r: 500, seed: 7, color: "#E9C46A", n: 50 },
    { f: F(T.s6b + srcToLocal("s6b_pillar", 87.0)), x: 830, y: 560, r: 500, seed: 8, color: "#E9C46A", n: 50 },
    { f: F(T.s6c + srcToLocal("s6c_stone", 97.75)), x: 800, y: 600, r: 500, seed: 9, color: "#E9C46A", n: 50 },
  ];
  const turnFrames = [F(T.s5a), F(26.1) - 15, F(T.s6end), F(T.s9[0]), F(T.s9[1]), F(T.s9[2]), F(T.s9[3]), F(T.s10)];
  const scraps: Scraps[] = turnFrames.map((f, i) => ({ f: f - 4, x: 1500 - 600 * (i % 2) * 0, y: 540, n: 46, seed: 40 + i }));

  const mode = (f: number) => {
    const s = f / 30;
    const cold =
      (s > 9.7 && s < 18.6) || (s > 27.9 && s < 37.0) || (s > 52.0 && s < 56.6) ? 1 : s > 41.8 && s < 47.2 ? 0.5 : 0;
    const density = interpolate(s, [0, 1.5, 70, 71, 73.6, 74.2], [0.25, 1, 1, 1.6, 1.2, 0], { extrapolateRight: "clamp" });
    return { warm: 1 - cold * 0.85, cold: cold * 0.85, density };
  };
  // rack focus for the motes: near (sharp foreground) while a caption pulls focus, far otherwise
  const captionWindows: [number, number][] = [
    [0.9, 5.3], [11.1, 14.6], [19.4, 22.9], [25.5, 27.9], [37.4, 39.8], [39.9, 42.4], [47.4, 49.2], [63.6, 67.6], [68.6, 74.2],
  ];
  const focus = (f: number) => {
    const s = f / 30;
    let near = 0;
    for (const [a, b] of captionWindows) near = Math.max(near, clamp01((s - a) / 0.5) * clamp01((b - s) / 0.5));
    return 0.65 - 0.5 * easeInOut(near);
  };
  const pan = (f: number) => {
    let p = 0;
    mStarts.forEach((m, i) => {
      p += whipDir(i) * 700 * easeInOut(clamp01((f - m + 4) / 8));
    });
    p += 700 * easeInOut(clamp01((f - F(T.m7) + 4) / 8));
    return p;
  };

  // ---------------- music ----------------
  const musicVol = (f: number) =>
    interpolate(f / 30, [0, 0.8, 71.6, 73.7], [0, 0.85, 0.85, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });

  // camera shake on the S6 stone landing (as approved in the draft; the capture also shakes here)
  const shake = (() => {
    const d = frame - popStone;
    if (d < 0 || d > 10) return { x: 0, y: 0 };
    const a = 14 * Math.pow(1 - d / 10, 2);
    return { x: Math.sin(d * 2.9) * a, y: Math.cos(d * 3.7) * a * 0.8 };
  })();

  // ---------------- shot list ----------------
  const L = (a: number, b: number) => b - a;

  return (
    <AbsoluteFill style={{ background: "#070504" }}>
      <Audio src={staticFile("music/you_are_my_ears.mp3")} trimBefore={sec(MUSIC_OFFSET)} volume={musicVol} />
      {/* deep void behind the planes */}
      <AbsoluteFill style={{ background: "radial-gradient(ellipse 80% 70% at 50% 45%, #22170e 0%, #0d0906 55%, #050302 100%)" }} />
      <Particles seed={77} count={110} mode={mode} focus={() => 0.9} pan={(f) => pan(f) * 0.5} depthRange={[0.45, 1]} opacity={0.9} />

      <AbsoluteFill style={{ transform: `translate(${shake.x}px, ${shake.y}px)` }}>
        {/* ===== S1+S2: pencil book -> in-game page turn -> stage 1 ===== */}
        <Shot
          clip="s12_book_to_forest"
          at={T.s12}
          len={L(T.s12, T.s3)}
          cam={[
            { t: 0, z: -620, rx: 30, ry: -10, x: 0, y: 40, blur: 9, op: 0, bright: 0.66, sat: 0.9 },
            { t: 18, op: 1 },
            { t: 70, blur: 7 },
            { t: 125, z: -180, rx: 12, ry: -3, y: 10, blur: 0, bright: 0.74 },
            { t: 160, bright: 1, sat: 1 },
            { t: 150, z: -120, rx: 9, ry: -2 },
            { t: 185, z: 0, rx: 0, ry: 0, x: 0, y: 0, s: 1, ox: 50, oy: 45 },
            { t: 270, s: 1.06 },
          ]}
        />
        {/* ===== S3: the prince sees by sound ===== */}
        <Shot
          clip="s3_prince"
          at={T.s3}
          len={L(T.s3, T.s4)}
          cam={[
            { t: 0, s: 1.06, ox: 50, oy: 45, rx: 0 },
            { t: 30, s: 1.0, ox: 37, oy: 52 },
            { t: 52, s: 1.2, ox: 37, oy: 52, rx: 3 },
            { t: 150, s: 1.12, ox: 40, oy: 55, rx: 0 },
            { t: 196, s: 1.0, ox: 41, oy: 60 },
            { t: 208, s: 1.0 },
            { t: 222, s: 1.18, ox: 41, oy: 60 },
            { t: 262, s: 1.18 },
            { t: 270, s: 1.04, chroma: 0 },
          ]}
        />
        {/* ===== S4: the princess opens with strength ===== */}
        <Shot
          clip="s4_princess"
          at={T.s4}
          len={L(T.s4, T.s5a)}
          post={HALF}
          turnOut
          z={2}
          cam={[
            { t: 0, s: 1.1, ry: 8, chroma: 16, blur: 4, ox: 44, oy: 62 },
            { t: 9, s: 1.0, ry: 0, chroma: 0, blur: 0 },
            { t: 150, s: 1.1 },
          ]}
        />
        {/* ===== S5: clear card -> interlude book page ===== */}
        <Shot
          clip="s5a_clear"
          at={T.s5a}
          len={L(T.s5a, T.s5b)}
          pre={HALF}
          z={1}
          cam={[
            { t: -HALF, z: -260, ry: 14, rx: 6 },
            { t: 30, z: 0, ry: 0, rx: 0 },
          ]}
        />
        <Shot
          clip="s5b_interlude"
          at={T.s5b}
          len={L(T.s5b, 25.6)}
          post={HALF}
          turnOut
          z={2}
          cam={[
            { t: 0, s: 1.08, rx: 6 },
            { t: 45, s: 1.0, rx: 0 },
          ]}
        />

        {/* ===== S6: speak, and the world changes ===== */}
        <Sequence from={F(T.card6)} durationInFrames={F(T.s6a) - F(T.card6) + 12} layout="absolute-fill" style={{ zIndex: 1 }}>
          <TitleCard text="말하면, 세계가 변한다" textFrom={6} hit={cardHit - F(T.card6)} exitAt={F(T.s6a) - F(T.card6) - 2} />
        </Sequence>
        {(
          [
            ["s6a_bridge", T.s6a, T.s6b, { ox: 50, oy: 62 }, 1],
            ["s6b_pillar", T.s6b, T.s6c, { ox: 45, oy: 56 }, -1],
          ] as const
        ).map(([clip, a, b, o, dir], i) => {
          const n = F(b) - F(a);
          return (
            <Shot
              key={clip}
              clip={clip}
              at={a}
              len={b - a}
              pre={12}
              post={12}
              z={3 + i}
              cam={[
                { t: -12, z: -1900, ry: 34 * dir, rx: 8, x: 520 * dir, y: -60, blur: 10, s: 0.86, op: 0.2, ox: 50, oy: 50 },
                { t: 0, z: -140, ry: 0, rx: 0, x: 0, y: 0, blur: 0, s: 0.97, op: 1 },
                { t: 10, z: 0, s: 1.0 },
                { t: 24, s: 1.0, ...o },
                { t: 44, s: 1.28, ...o },
                { t: n - 2, s: 1.34, ...o, z: 0, ry: 0, x: 0, blur: 0, op: 1, chroma: 0 },
                { t: n + 12, z: 900, ry: -30 * dir, x: -900 * dir, blur: 14, op: 0, chroma: 10 },
              ]}
            />
          );
        })}
        <Shot
          clip="s6c_stone"
          at={T.s6c}
          len={L(T.s6c, T.s6end)}
          pre={12}
          post={HALF}
          turnOut
          z={5}
          cam={[
            { t: -12, z: -1900, ry: 34, rx: 8, x: 520, y: -60, blur: 10, s: 0.86, op: 0.2 },
            { t: 0, z: -140, ry: 0, rx: 0, x: 0, y: 0, blur: 0, s: 0.97, op: 1 },
            { t: 10, z: 0, s: 1.0, ox: 47, oy: 45 },
            { t: 30, s: 1.0, ox: 47, oy: 45 },
            { t: 52, s: 1.22, ox: 47, oy: 50 },
            { t: popStone - F(T.s6c) - 1, s: 1.26 },
            { t: popStone - F(T.s6c) + 3, s: 1.33 },
            { t: 100, s: 1.12, ox: 50, oy: 55, blur: 0, z: 0, bright: 1 },
            { t: 118, blur: 7, z: -260, bright: 0.62 },
            { t: 180, blur: 8, z: -320, bright: 0.58 },
          ]}
        />

        {/* ===== S7: together ===== */}
        <Shot
          clip="s7_box"
          at={T.s7}
          len={L(T.s7, T.m[0])}
          pre={HALF}
          post={5}
          z={1}
          cam={[
            { t: -HALF, s: 1.2, ox: 43, oy: 62, rx: 7 },
            { t: 60, s: 1.04, rx: 0, x: 0, blur: 0, chroma: 0 },
            { t: 65, x: -1400, ry: 22, blur: 10, chroma: 14 },
          ]}
        />
        {MONTAGE.map((clip, i) => {
          const a = T.m[i];
          const b = i + 1 < T.m.length ? T.m[i + 1] : T.m7;
          const n = F(b) - F(a);
          const d = whipDir(i);
          return (
            <Shot
              key={clip}
              clip={clip}
              at={a}
              len={b - a}
              pre={5}
              post={5}
              z={2 + i}
              cam={[
                { t: -5, x: 1500 * d, ry: -26 * d, z: -300, blur: 10, chroma: 18, s: 1.05 },
                { t: 2, x: 0, ry: 0, z: 0, blur: 0, chroma: 0, s: 1.08 },
                { t: n - 1, s: 1.0, x: 0, ry: 0, blur: 0, chroma: 0, z: 0 },
                { t: n + 5, x: -1500 * d, ry: 26 * d, z: -300, blur: 10, chroma: 18 },
              ]}
            />
          );
        })}
        <Shot
          clip="s7m7_tower_warm"
          at={T.m7}
          len={L(T.m7, T.s8a)}
          pre={5}
          post={10}
          z={10}
          cam={[
            { t: -5, x: 1500, ry: -26, z: -300, blur: 10, chroma: 18, s: 1.12 },
            { t: 2, x: 0, ry: 0, z: 0, blur: 0, chroma: 0, s: 1.12, ox: 45, oy: 60 },
            { t: 58, s: 1.0 },
            { t: 60, z: 0, blur: 0, op: 1 },
            { t: 70, z: 1500, blur: 18, op: 0 },
          ]}
        />

        {/* ===== S8: the last climb ===== */}
        <Shot
          clip="s8a_push"
          at={T.s8a}
          len={L(T.s8a, T.s8b)}
          pre={10}
          z={9}
          cam={[
            { t: -10, z: -1200, blur: 14, rx: 14, op: 0 },
            { t: 4, z: -60, blur: 0, rx: 3, op: 1, s: 1.16, ox: 42, oy: 70 },
            { t: 72, z: 0, rx: 0, s: 1.06 },
          ]}
        />
        <Shot
          clip="s8b_heavy"
          at={T.s8b}
          len={L(T.s8b, T.s8c)}
          z={9}
          cam={[
            { t: 0, s: 1.25, x: -260, y: -30, chroma: 10 },
            { t: 5, chroma: 0 },
            { t: 40, s: 1.6, x: -576, y: -100 },
            { t: popHeavy - F(T.s8b) - 1, s: 1.6, x: -576, y: -100 },
            { t: popHeavy - F(T.s8b) + 2, s: 1.68, x: -620, y: -110 },
            { t: popHeavy - F(T.s8b) + 30, s: 1.6, x: -576, y: -100 },
            { t: 118, s: 1.45, x: -470, y: -80 },
          ]}
        />
        <Shot
          clip="s8c_last_echo"
          at={T.s8c}
          len={L(T.s8c, T.s9[0])}
          post={HALF}
          turnOut
          z={9}
          cam={[
            { t: 0, s: 1.25, ox: 55, oy: 55, rx: 4 },
            { t: 50, s: 1.1, rx: 0 },
            { t: 123, s: 1.0, ox: 50, oy: 45 },
          ]}
        />

        {/* ===== S9: the ending storybook, sepia into colour ===== */}
        {STORY.map((clip, i) => {
          const a = T.s9[i];
          const b = i + 1 < T.s9.length ? T.s9[i + 1] : T.s10;
          const n = F(b) - F(a);
          return (
            <Shot
              key={clip}
              clip={clip}
              at={a}
              len={b - a}
              pre={HALF}
              post={HALF}
              turnOut
              z={8 - i}
              cam={[
                { t: -HALF, s: 1.0, rx: 0, ry: 0 },
                { t: 0, s: 1.02, rx: 3, ry: i % 2 ? -3 : 3, ox: 50, oy: 50 },
                { t: n, s: 1.1, rx: 0, ry: 0, ox: i === 1 ? 45 : 50, oy: i === 1 ? 35 : 50 },
              ]}
            />
          );
        })}

        {/* ===== S10: end card ===== */}
        <Sequence from={F(T.s10) - HALF} durationInFrames={F(TOTAL) - F(T.s10) + HALF} layout="absolute-fill" style={{ zIndex: 0 }}>
          <EndCard pre={HALF} tagAt={F(71.26) - F(T.s10)} />
        </Sequence>
      </AbsoluteFill>

      {/* ===== front particle layer, light ===== */}
      <Particles seed={11} count={70} mode={mode} focus={focus} pan={pan} depthRange={[0, 0.45]} bursts={bursts} rings={rings} scraps={scraps} />
      <LightLeak
        intensity={(f) => {
          const s = f / 30;
          return (
            0.35 * interpolate(s, [0, 2, 9, 10], [0, 1, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }) +
            0.5 * interpolate(s, [25.6, 26.1, 27.4], [0, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }) +
            0.45 * interpolate(s, [57.4, 58.4, 59.9], [0, 1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }) +
            0.5 * interpolate(s, [65.6, 67.0, 70.0, 73.0], [0, 0.8, 1, 0.2], { extrapolateLeft: "clamp", extrapolateRight: "clamp" })
          );
        }}
      />
      <Flash at={cardHit} strength={0.45} />
      <Flash at={popStone} strength={0.35} />
      <Flash at={popHeavy} strength={0.5} />
      <Flash at={F(T.s8a)} strength={0.35} dur={8} />
      {mStarts.map((m) => (
        <Flash key={m} at={m} strength={0.12} dur={5} />
      ))}
      <Lens />

      {/* ===== type ===== */}
      <Caption text="보지 못하는 왕자." from={F(0.9)} to={F(3.0)} seed={1} />
      <Caption text="듣지 못하는 공주." from={F(3.1)} to={F(5.3)} seed={2} />
      <Caption text="왕자는 소리로 본다" from={echo1} to={F(14.6)} seed={3} />
      <Caption text="벽은 벽이 아니다" from={echo2 - 3} to={F(18.55)} small seed={4} />
      <Caption text="공주는 힘으로 연다" from={F(19.4)} to={F(22.9)} seed={5} />
      <WordPop text="「길어져」" at={popBridge} to={F(T.s6b) + 8} />
      <WordPop text="「짧아져」" at={popPillar} to={F(T.s6c) + 8} tilt={4} />
      <WordPop text="「무거워져」" at={popStone} to={F(37.35)} />
      <Caption text="당신의 목소리를, 당신의 컴퓨터 안의 AI가 듣는다" from={F(37.4)} to={F(39.75)} small seed={6} y={560} />
      <Caption text="혼자서는 건널 수 없는 숲" from={F(39.9)} to={F(42.4)} seed={7} />
      <Caption text="둘이라서 건너는 숲" from={F(47.35)} to={F(49.2)} seed={8} />
      <WordPop text="「무거워져라」" at={popHeavy} to={F(55.45)} size={160} />
      <Caption text="서로의 감각이 되어" from={F(63.6)} to={F(67.6)} seed={9} />
      {/* final black */}
      <AbsoluteFill style={{ background: "#000", opacity: interpolate(frame / 30, [73.6, 73.75], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" }) }} />
    </AbsoluteFill>
  );
};
