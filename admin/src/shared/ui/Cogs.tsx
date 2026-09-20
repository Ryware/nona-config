import type { JSX } from "solid-js";

const NINE_TOOTH_PATH =
  "M38.71,18.99 L43,8.59 L57,8.59 L61.29,18.99 L71.25,13.77 L81.98,22.78 L78.58,33.5 L89.57,35.91 L92,49.71 L82.5,55.73 L89.37,64.64 L82.36,76.77 L71.21,75.28 L70.75,86.52 L57.58,91.31 L50,83 L42.42,91.31 L29.25,86.52 L28.79,75.28 L17.64,76.77 L10.63,64.64 L17.5,55.73 L8,49.71 L10.43,35.91 L21.42,33.5 L18.02,22.78 L28.75,13.77 Z M 65.00,50 A 15,15 0 1,0 35.00,50 A 15,15 0 1,0 65.00,50 Z";

const SIX_TOOTH_PATH =
  "M35,24.02 L38.17,7.62 L61.83,7.62 L65,24.02 L80.79,18.56 L92.62,39.06 L80,50 L92.62,60.94 L80.79,81.44 L65,75.98 L61.83,92.38 L38.17,92.38 L35,75.98 L19.21,81.44 L7.38,60.94 L20,50 L7.38,39.06 L19.21,18.56 Z M 68.00,50 A 18,18 0 1,0 32.00,50 A 18,18 0 1,0 68.00,50 Z";

const TWELVE_TOOTH_PATH =
  "M41.46,18.12 L43.74,10.49 L56.26,10.49 L58.54,18.12 L64.33,12.66 L75.17,18.91 L73.33,26.67 L81.09,24.83 L87.34,35.67 L81.88,41.46 L89.51,43.74 L89.51,56.26 L81.88,58.54 L87.34,64.33 L81.09,75.17 L73.33,73.33 L75.17,81.09 L64.33,87.34 L58.54,81.88 L56.26,89.51 L43.74,89.51 L41.46,81.88 L35.67,87.34 L24.83,81.09 L26.67,73.33 L18.91,75.17 L12.66,64.33 L18.12,58.54 L10.49,56.26 L10.49,43.74 L18.12,41.46 L12.66,35.67 L18.91,24.83 L26.67,26.67 L24.83,18.91 L35.67,12.66 Z M 63.00,50 A 13,13 0 1,0 37.00,50 A 13,13 0 1,0 63.00,50 Z";

const TOOTH_PATHS = { 6: SIX_TOOTH_PATH, 9: NINE_TOOTH_PATH, 12: TWELVE_TOOTH_PATH };

type GearProps = {
  teeth?: 6 | 9 | 12;
  size?: number;
  duration?: number;
  reverse?: boolean;
  class?: string;
  style?: JSX.CSSProperties;
};

export function Gear(props: GearProps) {
  const teeth = () => props.teeth ?? 9;
  const size = () => props.size ?? 64;
  const duration = () => props.duration ?? 20;

  return (
    <svg
      viewBox="0 0 100 100"
      width={size()}
      height={size()}
      fill="currentColor"
      class={props.class}
      aria-hidden="true"
      style={{
        animation: `${props.reverse ? "nona-spin-reverse" : "nona-spin"} ${duration()}s linear infinite`,
        "transform-origin": "50% 50%",
        "will-change": "transform",
        ...props.style,
      }}
    >
      <path fill-rule="evenodd" clip-rule="evenodd" d={TOOTH_PATHS[teeth()]} />
    </svg>
  );
}

export function GearPair(props: {
  size?: number;
  nineDuration?: number;
  class?: string;
  style?: JSX.CSSProperties;
}) {
  const size = () => props.size ?? 40;
  const nineDuration = () => props.nineDuration ?? 18;
  const sixSize = () => size() * 0.62;

  return (
    <span class={props.class} style={props.style}>
      <span class="relative inline-block" style={{ width: `${size() * 1.195}px`, height: `${size() * 1.269}px` }}>
        <Gear teeth={9} size={size()} duration={nineDuration()} class="absolute left-0 top-0" />
        <Gear
          teeth={6}
          size={sixSize()}
          duration={nineDuration() * (6 / 9)}
          reverse
          class="absolute"
          style={{ left: `${size() * 0.575}px`, top: `${size() * 0.649}px` }}
        />
      </span>
    </span>
  );
}

export function GearTrio(props: { size?: number; duration?: number; class?: string; style?: JSX.CSSProperties }) {
  const size = () => props.size ?? 48;
  const duration = () => props.duration ?? 26;
  const midSize = () => size() * (9 / 12);
  const smallSize = () => size() * (6 / 12);

  return (
    <span class={props.class} style={props.style}>
      <span class="relative inline-block" style={{ width: `${size() * 1.5}px`, height: `${size() * 1.303}px` }}>
        <Gear teeth={12} size={size()} duration={duration()} class="absolute left-0 top-0" />
        <Gear
          teeth={9}
          size={midSize()}
          duration={duration() * (9 / 12)}
          reverse
          class="absolute"
          style={{ left: `${size() * 0.75}px`, top: `${size() * 0.293}px` }}
        />
        <Gear
          teeth={6}
          size={smallSize()}
          duration={duration() * (6 / 12)}
          class="absolute"
          style={{ left: `${size() * 0.298}px`, top: `${size() * 0.803}px` }}
        />
      </span>
    </span>
  );
}
