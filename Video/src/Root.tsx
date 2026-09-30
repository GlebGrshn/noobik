import "./index.css";
import { Composition } from "remotion";
import { Promo, type PromoProps } from "./promo/Promo";
import { beat, FPS } from "./promo/theme";
import { Full, Short } from "./promo/timeline";

// Ad cuts of the game trailer: 30 s and 15 s, landscape 16:9 and vertical 9:16. A few frames under the limit:
// AAC adds padding at the end, and ad platforms reject a "30 s" file that plays 30.06 s.
const FULL = Math.min(896, beat(Full.beats)), SHORT = Math.min(446, beat(Short.beats));
const formats: { id: string; props: PromoProps; frames: number }[] = [
  { id: "Promo30-Landscape", props: { cut: "full", portrait: false }, frames: FULL },
  { id: "Promo30-Portrait", props: { cut: "full", portrait: true }, frames: FULL },
  { id: "Promo15-Landscape", props: { cut: "short", portrait: false }, frames: SHORT },
  { id: "Promo15-Portrait", props: { cut: "short", portrait: true }, frames: SHORT },
];

export const RemotionRoot: React.FC = () => (
  <>
    {formats.map(({ id, props, frames }) => (
      <Composition
        key={id}
        id={id}
        component={Promo}
        durationInFrames={frames}
        fps={FPS}
        width={props.portrait ? 1080 : 1920}
        height={props.portrait ? 1920 : 1080}
        defaultProps={props}
      />
    ))}
  </>
);
