import { AbsoluteFill, Easing, Img, interpolate, OffthreadVideo, staticFile, useCurrentFrame } from "remotion";
import { sec } from "./theme";
import type { Shot } from "./timeline";

const clamp = { extrapolateLeft: "clamp", extrapolateRight: "clamp" } as const;

/**
 * One shot: a slow push-in over its length plus a small punch-in on the cut, for momentum.
 * In 9:16 footage and scene stills are cropped around `focus`; interface screenshots stay whole over a blurred copy.
 */
export const ShotView: React.FC<{ shot: Shot; frames: number; portrait: boolean }> = ({ shot, frames, portrait }) => {
  const frame = useCurrentFrame();
  const push = interpolate(frame, [0, frames], [1, shot.image ? 1.07 : 1.05], clamp);
  const punch = interpolate(frame, [0, 7], [1.06, 1], { ...clamp, easing: Easing.out(Easing.cubic) });
  const scale = push * punch;
  const cover: React.CSSProperties = {
    width: "100%",
    height: "100%",
    objectFit: "cover",
    objectPosition: `${(shot.focus ?? 0.5) * 100}% 50%`,
    transform: `scale(${scale})`,
  };

  if (shot.image) {
    const src = staticFile(`media/${shot.image}`);
    if (!portrait || !shot.ui) return <AbsoluteFill><Img src={src} style={cover} /></AbsoluteFill>;
    return (
      <AbsoluteFill style={{ backgroundColor: "#0b1417" }}>
        <Img src={src} style={{ ...cover, filter: "blur(28px) brightness(0.45)", transform: "scale(1.15)" }} />
        <AbsoluteFill style={{ justifyContent: "center", alignItems: "center" }}>
          <Img
            src={src}
            style={{
              width: "100%",
              transform: `scale(${scale * 1.18})`,
              borderRadius: 18,
              boxShadow: "0 30px 80px rgba(0,0,0,0.6)",
            }}
          />
        </AbsoluteFill>
      </AbsoluteFill>
    );
  }

  return (
    <AbsoluteFill>
      <OffthreadVideo
        src={staticFile(`media/clips/${shot.clip}.mp4`)}
        trimBefore={sec(shot.from ?? 0)}
        muted
        style={cover}
      />
    </AbsoluteFill>
  );
};
