const maxReleaseVersionPart = 2_147_483_647n;
const numericPartPattern = /^[0-9]+$/;

export type ReleaseVersionFormat = "exact" | "majorMinor";

/**
 * Parses the release version formats accepted by the management UI and returns
 * the exact, backend-compatible version that should be submitted.
 */
export function parseReleaseVersion(
  value: string,
  format: ReleaseVersionFormat = "exact"
): string | null {
  const parts = value.trim().split(".");
  const expectedParts = format === "majorMinor" ? 2 : 3;

  if (parts.length !== expectedParts) return null;

  const normalizedParts: string[] = [];
  for (const part of parts) {
    if (!numericPartPattern.test(part)) return null;

    const parsed = BigInt(part);
    if (parsed > maxReleaseVersionPart) return null;

    normalizedParts.push(parsed.toString());
  }

  if (format === "majorMinor") normalizedParts.push("0");
  return normalizedParts.join(".");
}
