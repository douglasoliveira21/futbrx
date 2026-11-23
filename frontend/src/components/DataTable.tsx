import { useState } from "react";
import {
  useReactTable,
  getCoreRowModel,
  getSortedRowModel,
  flexRender,
  type ColumnDef,
  type SortingState,
} from "@tanstack/react-table";
export function DataTable<T>({
  data,
  columns,
}: {
  data: T[];
  columns: ColumnDef<T, any>[];
}) {
  const [sorting, setSorting] = useState<SortingState>([]);
  const table = useReactTable({
    data,
    columns,
    state: { sorting },
    onSortingChange: setSorting,
    getCoreRowModel: getCoreRowModel(),
    getSortedRowModel: getSortedRowModel(),
  });
  return (
    <div className="table-scroll">
      <table>
        <thead>
          {table.getHeaderGroups().map((h) => (
            <tr key={h.id}>
              {h.headers.map((c) => (
                <th
                  key={c.id}
                  scope="col"
                  aria-sort={
                    c.column.getIsSorted() === "asc"
                      ? "ascending"
                      : c.column.getIsSorted() === "desc"
                        ? "descending"
                        : "none"
                  }
                >
                  <button
                    className="sort"
                    onClick={c.column.getToggleSortingHandler()}
                    disabled={!c.column.getCanSort()}
                  >
                    {flexRender(c.column.columnDef.header, c.getContext())}
                    {c.column.getIsSorted() === "asc"
                      ? " ↑"
                      : c.column.getIsSorted() === "desc"
                        ? " ↓"
                        : ""}
                  </button>
                </th>
              ))}
            </tr>
          ))}
        </thead>
        <tbody>
          {table.getRowModel().rows.map((r) => (
            <tr key={r.id}>
              {r.getVisibleCells().map((c) => (
                <td key={c.id}>
                  {flexRender(c.column.columnDef.cell, c.getContext())}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
      {data.length === 0 && (
        <p className="empty">Nenhum resultado para os filtros selecionados.</p>
      )}
    </div>
  );
}
