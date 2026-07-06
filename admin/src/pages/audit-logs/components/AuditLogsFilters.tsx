import flatpickr from "flatpickr";
import "flatpickr/dist/flatpickr.css";
import { createEffect, onCleanup, onMount, Show } from "solid-js";
import { MIcon } from "../../../shared/ui/icons";
import { Input } from "../../../shared/ui/input";
import { Select } from "../../../shared/ui/select";

interface AuditLogsFiltersProps {
  search: string;
  setSearch: (v: string) => void;
  filterAction: string;
  setFilterAction: (v: string) => void;
  filterEnv: string;
  setFilterEnv: (v: string) => void;
  dateFrom: string;
  setDateFrom: (v: string) => void;
  dateTo: string;
  setDateTo: (v: string) => void;
  uniqueActions: string[];
  uniqueEnvs: string[];
  clearAllFilters: () => void;
}

export function AuditLogsFilters(props: AuditLogsFiltersProps) {
  let fromInputRef: HTMLInputElement | undefined;
  let toInputRef: HTMLInputElement | undefined;
  let fpFrom: flatpickr.Instance | undefined;
  let fpTo: flatpickr.Instance | undefined;

  onMount(() => {
    if (fromInputRef) {
      fpFrom = flatpickr(fromInputRef, {
        dateFormat: "Y-m-d",
        defaultDate: props.dateFrom || undefined,
        onChange: (_, dateStr) => {
          props.setDateFrom(dateStr);
          if (fpTo) {
            fpTo.set("minDate", dateStr || undefined);
          }
        }
      });
    }

    if (toInputRef) {
      fpTo = flatpickr(toInputRef, {
        dateFormat: "Y-m-d",
        defaultDate: props.dateTo || undefined,
        onChange: (_, dateStr) => {
          props.setDateTo(dateStr);
          if (fpFrom) {
            fpFrom.set("maxDate", dateStr || undefined);
          }
        }
      });
    }
  });

  // Keep Flatpickr instances in sync if the parent changes the dates (e.g. "Clear" click)
  createEffect(() => {
    const from = props.dateFrom;
    if (fpFrom) {
      if (fpFrom.input.value !== from) {
        if (!from) {
          fpFrom.clear(false);
        } else {
          fpFrom.setDate(from, false);
        }
      }
      if (fpTo) {
        fpTo.set("minDate", from || undefined);
      }
    }
  });

  createEffect(() => {
    const to = props.dateTo;
    if (fpTo) {
      if (fpTo.input.value !== to) {
        if (!to) {
          fpTo.clear(false);
        } else {
          fpTo.setDate(to, false);
        }
      }
      if (fpFrom) {
        fpFrom.set("maxDate", to || undefined);
      }
    }
  });

  onCleanup(() => {
    fpFrom?.destroy();
    fpTo?.destroy();
  });

  const clearDates = () => {
    props.setDateFrom("");
    props.setDateTo("");
  };

  return (
    <div class="space-y-4">
      {/* Search and drop-down filters */}
      <div class="flex flex-wrap items-center gap-4">
        <Input
          data-testid="audit-search-input"
          type="text"
          placeholder="Filter audit trail..."
          value={props.search}
          onInput={e => props.setSearch(e.currentTarget.value)}
          class="h-10 w-56"
          leftIcon="search"
          wrapperStyle="w-auto"
        />
        <span class="text-outline/70 shrink-0 text-[11px] font-medium">Filters:</span>

        <div class="flex gap-2">
          {/* Action Type filter */}
          <div class="w-44">
            <Select
              value={props.filterAction === "all" ? "" : props.filterAction}
              onChange={val => props.setFilterAction(val)}
              placeholder="Action Type"
              class="h-10"
              options={props.uniqueActions.map(action => ({ value: action, label: action }))}
            />
          </div>

          {/* Environment filter */}
          <div class="w-44">
            <Select
              value={props.filterEnv === "all" ? "" : props.filterEnv}
              onChange={val => props.setFilterEnv(val)}
              placeholder="Environment"
              class="h-10"
              options={props.uniqueEnvs.map(env => ({ value: env, label: env }))}
            />
          </div>
        </div>

        <button
          onClick={() => props.clearAllFilters()}
          class="text-outline hover:text-error flex cursor-pointer items-center gap-1 border-0 bg-transparent text-[11px] font-medium transition-colors"
        >
          <MIcon name="close" class="text-[14px]" />
          Clear
        </button>
      </div>

      {/* Date Range picker */}
      <div class="flex flex-wrap items-center gap-3">
        <span class="text-outline/70 shrink-0 text-[11px] font-medium">Date Range:</span>
        <div class="relative flex items-center">
          <Input
            ref={fromInputRef}
            type="text"
            placeholder="From Date"
            value={props.dateFrom}
            class="h-10 w-32 cursor-pointer pr-8 pl-3"
          />
          <MIcon
            name="calendar_today"
            class="text-outline/70 pointer-events-none absolute right-2.5 text-xs"
          />
        </div>
        <span class="text-outline text-[11px]">to</span>
        <div class="relative flex items-center">
          <Input
            ref={toInputRef}
            type="text"
            placeholder="To Date"
            value={props.dateTo}
            class="h-10 w-32 cursor-pointer pr-8 pl-3"
          />
          <MIcon
            name="calendar_today"
            class="text-outline/70 pointer-events-none absolute right-2.5 text-xs"
          />
        </div>
        <Show when={props.dateFrom || props.dateTo}>
          <button
            onClick={clearDates}
            class="text-outline hover:text-error flex cursor-pointer items-center gap-1 border-0 bg-transparent text-[11px] font-medium transition-colors"
          >
            <MIcon name="close" class="text-[14px]" />
            Clear dates
          </button>
        </Show>
      </div>
    </div>
  );
}
