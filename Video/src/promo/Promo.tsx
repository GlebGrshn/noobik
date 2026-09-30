import { AbsoluteFill, Audio, interpolate, Sequence, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { CaptionView } from "./CaptionView";
import { DepthTag } from "./DepthTag";
import { EndCard } from "./EndCard";
import { ShotView } from "./ShotView";
import { beat, sec } from "./theme";
import { Cuts } from "./timeline";

export type PromoProps = { cut: "full" | "short"; portrait: boolean };

// Music sections picked by loudness: "Digging in the Dirt" is steady from 80 s, "Subterranean Pressure"
// is at its most intense from 174 s. Both start on a beat.
const DIG_MUSIC = 80.53, BOSS_MUSIC = 174.29, MUSIC = 0.62;
const clamp = { extrapolateLeft: "clamp", extrapolateRight: "clamp" } as const;

export const Promo: React.FC<PromoProps> = ({ cut: which, portrait }) => {
  const cut = Cuts[which];
  const frame = useCurrentFrame();
  const { durationInFrames } = useVideoConfig();
  const bossFrame = beat(cut.bossAt);
  const endFrame = beat(cut.endAt);

  // Flashes and camera shake at the explosion, the boss's appearance and the final hit.
  let flash = 0, dx = 0, dy = 0;
  for (const hit of cut.hits) {
    const t = frame - beat(hit.at);
    if (t < 0 || t > 14) continue;
    flash = Math.max(flash, hit.flash * interpolate(t, [0, 10], [1, 0], clamp));
    const fall = interpolate(t, [0, 14], [1, 0], clamp) * hit.shake;
    dx += Math.sin(t * 2.7 + hit.at) * fall;
    dy += Math.cos(t * 3.3 + hit.at * 2) * fall * 0.7;
  }

  return (
    <AbsoluteFill style={{ backgroundColor: "#000" }}>
      <AbsoluteFill style={{ transform: `translate(${dx}px, ${dy}px) scale(${1 + (Math.abs(dx) + Math.abs(dy)) / 2000})` }}>
        {cut.shots.map((shot, i) => {
          const from = beat(shot.start), frames = beat(shot.start + shot.length) - from;
          return (
            <Sequence key={i} from={from} durationInFrames={frames} name={shot.clip ?? shot.image}>
              <ShotView shot={shot} frames={frames} portrait={portrait} />
              {shot.depth && <DepthTag metres={shot.depth.metres} place={shot.depth.place} portrait={portrait} />}
            </Sequence>
          );
        })}
      </AbsoluteFill>

      {/* Darkening where the captions sit, so white type reads over the bright yard. */}
      <AbsoluteFill
        style={{
          background: portrait
            ? "linear-gradient(180deg, rgba(0,0,0,0) 45%, rgba(0,0,0,0.38) 62%, rgba(0,0,0,0.38) 80%, rgba(0,0,0,0) 95%)"
            : "linear-gradient(180deg, rgba(0,0,0,0) 55%, rgba(0,0,0,0.42) 100%)",
          opacity: frame < endFrame ? 1 : 0,
        }}
      />

      {cut.captions.map((caption, i) => {
        const from = beat(caption.start), frames = beat(caption.start + caption.length) - from;
        return (
          <Sequence key={i} from={from} durationInFrames={frames} name={caption.lines.join(" ")}>
            <CaptionView caption={caption} frames={frames} portrait={portrait} />
          </Sequence>
        );
      })}

      <Sequence from={endFrame} name="End card">
        <EndCard portrait={portrait} />
      </Sequence>

      <AbsoluteFill style={{ backgroundColor: "#fff", opacity: flash, pointerEvents: "none" }} />

      <Sequence durationInFrames={bossFrame + 8} name="Digging music">
        <Audio
          src={staticFile("media/music/music_2_roots.mp3")}
          trimBefore={sec(DIG_MUSIC)}
          volume={(f) => interpolate(f, [0, 3, bossFrame - 2, bossFrame + 8], [0, MUSIC, MUSIC, 0], clamp)}
        />
      </Sequence>
      <Sequence from={bossFrame} name="Boss music">
        <Audio
          src={staticFile("media/music/music_4_boss.mp3")}
          trimBefore={sec(BOSS_MUSIC)}
          volume={(f) =>
            interpolate(f, [0, 2, durationInFrames - bossFrame - 36, durationInFrames - bossFrame - 2], [0, MUSIC, MUSIC, 0], clamp)
          }
        />
      </Sequence>
      {cut.sounds.map((sound, i) => (
        <Sequence key={i} from={beat(sound.at)} name={sound.src}>
          <Audio src={staticFile(`media/sfx/${sound.src}.wav`)} volume={sound.volume ?? 1} />
        </Sequence>
      ))}
    </AbsoluteFill>
  );
};
