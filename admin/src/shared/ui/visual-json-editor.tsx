import { createEffect, createMemo, createSignal, Index, Show, on } from "solid-js";
import { Input } from "./input";

export interface JsonGridItem {
  key: string;
  value: string;
  type: "string" | "number" | "boolean";
}

interface VisualJsonEditorProps {
  value: string;
  onChange: (val: string) => void;
  id?: string;
}

interface VisualJsonRowProps {
  item: JsonGridItem;
  index: number;
  isDuplicate: boolean;
  onItemChange: (index: number, field: keyof JsonGridItem, val: string) => void;
  onRemove: (index: number) => void;
}

function VisualJsonRow(props: VisualJsonRowProps) {
  return (
    <div class="flex items-center gap-2 animate-fade-in">
      {/* Key Input */}
      <div class="flex-1 min-w-0">
        <Input
          type="text"
          value={props.item.key}
          onInput={(e) =>
            props.onItemChange(props.index, "key", e.currentTarget.value)
          }
          placeholder="Key"
          class={`h-7 px-2.5 rounded-lg text-xs font-mono bg-surface-container-low ${
            props.isDuplicate
              ? "border-error/40 focus:border-error/60 focus:ring-1 focus:ring-error/20"
              : "border-outline-variant/15"
          }`}
        />
      </div>

      {/* Value Input depending on type */}
      <div class="flex-1 min-w-0">
        <Show when={props.item.type === "boolean"}>
          <select
            value={props.item.value}
            onChange={(e) =>
              props.onItemChange(props.index, "value", e.currentTarget.value)
            }
            class="w-full bg-surface-container-low border border-outline-variant/15 focus:border-primary focus:ring-2 focus:ring-primary/20 rounded-lg px-2.5 py-1.5 text-xs text-on-surface outline-none cursor-pointer"
          >
            <option value="true">True</option>
            <option value="false">False</option>
          </select>
        </Show>

        <Show when={props.item.type === "number"}>
          <Input
            type="number"
            value={props.item.value}
            onInput={(e) =>
              props.onItemChange(props.index, "value", e.currentTarget.value)
            }
            placeholder="0"
            class="h-7 px-2.5 rounded-lg text-xs font-mono bg-surface-container-low border-outline-variant/15"
          />
        </Show>

        <Show when={props.item.type === "string"}>
          <Input
            type="text"
            value={props.item.value}
            onInput={(e) =>
              props.onItemChange(props.index, "value", e.currentTarget.value)
            }
            placeholder="Value"
            class="h-7 px-2.5 rounded-lg text-xs bg-surface-container-low border-outline-variant/15"
          />
        </Show>
      </div>

      {/* Type Selector */}
      <div class="w-20 shrink-0">
        <select
          value={props.item.type}
          onChange={(e) =>
            props.onItemChange(
              props.index,
              "type",
              e.currentTarget.value as JsonGridItem['type'],
            )
          }
          class="w-full bg-surface-container-low border border-outline-variant/15 focus:border-primary focus:ring-2 focus:ring-primary/20 rounded-lg px-1.5 py-1.5 text-[11px] font-mono text-on-surface outline-none cursor-pointer"
        >
          <option value="string">str</option>
          <option value="number">num</option>
          <option value="boolean">bool</option>
        </select>
      </div>

      {/* Delete button */}
      <button
        type="button"
        onClick={() => props.onRemove(props.index)}
        class="shrink-0 text-outline hover:text-error bg-transparent border-0 cursor-pointer p-1 rounded hover:bg-error/10 flex items-center justify-center"
        title="Remove field"
      >
        <span class="material-symbols-outlined text-[16px]">close</span>
      </button>
    </div>
  );
}

