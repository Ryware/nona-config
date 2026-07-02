/**
 * Common interface for SSO provider adapters.
 * Each provider returns an id-token string on success or throws on error.
 */
export interface SsoProvider {
  /**
   * Trigger the provider-specific sign-in flow and return an id-token string.
   * Throws an Error with a user-facing message on failure.
   */
  signIn(): Promise<string>;
}
