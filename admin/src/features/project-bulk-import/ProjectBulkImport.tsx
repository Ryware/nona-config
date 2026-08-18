import { Content, Item, Portal, Root, Trigger } from "@kobalte/core/dropdown-menu";
import { createSignal, For, Show } from "solid-js";
import { Button } from "../../shared/ui/button";
import { MIcon } from "../../shared/ui/icons";
import type { ConfigEntry } from "../../types";

const CSV_IMPORT_EXAMPLE = `key,value,contentType,scope
WELCOME_MESSAGE,"Hello world",text,client
FEATURE_ENABLED,true,boolean,all`;

const JSON_IMPORT_EXAMPLE = `[
  {
    "key": "WELCOME_MESSAGE",
    "value": "Hello world",
    "contentType": "text",
    "scope": "client"
  },
  {
    "key": "FEATURE_ENABLED",
    "value": "true",
    "contentType": "boolean",
    "scope": "all"
  }
]`;

type ImportExampleFormat = "csv" | "json";

export interface ParsedImport {
  key: string;
  value: string;
  contentType: 'text' | 'number' | 'boolean' | 'json';
  scope: 'client' | 'server' | 'all';
  alreadyExists: boolean;
  selected: boolean;
}

interface ProjectBulkImportProps {
  onCancel: () => void;
  onImport: (items: ParsedImport[]) => Promise<void>;
  existingEntries: ConfigEntry[];
  isPending: boolean;
  addToast: (msg: string, type: "success" | "error" | "info") => void;
}

const isRecord = (value: unknown): value is Record<string, unknown> =>
  typeof value === "object" && value !== null;

const errorMessage = (caught: unknown, fallback: string) =>
  caught instanceof Error && caught.message ? caught.message : fallback;

