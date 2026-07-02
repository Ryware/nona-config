import { createSignal, Show } from "solid-js";
import { Input } from "../../shared/ui/input";
import { Select } from "../../shared/ui/select";
import { VisualJsonEditor } from "../../shared/ui/visual-json-editor";
import { FormField } from "../../widgets/auth-shell/FormField";

type ConfigEntryContentType = "text" | "number" | "boolean" | "json";
type ConfigEntryScope = "client" | "server" | "all";

interface ProjectParamCreateFormProps {
  onCancel: () => void;
  onSubmit: (data: {
    key: string;
    value: string;
    contentType: ConfigEntryContentType;
    scope: ConfigEntryScope;
    displayName: string;
    description: string;
  }) => void;
  isPending: boolean;
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
  const [cfgDisplayName, setCfgDisplayName] = createSignal("");
  const [cfgDescription, setCfgDescription] = createSignal("");

  const onKeyDownConfigKey = (e: KeyboardEvent) => {
    if (e.key === " ") {
      e.preventDefault();
    }
  };

  const isAddInvalid = () => !isValidConfigEntryValue(cfgType(), cfgValue());

  const handleSubmit = (e: Event) => {
    e.preventDefault();
    if (!cfgKey().trim()) return;

    props.onSubmit({
      key: cfgKey().trim(),
      value: cfgValue().trim(),
      contentType: cfgType(),
      scope: "all",
      displayName: cfgDisplayName().trim(),
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
              onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
                setCfgKey(e.currentTarget.value)
              }
              required
              autofocus
              leftIcon="code"
              testId="parameter-key-input"
            />
          </div>
          <div>
            <label class="text-on-surface-variant mb-1.5 block text-[11px] font-medium tracking-[0.05em]">
              Datatype
            </label>
            <Select
              value={cfgType()}
              onChange={(val: string) => {
                setCfgType(val as ConfigEntryContentType);
                setCfgValue("");
              }}
              options={["text", "number", "boolean", "json"]}
            />
          </div>
        </div>
        <div class="space-y-4">
          <div>
            <FormField
              id="config-entry-display-name"
              label="Friendly Display Name"
              type="text"
              placeholder="e.g. Mail Server Port"
              value={cfgDisplayName()}
              onInput={(e: InputEvent & { currentTarget: HTMLInputElement }) =>
                setCfgDisplayName(e.currentTarget.value)
              }
              testId="parameter-display-name-input"
            />
          </div>
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
        <label
          for="config-entry-value"
          class="text-on-surface-variant mb-1.5 block text-[11px] font-medium tracking-[0.05em]"
        >
          Value
        </label>
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
        <button
          data-testid="parameter-create-cancel-button"
          type="button"
          onClick={() => props.onCancel()}
          class="text-on-surface-variant bg-surface-container-high hover:bg-surface-bright cursor-pointer rounded-lg border-0 px-4 py-2.5 text-[13px] font-semibold transition-all"
        >
          Cancel
        </button>
        <button
          data-testid="parameter-create-submit-button"
          type="submit"
          disabled={props.isPending || isAddInvalid()}
          class="bg-primary text-on-primary cursor-pointer rounded-lg border-0 px-4 py-2.5 text-[13px] font-semibold transition-all hover:brightness-105 disabled:opacity-50"
        >
          {props.isPending ? "Creating…" : "Add Parameter"}
        </button>
      </div>
    </form>
  );
}
