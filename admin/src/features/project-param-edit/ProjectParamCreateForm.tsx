import { createSignal, onMount, Show } from "solid-js";
import { Button } from "../../shared/ui/button";
import { Input } from "../../shared/ui/input";
import { Label } from "../../shared/ui/label";
import { Select } from "../../shared/ui/select";
import { VisualJsonEditor } from "../../shared/ui/visual-json-editor";
import type { ConfigEntry } from "../../types";
import { FormField } from "../../widgets/auth-shell/FormField";
import { MIcon } from "../../shared/ui/icons";

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

export function isValidConfigEntryValue(contentType: ConfigEntryContentType, value: string): boolean {
  const trimmed = value.trim();

  if (trimmed.length === 0) {
    return false;
  }

  if (contentType === "text") {
    return true;
  }

  try {
    const parsed = JSON.parse(trimmed) as unknown;

    if (contentType === "json") {
      return true;
    }

    if (contentType === "number") {
      return typeof parsed === "number" && Number.isFinite(parsed);
    }

    return typeof parsed === "boolean";
  } catch {
    return false;
  }
}

export function ProjectParamCreateForm(props: ProjectParamCreateFormProps) {
  const [cfgKey, setCfgKey] = createSignal("");
  const [cfgValue, setCfgValue] = createSignal("");
  const [cfgType, setCfgType] = createSignal<ConfigEntryContentType>("text");
  const [cfgScope, setCfgScope] = createSignal<ConfigEntryScope>("all");
  const [cfgDescription, setCfgDescription] = createSignal("");
  const [createError, setCreateError] = createSignal("");
  let keyInputRef: HTMLInputElement | undefined;

  onMount(() => {
    keyInputRef?.focus();
  });

  const onKeyDownConfigKey = (e: KeyboardEvent) => {
    if (e.key === " ") {
      e.preventDefault();
    }
  };

  const isAddInvalid = () => !isValidConfigEntryValue(cfgType(), cfgValue());

  const handleSubmit = (e: Event) => {
    e.preventDefault();
    const trimmedKey = cfgKey().trim();
    if (!trimmedKey) {
      setCreateError("Parameter key is required.");
      return;
    }
    if (props.existingEntries.some(entry => entry.key === trimmedKey)) {
      setCreateError("Parameter key already exists.");
      return;
    }
    setCreateError("");

    props.onSubmit({
      key: trimmedKey,
      value: cfgValue().trim(),
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
                if (createError()) setCreateError("");
              }}
              required
              leftIcon="code"
              testId="parameter-key-input"
              inputRef={element => {
                keyInputRef = element;
              }}
            />
            <Show when={createError()}>
              <p class="text-error mt-2 text-[11px] font-bold">{createError()}</p>
            </Show>
          </div>
          <div>
            <Label>Datatype</Label>
            <Select
              value={cfgType()}
              onChange={(val: string) => {
                setCfgType(val as ConfigEntryContentType);
                setCfgValue("");
              }}
              options={["text", "number", "boolean", "json"]}
            />
          </div>
          <div>
            <Label>Scope</Label>
            <Select
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
            value={cfgValue()}
            onChange={setCfgValue}
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
            onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
              setCfgValue(e.currentTarget.value)
            }
            required
            placeholder="0"
          />
        </Show>
        <Show when={cfgType() === "json"}>
          <VisualJsonEditor id="config-entry-value" value={cfgValue()} onChange={setCfgValue} />
        </Show>
        <Show when={cfgType() === "text"}>
          <Input
            data-testid="parameter-value-input"
            id="config-entry-value"
            type="text"
            value={cfgValue()}
            onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
              setCfgValue(e.currentTarget.value)
            }
            required
            placeholder="Enter configuration value"
          />
        </Show>
      </div>

      <div class="flex justify-end gap-3 pt-2">
        <Button
          data-testid="parameter-create-submit-button"
          type="submit"
          disabled={props.isPending || isAddInvalid()}
        >
          <MIcon name="add" class="text-[17px]" />
           {props.isPending ? "Creating…" : "Create"}
        </Button>
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
    </form>
  );
}
