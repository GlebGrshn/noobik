import { AbsoluteFill, interpolate, OffthreadVideo, spring, staticFile, useCurrentFrame, useVideoConfig } from "remotion";
import { Amber, Cream, Display, Ink, Mint, outlined, sec } from "./theme";

/** Closing card over the house at dawn after the victory: the title and the call to play. */
export const EndCard: React.FC<{ portrait: boolean }> = ({ portrait }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const title = spring({ frame, fps, config: { damping: 12, stiffness: 160 } });
  const second = spring({ frame: frame - 5, fps, config: { damping: 12, stiffness: 160 } });
  const tagline = spring({ frame: frame - 10, fps, config: { damping: 16 } });
  const cta = spring({ frame: frame - 14, fps, config: { damping: 9, stiffness: 170 } });
  const pulse = 1 + Math.max(0, Math.sin((frame - 24) / 7)) * 0.045 * (frame > 24 ? 1 : 0);
  const size = portrait ? 150 : 176;
  const fadeIn = interpolate(frame, [0, 6], [0, 1], { extrapolateRight: "clamp" });

  return (
    <AbsoluteFill style={{ backgroundColor: Ink }}>
      <AbsoluteFill style={{ opacity: fadeIn }}>
        <OffthreadVideo
          src={staticFile("media/clips/08_victory.mp4")}
          trimBefore={sec(5.3)}
          muted
          style={{
            width: "100%",
            height: "100%",
            objectFit: "cover",
            filter: "blur(10px) brightness(0.5) saturate(1.1)",
            transform: `scale(${1.12 + frame * 0.0008})`,
          }}
        />
        <AbsoluteFill style={{ background: "radial-gradient(ellipse at 50% 45%, rgba(17,32,36,0.1), rgba(17,32,36,0.75) 75%)" }} />
      </AbsoluteFill>
      <AbsoluteFill style={{ justifyContent: "center", alignItems: "center", gap: portrait ? 26 : 18 }}>
        <div style={{ textAlign: "center", lineHeight: 0.95 }}>
          <div
            style={{
              fontFamily: Display,
              fontWeight: 900,
              fontSize: size,
              color: Cream,
              transform: `scale(${0.6 + title * 0.4})`,
              opacity: title,
              ...outlined(16),
            }}
          >
            НУБИК
          </div>
          <div
            style={{
              fontFamily: Display,
              fontWeight: 900,
              fontSize: size,
              color: Amber,
              transform: `scale(${0.6 + second * 0.4})`,
              opacity: second,
              ...outlined(16),
            }}
          >
            ШАХТЁР
          </div>
        </div>
        <div
          style={{
            fontFamily: Display,
            fontWeight: 800,
            fontSize: portrait ? 50 : 52,
            letterSpacing: 12,
            color: Mint,
            opacity: tagline,
            transform: `translateY(${(1 - tagline) * 20}px)`,
            ...outlined(8),
          }}
        >
          КОПАЙ ГЛУБЖЕ
        </div>
        <div
          style={{
            marginTop: portrait ? 60 : 34,
            padding: portrait ? "30px 70px" : "26px 72px",
            borderRadius: 999,
            backgroundColor: Amber,
            color: Ink,
            fontFamily: Display,
            fontWeight: 900,
            fontSize: portrait ? 60 : 62,
            letterSpacing: 2,
            boxShadow: "0 18px 50px rgba(0,0,0,0.5), inset 0 -8px 0 rgba(0,0,0,0.15)",
            transform: `scale(${(0.5 + cta * 0.5) * pulse})`,
            opacity: Math.min(1, cta * 1.4),
          }}
        >
          ИГРАТЬ БЕСПЛАТНО
        </div>
        <div
          style={{
            fontFamily: Display,
            fontWeight: 700,
            fontSize: portrait ? 34 : 32,
            color: Cream,
            opacity: 0.85 * cta,
            marginTop: 8,
            ...outlined(5),
          }}
        >
          В браузере · на телефоне и компьютере
        </div>
      </AbsoluteFill>
    </AbsoluteFill>
  );
};
