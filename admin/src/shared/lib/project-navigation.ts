export type ProjectPageSection =
  | "environments"
  | "parameters"
  | "sharedLinks"
  | "apiKeys"
  | "releases";

const hasReleaseContext = (search: string) => {
  const searchParams = new URLSearchParams(search);

  return ["viewRelease", "release", "amend"].some(parameter => {
    const value = searchParams.get(parameter);
    return value !== null && value.length > 0;
  });
};

export function getProjectPageSection(
  pathname: string,
  search = ""
): ProjectPageSection | undefined {
  const parts = pathname.split("/").filter(Boolean);
  if (parts[0] !== "projects" || !parts[1]) return undefined;

  if (parts[2] === "environments") return "environments";
  if (parts[2] === "shared-links") return "sharedLinks";
  if (parts[2] === "api-keys") return "apiKeys";
  if (parts[2] === "releases") return "releases";
  if (parts.length !== 2) return undefined;

  return hasReleaseContext(search) ? "releases" : "parameters";
}
