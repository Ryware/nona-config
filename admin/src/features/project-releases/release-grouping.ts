import type { ConfigRelease } from "../../types";

export interface MinorGroup {
  key: string;
  label: string;
  releases: ConfigRelease[];
  hasActive: boolean;
}

export interface MajorGroup {
  key: string;
  label: string;
  minorGroups: MinorGroup[];
  hasActive: boolean;
}

function parseVersionParts(version: string): { major: string; minor: string } | null {
  const parts = version.split(".");
  if (parts.length >= 2 && /^\d+$/.test(parts[0]) && /^\d+$/.test(parts[1])) {
    return { major: parts[0], minor: parts[1] };
  }
  return null;
}

export function groupReleases(releases: ConfigRelease[]): MajorGroup[] {
  const majorOrder: string[] = [];
  const majorGroups = new Map<
    string,
    { minorOrder: string[]; minorGroups: Map<string, MinorGroup> }
  >();

  for (const release of releases) {
    const parsed = parseVersionParts(release.version);
    const majorKey = parsed?.major ?? release.version;
    const minorKey = parsed ? `${parsed.major}.${parsed.minor}` : release.version;

    let majorEntry = majorGroups.get(majorKey);
    if (!majorEntry) {
      majorEntry = { minorOrder: [], minorGroups: new Map() };
      majorGroups.set(majorKey, majorEntry);
      majorOrder.push(majorKey);
    }

    let minorGroup = majorEntry.minorGroups.get(minorKey);
    if (!minorGroup) {
      minorGroup = {
        key: minorKey,
        label: parsed ? `${minorKey}.x` : release.version,
        releases: [],
        hasActive: false
      };
      majorEntry.minorGroups.set(minorKey, minorGroup);
      majorEntry.minorOrder.push(minorKey);
    }
    minorGroup.releases.push(release);
    if (release.isActive) minorGroup.hasActive = true;
  }

  return majorOrder.map(majorKey => {
    const entry = majorGroups.get(majorKey)!;
    const minorGroups = entry.minorOrder.map(key => entry.minorGroups.get(key)!);
    return {
      key: majorKey,
      label: /^\d+$/.test(majorKey) ? `${majorKey}.x` : majorKey,
      minorGroups,
      hasActive: minorGroups.some(group => group.hasActive)
    };
  });
}

export function countReleases(group: MajorGroup): number {
  return group.minorGroups.reduce((sum, minorGroup) => sum + minorGroup.releases.length, 0);
}
