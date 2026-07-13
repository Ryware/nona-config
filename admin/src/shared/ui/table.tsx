import { type JSX, type ParentComponent, splitProps } from "solid-js";
import { cn } from "../lib/utils";


export const Table: ParentComponent<JSX.HTMLAttributes<HTMLTableElement>> = (
  props,
) => {
  const [local, others] = splitProps(props, ["class", "children"]);
  return (
    <div class="w-full overflow-auto">
      <table
        class={cn("w-full caption-bottom text-sm", local.class)}
        {...others}
      >
        {local.children}
      </table>
    </div>
  );
};

export const TableHeader: ParentComponent<
  JSX.HTMLAttributes<HTMLTableSectionElement>
> = (props) => {
  const [local, others] = splitProps(props, ["class", "children"]);
  return (
    <thead
      class={cn("border-b border-outline-variant/30", local.class)}
      {...others}
    >
      {local.children}
    </thead>
  );
};

export const TableBody: ParentComponent<
  JSX.HTMLAttributes<HTMLTableSectionElement>
> = (props) => {
  const [local, others] = splitProps(props, ["class", "children"]);
  return (
    <tbody
      class={cn("divide-y divide-outline-variant/20", local.class)}
      {...others}
    >
      {local.children}
    </tbody>
  );
};

export const TableFooter: ParentComponent<
  JSX.HTMLAttributes<HTMLTableSectionElement>
> = (props) => {
  const [local, others] = splitProps(props, ["class", "children"]);
  return (
    <tfoot
      class={cn(
        "border-t border-outline-variant/30 bg-surface-container-high font-medium",
        local.class,
      )}
      {...others}
    >
      {local.children}
    </tfoot>
  );
};

export const TableRow: ParentComponent<
  JSX.HTMLAttributes<HTMLTableRowElement>
> = (props) => {
  const [local, others] = splitProps(props, ["class", "children"]);
  return (
    <tr
      class={cn(
        "transition-colors hover:bg-surface-container-high data-[state=selected]:bg-surface-container-highest",
        local.class,
      )}
      {...others}
    >
      {local.children}
    </tr>
  );
};

export const TableHead: ParentComponent<
  JSX.ThHTMLAttributes<HTMLTableCellElement>
> = (props) => {
  const [local, others] = splitProps(props, ["class", "children"]);
  return (
    <th
      class={cn(
        "h-11 px-4 text-left text-[11px] font-semibold text-on-surface-variant uppercase tracking-wider [&:has([role=checkbox])]:pr-0",
        local.class,
      )}
      {...others}
    >
      {local.children}
    </th>
  );
};

export const TableCell: ParentComponent<
  JSX.TdHTMLAttributes<HTMLTableCellElement>
> = (props) => {
  const [local, others] = splitProps(props, ["class", "children"]);
  return (
    <td
      class={cn(
        "p-4 text-left text-on-surface [&:has([role=checkbox])]:pr-0",
        local.class,
      )}
      {...others}
    >
      {local.children}
    </td>
  );
};

export const TableCaption: ParentComponent<
  JSX.HTMLAttributes<HTMLTableCaptionElement>
> = (props) => {
  const [local, others] = splitProps(props, ["class", "children"]);
  return (
    <caption
      class={cn("mt-4 text-sm text-on-surface-variant", local.class)}
      {...others}
    >
      {local.children}
    </caption>
  );
};
