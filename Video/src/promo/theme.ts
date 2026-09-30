import { loadFont } from "@remotion/google-fonts/Rubik";

// Colours of the game's own interface (loading page, HUD cards).
export const Ink = "#112024";
export const InkSoft = "rgba(17, 32, 36, 0.86)";
export const Cream = "#F7F2DE";
export const Amber = "#FFC451";
export const Mint = "#6FE0B8";
export const Muted = "#B0C5BF";

export const { fontFamily: Display } = loadFont("normal", {
  weights: ["700", "800", "900"],
  subsets: ["latin", "cyrillic"],
});

export const FPS = 30;

/**
 * Cuts land on the music: both tracks run at about 152 BPM, so a half-bar (two beats) is 0.79 s.
 * Every time in the timeline is given in these units.
 */
export const BEAT = 0.79;
export const beat = (n: number) => Math.round(n * BEAT * FPS);
export const sec = (s: number) => Math.round(s * FPS);

/** Thick dark outline behind the letters, readable over any shot. */
export const outlined = (width: number): React.CSSProperties => ({
  WebkitTextStroke: `${width}px ${Ink}`,
  paintOrder: "stroke fill",
  textShadow: `0 ${width * 0.6}px ${width * 1.4}px rgba(0, 0, 0, 0.55)`,
});
