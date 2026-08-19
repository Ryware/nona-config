import { createMemo, createSignal, onMount, Show } from "solid-js";
import { Button } from "../../shared/ui/button";
import { Input } from "../../shared/ui/input";
import { Label } from "../../shared/ui/label";
import { Select } from "../../shared/ui/select";
import { VisualJsonEditor } from "../../shared/ui/visual-json-editor";
import type { ConfigEntry } from "../../types";
import { FormField } from "../../widgets/auth-shell/FormField";
import { MIcon } from "../../shared/ui/icons";
import { validateConfigEntryDraft } from "./config-entry-value";
import { Tooltip, TooltipLabel, TooltipTrigger } from "../../shared/ui/tooltip";
import { tooltipCopy } from "../../shared/lib/tooltip-copy";

type ConfigEntryContentType = "text" | "number" | "boolean" | "json";
type ConfigEntryScope = "client" | "server" | "all";

interface ProjectParamCreateFormProps {
  onCancel: () => void;
  onSubmit: (data: {
    key: string;
    value: string;
    contentType: ConfigEntryContentType;
    scope: ConfigEntryScope;
    description: string;
  }) => void;
  isPending: boolean;
  existingEntries: ConfigEntry[];
}

export function ProjectParamCreateForm(props: ProjectParamCreateFormProps) {
  const [cfgKey, setCfgKey] = createSignal("");
  const [cfgValue, setCfgValue] = createSignal("");
  const [cfgType, setCfgType] = createSignal<ConfigEntryContentType>("text");
  const [cfgScope, setCfgScope] = createSignal<ConfigEntryScope>("all");
  const [cfgDescription, setCfgDescription] = createSignal("");
  const [createError, setCreateError] = createSignal("");
  const [keyTouched, setKeyTouched] = createSignal(false);
  const [valueTouched, setValueTouched] = createSignal(false);
  let keyInputRef: HTMLInputElement | undefined;

  const keyErrorId = "config-entry-key-error";
  const valueErrorId = "config-entry-value-error";
  const actionStatusId = "config-entry-create-status";

  onMount(() => {
    keyInputRef?.focus();
  });

  const onKeyDownConfigKey = (e: KeyboardEvent) => {
    if (e.key === " ") {
      e.preventDefault();
    }
  };

  const validation = createMemo(() =>
    validateConfigEntryDraft({
      key: cfgKey(),
      value: cfgValue(),
      contentType: cfgType(),
      existingKeys: props.existingEntries.map((entry) => entry.key),
    }),
  );

  const keyError = () => (keyTouched() ? validation().keyError : undefined);
  const valueError = () => (valueTouched() ? validation().valueError : undefined);
  const isActionDisabled = () => props.isPending || !validation().isValid;
  const actionStatus = () => {
    if (props.isPending) return "Creating the parameter…";
    return validation().disabledReason ?? "Parameter is ready to create.";
  };

  const handleSubmit = (e: Event) => {
    e.preventDefault();
    setKeyTouched(true);
    setValueTouched(true);
    if (!validation().isValid) return;

    const trimmedKey = cfgKey().trim();
    setCreateError("");

    props.onSubmit({
      key: trimmedKey,
      value: cfgValue(),
      contentType: cfgType(),
      scope: cfgScope(),
      description: cfgDescription().trim()
    });
  };

  return (
    <form
      data-testid="parameter-create-form"
      onSubmit={handleSubmit}
      class="bg-surface-container-low border-outline-variant/15 animate-fade-in mb-4 space-y-4 rounded-2xl border p-6"
    >
      <div class="grid gap-4 md:grid-cols-2">
        <div class="space-y-4">
          <div>
            <FormField
              id="config-entry-key"
              label="Key"
              type="text"
              placeholder="CONFIG_KEY"
              value={cfgKey()}
              onKeyDown={onKeyDownConfigKey}
              onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) => {
                setCfgKey(e.currentTarget.value);
                setKeyTouched(true);
                if (createError()) setCreateError("");
              }}
              onBlur={() => setKeyTouched(true)}
              required
              aria-invalid={!!keyError()}
              aria-describedby={keyError() ? keyErrorId : undefined}
              leftIcon="code"
              testId="parameter-key-input"
              inputRef={element => {
                keyInputRef = element;
              }}
            />
            <Show when={keyError()}>
              <p id={keyErrorId} class="text-error mt-2 text-[11px] font-bold">
                {keyError()}
              </p>
            </Show>
          </div>
          <div>
            <TooltipLabel for="config-entry-type" content={tooltipCopy.datatype}>
              Datatype
            </TooltipLabel>
            <Select
              id="config-entry-type"
              aria-label="Datatype"
              value={cfgType()}
              onChange={(val: string) => {
                setCfgType(val as ConfigEntryContentType);
                setCfgValue("");
                setValueTouched(true);
              }}
              options={["text", "number", "boolean", "json"]}
            />
          </div>
          <div>
            <TooltipLabel for="config-entry-scope" content={tooltipCopy.scope}>
              Scope
            </TooltipLabel>
            <Select
              id="config-entry-scope"
              aria-label="Scope"
              value={cfgScope()}
              onChange={(val: string) => setCfgScope(val as ConfigEntryScope)}
              options={[
                { value: "all", label: "All" },
                { value: "client", label: "Client" },
                { value: "server", label: "Server" }
              ]}
            />
          </div>
        </div>
        <div class="space-y-4">
          <div>
            <FormField
              id="config-entry-description"
              label="Description"
              type="text"
              placeholder="Explain what this configuration does..."
              value={cfgDescription()}
              onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
                setCfgDescription(e.currentTarget.value)
              }
              maxLength={500}
              testId="parameter-description-input"
            />
          </div>
        </div>
      </div>

      <div>
        <Label for="config-entry-value">Value</Label>
        <Show when={cfgType() === "boolean"}>
          <Select
            id="config-entry-value"
            aria-label="Value"
            aria-invalid={!!valueError()}
            aria-describedby={valueError() ? valueErrorId : undefined}
            value={cfgValue()}
            onChange={(value) => {
              setCfgValue(value);
              setValueTouched(true);
            }}
            onBlur={() => setValueTouched(true)}
            placeholder="Select status..."
            options={[
              { value: "true", label: "True / Active" },
              { value: "false", label: "False / Inactive" }
            ]}
          />
        </Show>
        <Show when={cfgType() === "number"}>
          <Input
            data-testid="parameter-value-input"
            id="config-entry-value"
            type="number"
            value={cfgValue()}
            onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) => {
              setCfgValue(e.currentTarget.value);
              setValueTouched(true);
            }}
            onBlur={() => setValueTouched(true)}
            aria-invalid={!!valueError()}
            aria-describedby={valueError() ? valueErrorId : undefined}
            placeholder="0"
          />
        </Show>
        <Show when={cfgType() === "json"}>
          <VisualJsonEditor
            id="config-entry-value"
            aria-label="Value"
            aria-invalid={!!valueError()}
            aria-describedby={valueError() ? valueErrorId : undefined}
            value={cfgValue()}
            onChange={(value) => {
              setCfgValue(value);
              setValueTouched(true);
            }}
          />
        </Show>
        <Show when={cfgType() === "text"}>
          <Input
            data-testid="parameter-value-input"
            id="config-entry-value"
            type="text"
            value={cfgValue()}
            onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) => {
              setCfgValue(e.currentTarget.value);
              setValueTouched(true);
            }}
            onBlur={() => setValueTouched(true)}
            aria-invalid={!!valueError()}
            aria-describedby={valueError() ? valueErrorId : undefined}
            placeholder="Enter configuration value"
          />
        </Show>
        <Show when={valueError()}>
          <p id={valueErrorId} class="text-error mt-2 text-[11px] font-bold">
            {valueError()}
          </p>
        </Show>
        <Show when={createError()}>
          <p role="alert" class="text-error mt-2 text-[11px] font-bold">
            {createError()}
          </p>
        </Show>
      </div>

      <div class="flex flex-col gap-3 pt-2 sm:flex-row sm:items-center sm:justify-between">
        <p
          id={actionStatusId}
          role="status"
          aria-live="polite"
          class="text-on-surface-variant text-[11px]"
        >
          {actionStatus()}
        </p>
        <div class="flex justify-end gap-3">
          <Show
            when={isActionDisabled()}
            fallback={
              <Button data-testid="parameter-create-submit-button" type="submit">
                <MIcon name="add" class="text-[17px]" />
                Create
              </Button>
            }
          >
            <Tooltip content={actionStatus()}>
              <TooltipTrigger
                as="span"
                tabindex="0"
                data-tooltip-trigger
                class="inline-flex rounded-lg focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-primary/40"
              >
                <Button
                  data-testid="parameter-create-submit-button"
                  type="submit"
                  disabled
                  aria-describedby={actionStatusId}
                >
                  <MIcon name="add" class="text-[17px]" />
                  {props.isPending ? "Creating…" : "Create"}
                </Button>
              </TooltipTrigger>
            </Tooltip>
          </Show>
          <Button
            data-testid="parameter-create-cancel-button"
            type="button"
            variant="outline"
            onClick={() => props.onCancel()}
          >
            <MIcon name="close" class="text-[16px]" />
            Cancel
          </Button>
        </div>
      </div>
    </form>
  );
}
