import { type PolymorphicProps } from "@kobalte/core/polymorphic";
import { cva, type VariantProps } from "class-variance-authority";
import { splitProps, type ValidComponent } from "solid-js";
import { cn } from "../lib/utils";

export const buttonVariants = cva(
  "inline-flex items-center justify-center gap-2 whitespace-nowrap rounded-lg cursor-pointer text-[13px] font-bold tracking-tight transition-all duration-200 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40 disabled:pointer-events-none disabled:opacity-40 active:scale-[0.98]",
  {
    variants: {
      variant: {
        default: "bg-primary text-on-primary shadow-md hover:brightness-105",
        success: "bg-success text-on-success shadow-md hover:brightness-105",
        destructive: "bg-error text-on-error shadow-md hover:brightness-105",
        outline:
          "border border-outline-variant/30 bg-surface-container-low text-on-surface-variant hover:bg-surface-container hover:text-on-surface",
        secondary:
          "bg-surface-container-high text-on-surface-variant hover:bg-surface-bright hover:text-on-surface",
        ghost:
          "text-outline hover:bg-surface-container-low hover:text-on-surface",
        link: "text-primary underline-offset-4 hover:underline p-0 h-auto",
        google: "bg-white text-gray-900 hover:bg-gray-100 shadow-md",
      },
      size: {
        default: "h-11 px-6",
        sm: "h-8 px-3 text-[11px] rounded-md",
        lg: "h-12 px-8 text-[14px]",
        icon: "h-9 w-9",
      },
    },
    defaultVariants: {
      variant: "default",
      size: "default",
    },
  },
);

type ButtonProps<T extends ValidComponent = "button"> = PolymorphicProps<
  T,
  VariantProps<typeof buttonVariants>
>;

export function Button<T extends ValidComponent = "button">(
  props: ButtonProps<T>,
) {
  const [local, others] = splitProps(props as ButtonProps, [
    "variant",
    "size",
    "class",
  ]);
  return (
    <button
      class={cn(
        buttonVariants({ variant: local.variant, size: local.size }),
        local.class,
      )}
      {...others}
    />
  );
}