export function ProjectBulkImport(props: ProjectBulkImportProps) {
  const [dragActive, setDragActive] = createSignal(false);
  const [parsedImports, setParsedImports] = createSignal<ParsedImport[]>([]);
  const [importLoading, setImportLoading] = createSignal(false);
  const [selectedExample, setSelectedExample] = createSignal<ImportExampleFormat | null>(null);

  const selectedExampleText = () =>
    selectedExample() === "csv" ? CSV_IMPORT_EXAMPLE : JSON_IMPORT_EXAMPLE;

  function parseCSV(text: string): Omit<ParsedImport, "alreadyExists" | "selected">[] {
    const lines = text.split(/\r?\n/);
    if (lines.length === 0) return [];
    
    const firstLine = lines[0].toLowerCase();
    const hasHeader = firstLine.includes("key") && firstLine.includes("value");
    const startIndex = hasHeader ? 1 : 0;
    
    const results: Omit<ParsedImport, "alreadyExists" | "selected">[] = [];
    for (let i = startIndex; i < lines.length; i++) {
      const line = lines[i].trim();
      if (!line) continue;
      
      const matches = line.match(/(".*?"|[^",\s]+)(?=\s*,|\s*$)/g) || line.split(",");
      if (matches.length < 2) continue;
      
      const rawKey = matches[0].replace(/^"|"$/g, "").trim();
      const rawVal = matches[1].replace(/^"|"$/g, "").trim();
      const rawType = matches[2]?.replace(/^"|"$/g, "").trim().toLowerCase() ?? "text";
      const rawScope = matches[3]?.replace(/^"|"$/g, "").trim().toLowerCase() ?? "all";
      
      const normalizedRawType = rawType === "string" ? "text" : rawType;
      const cleanType: ConfigEntry['contentType'] = (["text", "number", "boolean", "json"] as const).includes(normalizedRawType as ConfigEntry['contentType']) ? normalizedRawType as ConfigEntry['contentType'] : "text";
      const cleanScope: ConfigEntry['scope'] = (["client", "server", "all"] as const).includes(rawScope as ConfigEntry['scope']) ? rawScope as ConfigEntry['scope'] : "all";
      
      results.push({
        key: rawKey,
        value: rawVal,
        contentType: cleanType,
        scope: cleanScope,
      });
    }
    return results;
  }

  function parseJSONImport(text: string): Omit<ParsedImport, "alreadyExists" | "selected">[] {
    const obj = JSON.parse(text) as unknown;
    const results: Omit<ParsedImport, "alreadyExists" | "selected">[] = [];
    
    if (Array.isArray(obj)) {
      for (const item of obj) {
        if (isRecord(item) && item.key) {
          const contentType =
            typeof item.contentType === "string" &&
            ["text", "number", "boolean", "json"].includes(item.contentType)
              ? item.contentType
              : "text";
          const scope =
            typeof item.scope === "string" && ["client", "server", "all"].includes(item.scope)
              ? item.scope
              : "all";
          results.push({
            key: String(item.key).trim(),
            value: String(item.value ?? "").trim(),
            contentType: contentType as ParsedImport["contentType"],
            scope: scope as ParsedImport["scope"],
          });
        }
      }
    } else if (isRecord(obj)) {
      for (const [k, v] of Object.entries(obj)) {
        let t: 'text' | 'number' | 'boolean' | 'json' = "text";
        let valStr = "";
        if (typeof v === "object" && v !== null) {
          t = "json";
          valStr = JSON.stringify(v, null, 2);
        } else {
          if (typeof v === "number") t = "number";
          else if (typeof v === "boolean") t = "boolean";
          valStr = String(v);
        }
        results.push({
          key: k.trim(),
          value: valStr,
          contentType: t,
          scope: "all",
        });
      }
    }
    return results;
  }

  const handleFileUpload = (text: string, fileName: string) => {
    try {
      let parsed: Omit<ParsedImport, "alreadyExists" | "selected">[] = [];
      if (fileName.endsWith(".csv")) {
        parsed = parseCSV(text);
      } else {
        parsed = parseJSONImport(text);
      }

      const existingKeys = new Set(props.existingEntries.map(entry => entry.key));
      const finalItems = parsed.map(item => ({
        ...item,
        alreadyExists: existingKeys.has(item.key),
        selected: true
      }));

      setParsedImports(finalItems);
      props.addToast(`Parsed ${finalItems.length} parameters successfully`, "success");
    } catch (caught) {
      props.addToast(`Failed to parse file: ${errorMessage(caught, "Invalid format")}`, "error");
    }
  };

  const handleDrag = (e: DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    if (e.type === "dragenter" || e.type === "dragover") {
      setDragActive(true);
    } else if (e.type === "dragleave") {
      setDragActive(false);
    }
  };

  const handleDrop = (e: DragEvent) => {
    e.preventDefault();
    e.stopPropagation();
    setDragActive(false);
    if (e.dataTransfer?.files && e.dataTransfer.files[0]) {
      const file = e.dataTransfer.files[0];
      const reader = new FileReader();
      reader.onload = (event) => {
        if (event.target?.result) {
          handleFileUpload(event.target.result as string, file.name);
        }
      };
      reader.readAsText(file);
    }
  };

  const handleFileChange = (e: Event & { currentTarget: HTMLInputElement }) => {
    if (e.currentTarget.files && e.currentTarget.files[0]) {
      const file = e.currentTarget.files[0];
      const reader = new FileReader();
      reader.onload = (event) => {
        if (event.target?.result) {
          handleFileUpload(event.target.result as string, file.name);
        }
      };
      reader.readAsText(file);
    }
  };

  const executeImport = async () => {
    setImportLoading(true);
    const selected = parsedImports().filter(p => p.selected);
    if (selected.length === 0) {
      props.addToast("No parameters selected to import", "error");
      setImportLoading(false);
      return;
    }

    try {
      await props.onImport(selected);
      setParsedImports([]);
    } catch (caught) {
      props.addToast(`Failed to import: ${errorMessage(caught, "API error")}`, "error");
    } finally {
      setImportLoading(false);
    }
  };

  return (
      <div class="bg-surface-container-low rounded-2xl p-6 border border-outline-variant/15 space-y-4 animate-fade-in">
      <div class="flex items-center justify-between pb-3 border-b border-outline-variant/10">
        <h3 class="text-[13px] font-semibold text-on-surface">Bulk Import Parameters</h3>
        <button
          onClick={() => props.onCancel()}
          class="text-outline hover:text-on-surface bg-transparent border-0 cursor-pointer flex items-center justify-center p-1 rounded hover:bg-surface-container-high"
        >
          <MIcon name="close" class="text-[18px]" />
        </button>
      </div>
      
      {/* Drag & Drop zone */}
      <div
        onDragEnter={handleDrag}
        onDragOver={handleDrag}
        onDragLeave={handleDrag}
        onDrop={handleDrop}
        class={`border-2 border-dashed rounded-xl p-8 text-center transition-all ${
          dragActive() 
            ? "border-primary bg-primary/5 text-primary" 
            : "border-outline-variant/30 hover:border-primary/50 text-outline"
        }`}
      >
        <MIcon name="cloud_upload" class="text-[36px] mb-2 block" />
        <p class="text-xs font-semibold text-on-surface">Drag & Drop configuration file here or click to select</p>
        <p class="text-[10px] text-outline mt-1">Supports JSON (flat key-values or entries list) and CSV (key,value)</p>

        <div class="mt-4 flex justify-center">
          <Root placement="bottom" gutter={6}>
            <Trigger
              data-testid="bulk-import-examples-trigger"
              aria-label="View file examples"
              class="inline-flex cursor-pointer items-center gap-1.5 rounded-lg border border-outline-variant/15 bg-surface-container-high px-3 py-1.5 text-[11px] font-medium text-on-surface-variant transition-all hover:bg-surface-bright"
            >
              <MIcon name="description" class="text-[15px]" />
              View file examples
              <MIcon name="arrow_drop_down" class="text-[16px]" />
            </Trigger>
            <Portal>
              <Content class="z-50 min-w-44 overflow-hidden rounded-lg border border-outline-variant/20 bg-surface-container-low shadow-xl outline-none animate-fade-in">
                <Item
                  data-testid="bulk-import-example-csv"
                  onSelect={() => setSelectedExample("csv")}
                  class="flex w-full cursor-pointer items-center gap-2.5 border-0 bg-transparent px-4 py-2.5 text-[13px] text-on-surface outline-none transition-colors hover:bg-surface-container-high data-[highlighted]:bg-surface-container-high data-[highlighted]:ring-2 data-[highlighted]:ring-inset data-[highlighted]:ring-primary/40"
                >
                  <MIcon name="table_view" class="text-[16px] text-outline" />
                  CSV example
                </Item>
                <Item
                  data-testid="bulk-import-example-json"
                  onSelect={() => setSelectedExample("json")}
                  class="flex w-full cursor-pointer items-center gap-2.5 border-0 bg-transparent px-4 py-2.5 text-[13px] text-on-surface outline-none transition-colors hover:bg-surface-container-high data-[highlighted]:bg-surface-container-high data-[highlighted]:ring-2 data-[highlighted]:ring-inset data-[highlighted]:ring-primary/40"
                >
                  <MIcon name="data_object" class="text-[16px] text-outline" />
                  JSON example
                </Item>
              </Content>
            </Portal>
          </Root>
        </div>

        <Show when={selectedExample()}>
          {format => (
            <div class="mx-auto mt-3 max-w-2xl text-left">
              <p class="mb-1 text-[10px] font-semibold uppercase tracking-[0.05em] text-outline">
                {format().toUpperCase()} example
              </p>
              <pre
                data-testid="bulk-import-example-snippet"
                data-format={format()}
                aria-label={`${format().toUpperCase()} import file example`}
                class="max-h-56 overflow-auto rounded-lg border border-outline-variant/20 bg-surface-container-lowest px-3 py-2.5 font-mono text-[11px] leading-relaxed text-on-surface whitespace-pre"
              >
                {selectedExampleText()}
              </pre>
            </div>
          )}
        </Show>
        
        <input
          type="file"
          accept=".csv,.json"
          onChange={handleFileChange}
          class="hidden"
          id="bulk-file-upload-input"
        />
        <label
          for="bulk-file-upload-input"
          class="mt-4 inline-block cursor-pointer rounded-lg border border-outline-variant/15 bg-surface-container-high px-4 py-2 text-[11px] font-medium text-on-surface-variant transition-all hover:bg-surface-bright"
        >
          Browse Files
        </label>
      </div>

      {/* Preview Area */}
      <Show when={parsedImports().length > 0}>
        <div class="space-y-3 pt-2">
          <p class="text-[12px] font-semibold text-on-surface-variant">Parsed Parameters Preview</p>
          <div class="max-h-60 overflow-y-auto border border-outline-variant/10 rounded-xl bg-surface-container-lowest/50">
            <table class="w-full text-left border-collapse text-[11.5px]">
              <thead class="bg-surface-container-lowest/50 sticky top-0 border-b border-outline-variant/15">
                <tr>
                  <th class="py-2.5 px-4 w-10 text-[11px] font-medium text-outline uppercase tracking-[0.05em]">Select</th>
                  <th class="py-2.5 px-4 text-[11px] font-medium text-outline uppercase tracking-[0.05em]">Key</th>
                  <th class="py-2.5 px-4 text-[11px] font-medium text-outline uppercase tracking-[0.05em]">Value</th>
                  <th class="py-2.5 px-4 text-[11px] font-medium text-outline uppercase tracking-[0.05em]">Type</th>
                  <th class="py-2.5 px-4 text-[11px] font-medium text-outline uppercase tracking-[0.05em]">Status</th>
                </tr>
              </thead>
              <tbody class="divide-y divide-outline-variant/10">
                <For each={parsedImports()}>
                  {(item, idx) => (
                    <tr class="hover:bg-surface-container-high/20">
                      <td class="py-2.5 px-4">
                        <input
                          type="checkbox"
                          checked={item.selected}
                          onChange={(e) => {
                            const list = [...parsedImports()];
                            list[idx()].selected = e.currentTarget.checked;
                            setParsedImports(list);
                          }}
                          class="rounded border-outline-variant/30 text-primary focus:ring-primary/20 cursor-pointer"
                        />
                      </td>
                      <td class="py-2.5 px-4 font-mono font-bold text-on-surface">{item.key}</td>
                      <td class="py-2.5 px-4 font-mono text-outline truncate max-w-37.5">{item.value}</td>
                      <td class="py-2.5 px-4">
                        <span class="px-1.5 py-0.5 rounded text-[9px] font-bold uppercase bg-surface-container-high text-outline">
                          {item.contentType}
                        </span>
                      </td>
                      <td class="py-2.5 px-4">
                        <Show
                          when={item.alreadyExists}
                          fallback={
                            <span class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-bold uppercase bg-success/10 border border-success/20 text-success">
                              <MIcon name="add_circle" class="text-[10px]" />
                              New
                            </span>
                          }
                        >
                          <span class="inline-flex items-center gap-1 px-1.5 py-0.5 rounded text-[9px] font-bold uppercase bg-amber-500/10 border border-amber-500/20 text-amber-400">
                            <MIcon name="warning" class="text-[10px]" />
                            Overwrite
                          </span>
                        </Show>
                      </td>
                    </tr>
                  )}
                </For>
              </tbody>
            </table>
          </div>

          <div class="flex justify-end gap-3 pt-2">
            <Button
              type="button"
              disabled={importLoading() || props.isPending}
              onClick={executeImport}
            >
              <MIcon name="publish" class="text-[16px]" />
              {importLoading() ? "Importing…" : "Execute Import"}
            </Button>
            <Button
              type="button"
              variant="outline"
              onClick={() => { setParsedImports([]); props.onCancel(); }}
            >
              <MIcon name="close" class="text-[16px]" />
              Cancel
            </Button>
          </div>
        </div>
      </Show>
    </div>
  );
}
