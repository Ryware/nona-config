import { createEffect, type JSXElement, Show } from "solid-js";
import { createSignal } from "solid-js";
import { Portal } from "solid-js/web";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import { TooltipLabel } from "../../shared/ui/tooltip";
import { tooltipCopy } from "../../shared/lib/tooltip-copy";
import { Input } from "../../shared/ui/input";
import { parseReleaseVersion, type ReleaseVersionFormat } from "./release-version";

interface ReleaseVersionDialogProps {
  open: boolean;
  title: string;
  description?: JSXElement;
  initialVersion: string;
  /** Existing release versions, used to prevent collisions. */
  existingVersions: string[];
  confirmLabel: string;
  placeholder?: string;
  validationMessage?: string;
  versionFormat?: ReleaseVersionFormat;
  isPending?: boolean;
  onConfirm: (version: string) => void;
  onCancel: () => void;
}

export function ReleaseVersionDialog(props: ReleaseVersionDialogProps) {
  const [version, setVersion] = createSignal(props.initialVersion);
  const [error, setError] = createSignal("");

  // Reset the field whenever the dialog opens or its seed version changes.
  createEffect(() => {
    if (props.open) {
      setVersion(props.initialVersion);
      setError("");
    }
  });

  const submit = () => {
    const normalizedVersion = parseReleaseVersion(version(), props.versionFormat);

    if (normalizedVersion === null) {
      setError(props.validationMessage ?? "Use major.minor.patch.");
      return;
    }

    if (
      props.existingVersions.some(
        existingVersion => parseReleaseVersion(existingVersion) === normalizedVersion
      )
    ) {
      setError("That version already exists.");
      return;
    }
    setError("");
    props.onConfirm(normalizedVersion);
  };

  return (
    <Show when={props.open}>
      <Portal>
        <div
          data-testid="release-version-dialog"
          class="animate-backdrop-in fixed inset-0 z-80 flex items-center justify-center bg-black/60 p-4 backdrop-blur-sm"
          role="dialog"
          aria-modal="true"
          aria-labelledby="release-version-dialog-title"
          onClick={e => {
            if (e.target === e.currentTarget && !props.isPending) props.onCancel();
          }}
        >
          <div class="bg-surface-container-low border-outline-variant/15 animate-palette-in w-full max-w-md rounded-2xl border p-8 shadow-2xl">
            <h3
              id="release-version-dialog-title"
              class="font-headline text-on-surface mb-2 text-base font-bold"
            >
              {props.title}
            </h3>
            <Show when={props.description}>
              <p class="text-on-surface-variant mb-5 text-[15px] leading-relaxed">{props.description}</p>
            </Show>

            <div>
              <TooltipLabel for="release-version-input" content={tooltipCopy.version}>Version</TooltipLabel>
              <Input
                data-testid="release-version-input"
                id="release-version-input"
                autofocus
                value={version()}
                onInput={event => {
                  setVersion(event.currentTarget.value);
                  if (error()) setError("");
                }}
                onKeyDown={(event: KeyboardEvent) => {
                  if (event.key === "Enter" && !props.isPending) submit();
                }}
                placeholder={props.placeholder ?? "1.0.0"}
                class="font-mono"
              />
              <Show when={error()}>
                <p class="text-error mt-2 text-[12px] font-bold">{error()}</p>
              </Show>
            </div>

            <div class="mt-6 flex justify-end gap-3">
              <Button
                data-testid="release-version-confirm-button"
                type="button"
                onClick={submit}
                disabled={props.isPending}
              >
                <MIcon name="arrow_forward" class="text-[16px]" />
                {props.isPending ? "Please wait…" : props.confirmLabel}
              </Button>
              <Button
                data-testid="release-version-cancel-button"
                type="button"
                variant="outline"
                onClick={() => props.onCancel()}
                disabled={props.isPending}
              >
                <MIcon name="close" class="text-[16px]" />
                Cancel
              </Button>
            </div>
          </div>
        </div>
      </Portal>
    </Show>
  );
}
