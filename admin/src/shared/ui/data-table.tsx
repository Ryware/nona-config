import {
  createSolidTable,
  flexRender,
  getCoreRowModel,
  getSortedRowModel,
  type ColumnDef,
  type RowSelectionState,
  type SortingState,
} from "@tanstack/solid-table";
import { For, Show, createSignal } from "solid-js";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "./table";

interface DataTableProps<TData> {
  columns: ColumnDef<TData, unknown>[];
  data: TData[];
  onRowClick?: (row: TData) => void;
}

export function DataTable<TData>(props: DataTableProps<TData>) {
  const [sorting, setSorting] = createSignal<SortingState>([]);
  const [rowSelection, setRowSelection] = createSignal<RowSelectionState>({});

  const table = createSolidTable({
    get data() {
      return props.data;
    },
    get columns() {
      return props.columns;
    },
    state: {
      get sorting() {
        return sorting();
      },
      get rowSelection() {
        return rowSelection();
      },
    },
    onSortingChange: setSorting,
    onRowSelectionChange: setRowSelection,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  });

  return (
    <div class="bg-surface-container-lowest border border-outline-variant/15 rounded-xl overflow-hidden">
      <Table>
        <TableHeader>
          <For each={table.getHeaderGroups()}>
            {(headerGroup) => (
              <TableRow>
                <For each={headerGroup.headers}>
                  {(header) => (
                    <TableHead colSpan={header.colSpan}>
                      <Show when={!header.isPlaceholder}>
                        <div
                          class={
                            header.column.getCanSort()
                              ? "cursor-pointer select-none flex items-center gap-2"
                              : ""
                          }
                          onClick={header.column.getToggleSortingHandler()}
                        >
                          {flexRender(
                            header.column.columnDef.header,
                            header.getContext(),
                          )}
                          <Show when={header.column.getCanSort()}>
                            <span class="text-on-surface-variant">
                              {{
                                asc: "↑",
                                desc: "↓",
                              }[header.column.getIsSorted() as string] ?? "↕"}
                            </span>
                          </Show>
                        </div>
                      </Show>
                    </TableHead>
                  )}
                </For>
              </TableRow>
            )}
          </For>
        </TableHeader>
        <TableBody>
          <Show
            when={table.getRowModel().rows.length > 0}
            fallback={
              <TableRow>
                <TableCell
                  colSpan={props.columns.length}
                  class="text-center py-12"
                >
                  No data available
                </TableCell>
              </TableRow>
            }
          >
            {" "}
            <For each={table.getRowModel().rows}>
              {(row) => (
                <TableRow onClick={() => props.onRowClick?.(row.original)}>
                  <For each={row.getVisibleCells()}>
                    {(cell) => (
                      <TableCell>
                        {flexRender(
                          cell.column.columnDef.cell,
                          cell.getContext(),
                        )}
                      </TableCell>
                    )}
                  </For>
                </TableRow>
              )}
            </For>
          </Show>
        </TableBody>
      </Table>
    </div>
  );
}