export function VisualJsonEditor(props: VisualJsonEditorProps) {
  const [items, setItems] = createSignal<JsonGridItem[]>([]);
  const [isVisual, setIsVisual] = createSignal(true);
  const [isNested, setIsNested] = createSignal(false);
  const [errorMsg, setErrorMsg] = createSignal<string | null>(null);
  let pendingVisualSyncValue: string | null = null;

  // Helper to parse JSON
  const tryParseFlatJson = (jsonStr: string): JsonGridItem[] | null => {
    try {
      const parsed = JSON.parse(jsonStr);
      if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
        const gridItems: JsonGridItem[] = [];
        for (const [k, v] of Object.entries(parsed)) {
          if (typeof v === "object" && v !== null) {
            return null; // Nested object/array
          }
          let t: "string" | "number" | "boolean" = "string";
          if (typeof v === "number") t = "number";
          else if (typeof v === "boolean") t = "boolean";
          gridItems.push({ key: k, value: String(v), type: t });
        }
        return gridItems;
      }
    } catch {}
    return null;
  };

  // Sync value from props to internal state.
  // This must react only to external value changes, not local item edits.
  createEffect(on(() => props.value, rawVal => {
    if (pendingVisualSyncValue !== null) {
      if (rawVal === pendingVisualSyncValue) {
        pendingVisualSyncValue = null;
      }
      return;
    }

    try {
      if (rawVal.trim() === "") {
        setErrorMsg(null);
        setIsNested(false);
        return;
      }
      JSON.parse(rawVal);
      setErrorMsg(null);
    } catch (caught) {
      setErrorMsg(caught instanceof Error && caught.message ? caught.message : "Invalid JSON syntax");
    }

    const parsed = tryParseFlatJson(rawVal);
    if (parsed !== null) {
      setIsNested(false);
      const current = items();
      const hasChanged =
        current.length !== parsed.length ||
        parsed.some(
          (p, i) =>
            !current[i] ||
            current[i].key !== p.key ||
            current[i].value !== p.value ||
            current[i].type !== p.type,
        );

      if (hasChanged) {
        setItems(parsed);
      }
    } else {
      setIsNested(true);
      setIsVisual(false); // Force raw mode if nested
    }
  }));

  // Compile items to JSON string and trigger onChange
  const updateFromItems = (newItems: JsonGridItem[]) => {
    const obj: Record<string, string | number | boolean> = {};
    for (const item of newItems) {
      if (!item.key.trim()) continue;
      if (item.type === "number") {
        const num = Number(item.value);
        obj[item.key] = isNaN(num) ? 0 : num;
      } else if (item.type === "boolean") {
        obj[item.key] = item.value === "true";
      } else {
        obj[item.key] = item.value;
      }
    }
    const nextValue = JSON.stringify(obj, null, 2);
    pendingVisualSyncValue = nextValue;
    setItems(newItems);
    props.onChange(nextValue);
  };

  const handleAddField = () => {
    const current = [...items()];
    current.push({ key: "", value: "", type: "string" });
    updateFromItems(current);
  };

  const handleRemoveField = (index: number) => {
    const current = [...items()];
    current.splice(index, 1);
    updateFromItems(current);
  };

  const handleItemChange = (
    index: number,
    field: keyof JsonGridItem,
    val: string,
  ) => {
    const current = items().map((item, idx) => {
      if (idx === index) {
        const updated = { ...item, [field]: val };
        if (field === "type") {
          if (val === "boolean") updated.value = "true";
          else if (val === "number") updated.value = "0";
          else updated.value = "";
        }
        return updated;
      }
      return item;
    });
    updateFromItems(current);
  };

  const handleRawChange = (e: InputEvent & { currentTarget: HTMLTextAreaElement }) => {
    props.onChange(e.currentTarget.value);
  };

  const formatRawJson = () => {
    try {
      const parsed = JSON.parse(props.value);
      props.onChange(JSON.stringify(parsed, null, 2));
    } catch {}
  };

  // Find duplicate keys in a single-pass O(N) allocation-free check
  const duplicateKeys = createMemo(() => {
    const seen = new Set<string>();
    const dupes = new Set<string>();
    for (const item of items()) {
      const key = item.key.trim();
      if (!key) continue;
      if (seen.has(key)) {
        dupes.add(key);
      }
      seen.add(key);
    }
    return dupes;
  });

  return (
    <div class="space-y-3">
      {/* Mode Selector Toggle */}
      <div class="flex items-center justify-between">
        <div class="flex bg-surface-container-high/60 p-0.5 rounded-lg border border-outline-variant/10">
          <button
            type="button"
            disabled={isNested()}
            onClick={() => setIsVisual(true)}
            class={`px-3 py-1 rounded-md text-[11px] font-bold uppercase tracking-wider transition-all border-0 cursor-pointer ${
              isVisual()
                ? "bg-primary text-on-primary shadow-sm"
                : "text-outline hover:text-on-surface disabled:opacity-40"
            }`}
          >
            Visual Grid
          </button>
          <button
            type="button"
            onClick={() => setIsVisual(false)}
            class={`px-3 py-1 rounded-md text-[11px] font-bold uppercase tracking-wider transition-all border-0 cursor-pointer ${
              !isVisual()
                ? "bg-primary text-on-primary shadow-sm"
                : "text-outline hover:text-on-surface"
            }`}
          >
            Raw Code
          </button>
        </div>

        <Show when={!isVisual()}>
          <div class="flex gap-2">
            <button
              type="button"
              onClick={formatRawJson}
              disabled={!!errorMsg()}
              class="px-2 py-1 rounded border border-outline-variant/20 hover:border-primary/50 text-[10px] font-bold font-mono text-outline hover:text-primary transition-all bg-transparent cursor-pointer"
            >
              Format Code
            </button>
            <span
              class={`px-2 py-0.5 rounded text-[9px] font-bold font-mono uppercase ${
                errorMsg()
                  ? "bg-error/10 border border-error/20 text-error"
                  : "bg-success/10 border border-success/20 text-success"
              }`}
            >
              {errorMsg() ? "Invalid Syntax" : "Valid JSON"}
            </span>
          </div>
        </Show>
      </div>

      {/* Editor Content Area */}
      <Show
        when={isVisual()}
        fallback={
          <div class="relative">
            <textarea
              id={props.id}
              value={props.value}
              onInput={handleRawChange}
              rows={6}
              class="w-full bg-surface-container-low border border-outline-variant/15 focus:border-primary focus:ring-2 focus:ring-primary/20 rounded-lg px-4 py-2.5 text-sm text-on-surface font-mono placeholder:text-outline/60 transition-all outline-none"
              placeholder='{ "key": "value" }'
            />
            <Show when={errorMsg()}>
              <p class="text-xs text-error font-bold mt-1 flex items-center gap-1.5 animate-fade-in">
                <span class="material-symbols-outlined text-[14px]">error</span>
                {errorMsg()}
              </p>
            </Show>
            <Show when={isNested()}>
              <p class="text-[10px] text-outline mt-1.5 leading-relaxed">
                * Visual Grid is disabled because this JSON configuration
                contains nested objects or arrays.
              </p>
            </Show>
          </div>
        }
      >
        <div class="space-y-2 border border-outline-variant/10 rounded-xl p-3 bg-surface-container-low/30">
          <div class="max-h-60 overflow-y-auto space-y-2 pr-1">
            <Show
              when={items().length > 0}
              fallback={
                <div class="text-center py-6 text-xs text-outline font-medium">
                  No key-value pairs. Click 'Add Row' to begin.
                </div>
              }
            >
              <Index each={items()}>
                {(item, index) => (
                  <VisualJsonRow
                    item={item()}
                    index={index}
                    isDuplicate={duplicateKeys().has(item().key.trim())}
                    onItemChange={handleItemChange}
                    onRemove={handleRemoveField}
                  />
                )}
              </Index>
            </Show>
          </div>

          {/* Add field and validations */}
          <div class="flex items-center justify-between pt-1 border-t border-outline-variant/10">
            <button
              type="button"
              onClick={handleAddField}
              data-testid="visual-json-editor-add-item"
              class="flex items-center gap-1 text-[11px] font-bold text-primary hover:text-primary-container bg-transparent border-0 cursor-pointer p-1 rounded select-none"
            >
              <span class="material-symbols-outlined text-[14px]">add</span>
              Add Item
            </button>

            <Show when={duplicateKeys().size > 0}>
              <span class="text-[10px] text-error font-bold flex items-center gap-1 select-none animate-fade-in">
                <span class="material-symbols-outlined text-[12px]">
                  warning
                </span>
                Duplicate keys detected
              </span>
            </Show>
          </div>
        </div>
      </Show>
    </div>
  );
}
