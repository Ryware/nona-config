/**
 * Hierarchical query keys for the project entity.
 *
 * Usage:
 *   useQuery({ queryKey: projectKeys.list() })
 *   useQuery({ queryKey: projectKeys.detail(slug) })
 *   useQuery({ queryKey: projectKeys.environments(slug) })
 *   queryClient.invalidateQueries({ queryKey: projectKeys.detail(slug) })
 *     → cascades to environments + config entries for that project
 */
export const projectKeys = {
  /** All project-related cache entries */
  all: () => ["project"] as const,

  /** List of all projects */
  list: () => [...projectKeys.all(), "list"] as const,

  /** Single project by slug */
  detail: (slug: string) => [...projectKeys.all(), "detail", slug] as const,

  /** All environments of a project */
  environments: (slug: string) =>
    [...projectKeys.detail(slug), "environments"] as const,

  /** Config entries for a specific environment */
  configEntries: (slug: string, env: string) =>
    [...projectKeys.detail(slug), "config-entries", env] as const,

  /** Config releases for a specific environment */
  configReleases: (slug: string, env: string) =>
    [...projectKeys.detail(slug), "config-releases", env] as const,

  /** Details for a specific config release version */
  configReleaseDetails: (slug: string, env: string, version: string) =>
    [...projectKeys.configReleases(slug, env), version] as const,

  /** Version history for a specific config entry */
  configEntryHistory: (slug: string, env: string, key: string) =>
    [...projectKeys.configEntries(slug, env), key, "history"] as const,

  /** Share links for a specific config entry */
  configEntryShareLinks: (slug: string, env: string, key: string) =>
    [...projectKeys.configEntries(slug, env), key, "share-links"] as const,

  /** Aggregated share links for a specific environment */
  environmentShareLinks: (slug: string, env: string) =>
    [...projectKeys.detail(slug), "share-links", env] as const,

  /** Managed API keys for a project */
  apiKeys: (slug: string) => [...projectKeys.detail(slug), "api-keys"] as const,
} as const;
