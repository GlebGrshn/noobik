/**
 * The ad's edit. Footage: the 1080p flyovers recorded by PromoRecorder (Recordings/promo-*) and two
 * 4K stills of the house from PromoStills. Shot and caption times are in half-bars of the music (see BEAT).
 */

export type Clip =
  | "01_yard"
  | "02_descent"
  | "03_roots"
  | "04_slate"
  | "05_crystals"
  | "06_magma_meteor"
  | "07_cthulhu"
  | "08_victory";

export type Shot = {
  start: number;
  length: number;
  /** A flyover clip from `from` seconds, or a still image (path under public/media). */
  clip?: Clip;
  from?: number;
  image?: string;
  /** The image is an interface screenshot: never cropped. */
  ui?: boolean;
  /** Horizontal point of interest for the 9:16 crop (0 left … 1 right). */
  focus?: number;
  /** Depth chip in the HUD style. */
  depth?: { metres: number; place: string };
};

export type Caption = { start: number; length: number; lines: string[]; accent?: number };
export type Sound = { at: number; src: string; volume?: number };
/** Full-screen flash and camera shake at a moment (in beats). */
export type Hit = { at: number; flash: number; shake: number };

export type Cut = {
  beats: number;
  shots: Shot[];
  captions: Caption[];
  sounds: Sound[];
  hits: Hit[];
  /** Where the boss music takes over from the digging music. */
  bossAt: number;
  endAt: number;
};

// Clip moments (seconds inside the clips).
const EXPLOSION = 12.05; // 06_magma_meteor: the dynamite goes off
const FINAL_HIT = 1.03; // 08_victory: the last harpoon hit, the boss starts to sink

const ROOTS = { metres: 18, place: "Лагерь у корней" };
const SLATE = { metres: 48, place: "Старая штольня" };
const CRYSTALS = { metres: 88, place: "Кристальный грот" };
const MAGMA = { metres: 110, place: "Магма" };

/** 30 seconds: the whole journey, the shop, the meteorite and the boss. */
export const Full: Cut = {
  beats: 38,
  bossAt: 23,
  endAt: 34,
  shots: [
    { start: 0, length: 3, clip: "01_yard", from: 2.6, focus: 0.5 },
    { start: 3, length: 3, clip: "02_descent", from: 0.6 },
    { start: 6, length: 2, clip: "03_roots", from: 2.4, depth: ROOTS, focus: 0.45 },
    { start: 8, length: 2, clip: "04_slate", from: 2.4, depth: SLATE, focus: 0.5 },
    { start: 10, length: 2, clip: "05_crystals", from: 2.4, depth: CRYSTALS, focus: 0.4 },
    { start: 12, length: 2, clip: "02_descent", from: 12.8, depth: MAGMA },
    { start: 14, length: 2, image: "stills/06_house_buyer.jpg", focus: 0.6 },
    { start: 16, length: 2, image: "stills/07_house_workshop.jpg", focus: 0.45 },
    { start: 18, length: 5, clip: "06_magma_meteor", from: EXPLOSION - 4 * 0.79, focus: 0.47 },
    { start: 23, length: 3, clip: "07_cthulhu", from: 1.0 },
    { start: 26, length: 4, clip: "07_cthulhu", from: 13.0 },
    { start: 30, length: 4, clip: "08_victory", from: FINAL_HIT - 0.33 },
  ],
  captions: [
    { start: 0.2, length: 2.8, lines: ["Что скрыто", "под двором?"], accent: 1 },
    { start: 3, length: 3, lines: ["120 метров", "тайн и сокровищ"], accent: 0 },
    { start: 6.3, length: 7.7, lines: ["Копай глубже"], accent: 0 },
    { start: 14, length: 4, lines: ["Продавай руду", "улучшай снаряжение"], accent: 1 },
    { start: 18.3, length: 4.7, lines: ["Взорви метеорит", "динамитом"], accent: 0 },
    { start: 23.3, length: 2.7, lines: ["Разбуди", "древнее зло"], accent: 1 },
    { start: 26, length: 4, lines: ["Сразись", "с Ктулху"], accent: 1 },
    { start: 30.4, length: 3.6, lines: ["И победи!"], accent: 0 },
  ],
  sounds: [
    { at: 3, src: "swing", volume: 0.8 },
    { at: 3.05, src: "dig_dirt", volume: 0.9 },
    { at: 6, src: "dig_dirt", volume: 0.6 },
    { at: 8, src: "dig_stone", volume: 0.6 },
    { at: 10, src: "dig_crystal", volume: 0.6 },
    { at: 12, src: "sizzle", volume: 0.7 },
    { at: 14, src: "coin", volume: 0.8 },
    { at: 16, src: "upgrade", volume: 0.8 },
    { at: 20.2, src: "fuse", volume: 0.7 },
    { at: 22, src: "boom", volume: 1 },
    { at: 23, src: "roar", volume: 1 },
    ...[0, 0.5, 1, 1.5, 2, 2.5].map((t) => ({ at: 26 + t / 0.79, src: "harpoon", volume: 0.55 })),
    { at: 30 + 0.33 / 0.79, src: "boss_hit", volume: 1 },
    { at: 34, src: "zone", volume: 0.9 },
  ],
  hits: [
    { at: 22, flash: 0.9, shake: 26 },
    { at: 23, flash: 0.5, shake: 14 },
    { at: 30 + 0.33 / 0.79, flash: 0.45, shake: 18 },
  ],
};

