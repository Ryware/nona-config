import type { ConfigEntry } from "../../types";

export const PARAMETER_TREE_MAX_SEGMENTS = 4;

export interface ParameterTreeNode {
  id: string;
  label: string;
  depth: number;
  entry?: ConfigEntry;
  children: ParameterTreeNode[];
  count: number;
  legacy?: boolean;
}

interface MutableTreeNode {
  id: string;
  label: string;
  depth: number;
  entry?: ConfigEntry;
  children: Map<string, MutableTreeNode>;
  legacy?: boolean;
}

const compareText = (left: string, right: string) =>
  left.localeCompare(right, undefined, { sensitivity: "base", numeric: true })
  || left.localeCompare(right);

export function isTreeCompatibleKey(key: string) {
  const segments = key.split(":");
  return segments.length <= PARAMETER_TREE_MAX_SEGMENTS
    && segments.every(segment => segment.length > 0);
}

export function parameterName(key: string) {
  if (!isTreeCompatibleKey(key)) return key;
  return key.split(":").at(-1) ?? key;
}

export function buildParameterTree(entries: readonly ConfigEntry[]): ParameterTreeNode[] {
  const roots = new Map<string, MutableTreeNode>();

  for (const entry of entries) {
    if (!isTreeCompatibleKey(entry.key)) {
      roots.set(`legacy:${entry.key}`, {
        id: `legacy:${entry.key}`,
        label: entry.key,
        depth: 0,
        entry,
        children: new Map(),
        legacy: true
      });
      continue;
    }

    const segments = entry.key.split(":");
    let siblings = roots;
    let path = "";

    segments.forEach((segment, index) => {
      path = path ? `${path}:${segment}` : segment;
      const mapKey = `path:${path.toLowerCase()}`;
      let node = siblings.get(mapKey);
      if (!node) {
        node = {
          id: path,
          label: segment,
          depth: index,
          children: new Map()
        };
        siblings.set(mapKey, node);
      }

      if (index === segments.length - 1) node.entry = entry;
      siblings = node.children;
    });
  }

  const finalize = (node: MutableTreeNode): ParameterTreeNode => {
    const children = [...node.children.values()]
      .map(finalize)
      .sort((left, right) => compareText(left.label, right.label) || compareText(left.id, right.id));
    return {
      id: node.id,
      label: node.label,
      depth: node.depth,
      entry: node.entry,
      children,
      count: (node.entry ? 1 : 0) + children.reduce((total, child) => total + child.count, 0),
      legacy: node.legacy
    };
  };

  return [...roots.values()]
    .map(finalize)
    .sort((left, right) => compareText(left.label, right.label) || compareText(left.id, right.id));
}
