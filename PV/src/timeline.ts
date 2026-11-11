import data from "./segments.json";
import { FPS, sec } from "./lib";

export type Piece = { in: number; out: number; speed: number };
export type Segment = { id: string; note?: string; pieces: Piece[] };

export const SEGMENTS: Record<string, Segment> = Object.fromEntries((data.segments as Segment[]).map((s) => [s.id, s]));
export const HANDLE_F = Math.round(data.handle * FPS);

/** output length of a segment (seconds, without handles) */
export const segLen = (id: string) => SEGMENTS[id].pieces.reduce((a, p) => a + (p.out - p.in) / p.speed, 0);

/** output time (seconds from the segment in-point) at which a given SOURCE time is shown */
export const srcToLocal = (id: string, src: number) => {
  let acc = 0;
  for (const p of SEGMENTS[id].pieces) {
    if (src <= p.out) return acc + (Math.max(src, p.in) - p.in) / p.speed;
    acc += (p.out - p.in) / p.speed;
  }
  return acc;
};

// ---------------------------------------------------------------------------------------
// Edit decision list (timeline seconds). Every in-point is placed explicitly so that
// the spoken-word reactions land on music onsets (music starts at MUSIC_OFFSET).
// ---------------------------------------------------------------------------------------
export const MUSIC_OFFSET = 102.25; // second of cutscene_you_are_my_ears.mp3 played at t=0 (S6 card lands on the 128.35s hit)
export const TOTAL = 74.2;

export const T = {
  s12: 0.6,
  s3: 9.6,
  s4: 18.6,
  s5a: 23.4,
  s5b: 24.4,
  card6: 25.3,
  s6a: 27.9,
  s6b: 31.0,
  s6c: 33.9,
  s6end: 39.8,
  s7: 39.8,
  m: [41.8, 42.7, 43.6, 44.5, 45.4, 46.3],
  m7: 47.2,
  s8a: 49.2,
  s8b: 51.6,
  s8c: 55.529,
  s9: [59.629, 61.329, 63.029, 65.629],
  s10: 68.029,
};

export const MONTAGE = ["s7m1_cave_box", "s7m2_cave_voice", "s7m3_fakewall", "s7m4_stone_reveal", "s7m5_impact", "s7m6_tower_echo"];
export const STORY = ["s9a_witch_meet", "s9b_witch_close", "s9c_hands", "s9d_forest"];

export const DURATION_F = sec(TOTAL);
