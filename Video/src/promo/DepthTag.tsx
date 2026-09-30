import { AbsoluteFill, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { Cream, Display, InkSoft, Mint, Muted } from "./theme";

/** The in-game depth card: a mint arrow, the depth, and the place's name under it. */
export const DepthTag: React.FC<{ metres: number; place: string; portrait: boolean }> = ({ metres, place, portrait }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const slide = spring({ frame, fps, config: { damping: 14, stiffness: 180 } });
  const k = portrait ? 1.15 : 1.3;
  return (
    <AbsoluteFill
      style={{
        justifyContent: "flex-start",
        alignItems: portrait ? "center" : "flex-start",
        padding: portrait ? "250px 0 0 0" : "64px 0 0 72px",
      }}
    >
      <div
        style={{
          display: "flex",
          alignItems: "center",
          gap: 22 * k,
          padding: `${18 * k}px ${34 * k}px ${18 * k}px ${26 * k}px`,
          borderRadius: 24 * k,
          backgroundColor: InkSoft,
          boxShadow: "0 12px 40px rgba(0,0,0,0.45)",
          transform: `translateX(${(1 - slide) * (portrait ? 0 : -120)}px) translateY(${portrait ? (1 - slide) * -60 : 0}px)`,
          opacity: slide,
        }}
      >
        <div style={{ fontFamily: Display, fontWeight: 900, fontSize: 70 * k, color: Mint, lineHeight: 1 }}>↓</div>
        <div>
          <div style={{ fontFamily: Display, fontWeight: 900, fontSize: 62 * k, color: Cream, lineHeight: 1 }}>{metres} м</div>
          <div
            style={{
              fontFamily: Display,
              fontWeight: 700,
              fontSize: 26 * k,
              color: Muted,
              textTransform: "uppercase",
              letterSpacing: 2,
              marginTop: 6,
            }}
          >
            {place}
          </div>
        </div>
      </div>
    </AbsoluteFill>
  );
};