/** 15 seconds: the dig, the meteorite, the boss. */
export const Short: Cut = {
  beats: 19,
  bossAt: 10,
  endAt: 14,
  shots: [
    { start: 0, length: 2, clip: "01_yard", from: 2.6, focus: 0.5 },
    { start: 2, length: 2, clip: "02_descent", from: 0.6 },
    { start: 4, length: 1, clip: "03_roots", from: 2.8, depth: ROOTS, focus: 0.45 },
    { start: 5, length: 1, clip: "04_slate", from: 2.8, depth: SLATE, focus: 0.5 },
    { start: 6, length: 1, clip: "05_crystals", from: 2.8, depth: CRYSTALS, focus: 0.4 },
    { start: 7, length: 3, clip: "06_magma_meteor", from: EXPLOSION - 2 * 0.79, focus: 0.47 },
    { start: 10, length: 2, clip: "07_cthulhu", from: 1.4 },
    // Only 1.6 s here: start as the boss begins to sink, the hit lands on the cut.
    { start: 12, length: 2, clip: "08_victory", from: FINAL_HIT + 0.85 },
  ],
  captions: [
    { start: 0.15, length: 1.85, lines: ["Что скрыто", "под двором?"], accent: 1 },
    { start: 2, length: 2, lines: ["120 метров", "тайн и сокровищ"], accent: 0 },
    { start: 4.2, length: 2.8, lines: ["Копай глубже"], accent: 0 },
    { start: 7.2, length: 2.8, lines: ["Взорви метеорит"], accent: 0 },
    { start: 10.2, length: 1.8, lines: ["Разбуди", "древнее зло"], accent: 1 },
    { start: 12.3, length: 1.7, lines: ["И победи!"], accent: 0 },
  ],
  sounds: [
    { at: 2, src: "swing", volume: 0.8 },
    { at: 2.05, src: "dig_dirt", volume: 0.9 },
    { at: 4, src: "dig_dirt", volume: 0.6 },
    { at: 5, src: "dig_stone", volume: 0.6 },
    { at: 6, src: "dig_crystal", volume: 0.6 },
    { at: 7.4, src: "fuse", volume: 0.7 },
    { at: 9, src: "boom", volume: 1 },
    { at: 10, src: "roar", volume: 1 },
    { at: 12, src: "boss_hit", volume: 1 },
    { at: 14, src: "zone", volume: 0.9 },
  ],
  hits: [
    { at: 9, flash: 0.9, shake: 26 },
    { at: 10, flash: 0.5, shake: 14 },
    { at: 12, flash: 0.45, shake: 18 },
  ],
};

export const Cuts = { full: Full, short: Short };
