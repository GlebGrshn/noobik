import { AbsoluteFill, interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { Amber, Cream, Display, outlined } from "./theme";
import type { Caption } from "./timeline";

/** Big two-line caption that pops in line by line and fades out just before the next beat. */
export const CaptionView: React.FC<{ caption: Caption; frames: number; portrait: boolean }> = ({
  caption,
  frames,
  portrait,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const size = portrait ? 104 : 108;
  const out = interpolate(frame, [frames - 5, frames], [1, 0], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  return (
    <AbsoluteFill
      style={{
        justifyContent: portrait ? "flex-start" : "flex-end",
        alignItems: "center",
        // Lower middle of a 9:16 frame: clear of the platform's top bar and its bottom buttons.
        paddingTop: portrait ? 1170 : 0,
        paddingBottom: portrait ? 0 : 86,
        opacity: out,
      }}
    >
      {caption.lines.map((line, i) => {
        const pop = spring({ frame: frame - i * 4, fps, config: { damping: 11, stiffness: 190, mass: 0.7 } });
        return (
          <div
            key={i}
            style={{
              fontFamily: Display,
              fontWeight: 900,
              fontSize: size,
              lineHeight: 1.02,
              textTransform: "uppercase",
              textAlign: "center",
              letterSpacing: 1,
              maxWidth: portrait ? 980 : 1700,
              color: i === caption.accent ? Amber : Cream,
              transform: `translateY(${(1 - pop) * 40}px) scale(${0.7 + pop * 0.3}) rotate(${(1 - pop) * -4}deg)`,
              opacity: Math.min(1, pop * 1.5),
              ...outlined(portrait ? 14 : 15),
            }}
          >
            {line}
          </div>
        );
      })}
    </AbsoluteFill>
  );
};
