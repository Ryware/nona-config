import {
  Content,
  Icon,
  Item,
  ItemIndicator,
  ItemLabel,
  Listbox,
  Portal,
  Root,
  Trigger,
  Value,
} from "@kobalte/core/select";
import type { ComponentProps } from "solid-js";
import { cn } from "../lib/utils";


export interface SelectOption {
  value: string;
  label: string;
}

interface SelectProps {
  value: string;
  onChange: (value: string) => void;
  options: (string | SelectOption)[];
  placeholder?: string;
  id?: string;
  disabled?: boolean;
  class?: string;
}

type KobalteSelectItemProps = ComponentProps<typeof Item>;
type SelectItemProps = Omit<KobalteSelectItemProps, "item"> & {
  item: KobalteSelectItemProps["item"] & {
    rawValue: SelectOption;
  };
};

export function Select(props: SelectProps) {
  const normalizedOptions = () =>
    props.options.map((opt) =>
      typeof opt === "string" ? { value: opt, label: opt } : opt,
    );

  const selectedOption = () =>
    normalizedOptions().find((opt) => opt.value === props.value);

  const handleValueChange = (newOpt: SelectOption | null) => {
    if (newOpt) {
      props.onChange(newOpt.value);
    }
  };

  return (
    <Root
      options={normalizedOptions()}
      value={props.value ? selectedOption() ?? null : null}
      onChange={handleValueChange}
      optionValue="value"
      optionTextValue="label"
      disabled={props.disabled}
      placeholder={props.placeholder}
      itemComponent={(itemProps: SelectItemProps) => (
        <Item
          item={itemProps.item}
          class="flex items-center justify-between w-full px-3 py-2 rounded-lg text-xs text-on-surface hover:bg-surface-container-high hover:text-on-surface transition-colors cursor-pointer outline-none select-none relative data-selected:bg-surface-container-high/60"
        >
          <div class="flex items-center gap-2">
            <span class="w-4 h-4 flex items-center justify-center shrink-0">
              <ItemIndicator class="text-primary">
                <span class="material-symbols-outlined text-[16px]">check</span>
              </ItemIndicator>
            </span>
            <ItemLabel class="text-left font-sans">
              {itemProps.item.rawValue.label}
            </ItemLabel>
          </div>
        </Item>
      )}
    >
      <Trigger
        id={props.id}
        class={cn(
          "flex items-center justify-between w-full pl-4 pr-3 py-2 text-[13px] text-on-surface bg-surface-container-lowest border border-outline-variant/20 rounded-lg hover:border-outline-variant/30 transition-all focus:outline-none focus:ring-2 focus:ring-primary/20 h-11 cursor-pointer outline-none",
          props.class,
        )}
      >
        <Value class="text-left font-sans">
          {(state) => {
            const label = (state.selectedOption() as SelectOption | null)?.label;
            return (
              <span class={label ? "text-on-surface" : "text-outline/50"}>
                {label ?? props.placeholder}
              </span>
            );
          }}
        </Value>
        <Icon class="text-outline/70 flex items-center justify-center shrink-0">
          <span class="material-symbols-outlined text-[18px]">expand_more</span>
        </Icon>
      </Trigger>
      <Portal>
        <Content class="bg-surface-container-low border border-outline-variant/20 rounded-xl shadow-2xl p-1 z-100 min-w-40 animate-fade-in outline-none">
          <Listbox class="outline-none" />
        </Content>
      </Portal>
    </Root>
  );
}
