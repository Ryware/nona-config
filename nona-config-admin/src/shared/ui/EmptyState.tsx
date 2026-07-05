import { type Component } from "solid-js";
import { Card, CardContent } from "../ui/card";
import { Button } from "../ui/button";

interface EmptyStateProps {
  title: string;
  description?: string;
  actionLabel?: string;
  onAction?: () => void;
  icon?: string;
}

export const EmptyState: Component<EmptyStateProps> = (props) => {
  return (
    <Card class="border-dashed">
      <CardContent class="py-12">
        <div class="text-center">
          {props.icon && (
            <div class="mb-4 text-4xl">{props.icon}</div>
          )}
          <h3 class="text-lg font-medium text-on-surface mb-2">{props.title}</h3>
          {props.description && (
            <p class="text-on-surface-variant mb-4">{props.description}</p>
          )}
          {props.actionLabel && props.onAction && (
            <Button onClick={props.onAction}>
              {props.actionLabel}
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
};
