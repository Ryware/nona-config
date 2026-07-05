import { type JSX, type ParentComponent } from "solid-js";
import { cn } from "../lib/utils";

export const Card: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (
  props,
) => {
  return (
    <div
      class={cn(
        "rounded-2xl bg-surface-container-low text-on-surface border border-outline-variant/15",
        props.class,
      )}
      {...props}
    />
  );
};

export const CardHeader: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (
  props,
) => {
  return (
    <div class={cn("flex flex-col space-y-1.5 p-6", props.class)} {...props} />
  );
};

export const CardTitle: ParentComponent<
  JSX.HTMLAttributes<HTMLHeadingElement>
> = (props) => {
  return (
    <h3
      class={cn(
        "font-bold leading-none tracking-tight text-on-surface text-lg font-headline",
        props.class,
      )}
      {...props}
    />
  );
};

export const CardDescription: ParentComponent<
  JSX.HTMLAttributes<HTMLParagraphElement>
> = (props) => {
  return (
    <p
      class={cn("text-[13px] text-on-surface-variant", props.class)}
      {...props}
    />
  );
};

export const CardContent: ParentComponent<
  JSX.HTMLAttributes<HTMLDivElement>
> = (props) => {
  return <div class={cn("p-6 pt-0", props.class)} {...props} />;
};

export const CardFooter: ParentComponent<JSX.HTMLAttributes<HTMLDivElement>> = (
  props,
) => {
  return (
    <div class={cn("flex items-center p-6 pt-0", props.class)} {...props} />
  );
};
