import * as TooltipPrimitive from "@kobalte/core/tooltip";
import { splitProps, type JSX, type ParentProps } from "solid-js";
import { cn } from "../lib/utils";

interface TooltipProps extends ParentProps {
  content: JSX.Element;
  placement?: TooltipPrimitive.TooltipRootProps["placement"];
  openDelay?: number;
}

export function Tooltip(props: TooltipProps) {
  return (
    <TooltipPrimitive.Root
      placement={props.placement ?? "top"}
      openDelay={props.openDelay ?? 350}
      closeDelay={80}
      gutter={7}
    >
      {props.children}
      <TooltipPrimitive.Portal>
        <TooltipPrimitive.Content class="bg-surface-container-highest text-on-surface border-outline-variant/20 z-[200] max-w-80 rounded-lg border px-3 py-2 text-[12px] leading-relaxed shadow-xl motion-safe:animate-fade-in">
          {props.content}
          <TooltipPrimitive.Arrow class="fill-surface-container-highest" />
        </TooltipPrimitive.Content>
      </TooltipPrimitive.Portal>
    </TooltipPrimitive.Root>
  );
}

export const TooltipTrigger = TooltipPrimitive.Trigger;

interface TooltipLabelProps extends JSX.LabelHTMLAttributes<HTMLLabelElement> {
  content: JSX.Element;
  placement?: TooltipPrimitive.TooltipRootProps["placement"];
}

export function TooltipLabel(props: TooltipLabelProps) {
  const [local, others] = splitProps(props, ["content", "placement", "class", "children"]);
  return (
    <Tooltip content={local.content} placement={local.placement}>
      <TooltipTrigger
        as="label"
        tabindex="0"
        data-tooltip-trigger
        class={cn(
          "text-on-surface-variant mb-1.5 block w-fit cursor-help border-b border-dotted border-outline/60 text-[12px] font-medium tracking-[0.05em] focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40",
          local.class,
        )}
        {...others}
      >
        {local.children}
      </TooltipTrigger>
    </Tooltip>
  );
}
