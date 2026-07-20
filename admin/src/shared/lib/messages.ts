/**
 * Centralised user-facing toast / notification strings.
 * Import these constants instead of using inline string literals.
 */
export const MSG = {
  // --- Parameters ---
  PARAM_CREATED: "Parameter created",
  PARAM_UPDATED: "Parameter updated successfully",
  PARAM_ROLLED_BACK: "Parameter rolled back",
  PARAM_DELETED: "Parameter deleted",
  PARAM_CREATE_FAILED: "Failed to create parameter",
  PARAM_UPDATE_FAILED: "Failed to update parameter",
  PARAM_ROLLBACK_FAILED: "Failed to roll back parameter",
  PARAM_DELETE_FAILED: "Failed to delete parameter",
  SHARE_LINK_CREATED: "Share link created",
  SHARE_LINK_CREATE_FAILED: "Failed to create share link",
  SHARE_LINK_REVOKED: "Share link revoked",
  SHARE_LINK_REVOKE_FAILED: "Failed to revoke share link",

  // --- Environments ---
  ENV_CREATED: "Environment created",
  ENV_DELETED: "Environment deleted",
  ENV_CREATE_FAILED: "Failed to create environment",
  ENV_DELETE_FAILED: "Failed to delete environment",

  // --- Config Releases ---
  RELEASE_PUBLISHED: "Release published",
  RELEASE_PUBLISH_FAILED: "Failed to publish release",
  RELEASE_ACTIVATED: "Active release updated",
  RELEASE_ACTIVATE_FAILED: "Failed to update active release",
  RELEASE_DELETED: "Release deleted",
  RELEASE_DELETE_FAILED: "Failed to delete release",

  // --- Projects ---
  PROJECT_CREATED: "Project created",
  PROJECT_DELETED: "Project deleted",
  PROJECT_CREATE_FAILED: "Failed to create project",
  PROJECT_DELETE_FAILED: "Failed to delete project",

  // --- API Keys ---
  API_KEY_CREATED: "API key created",
  API_KEY_CREATE_FAILED: "Failed to create API key",
  API_KEY_DELETED: "API key deleted",
  API_KEY_DELETE_FAILED: "Failed to delete API key",
  COPIED: "Copied to clipboard",
  COPY_FAILED: "Copy failed",

  // --- Users / Members ---
  INVITE_GENERATED: "Invitation link generated",
  INVITE_COPIED: "Invitation link copied",
  INVITE_COPY_FAILED: "Failed to copy invitation link",
  MEMBER_UPDATED: "Member updated successfully",
  MEMBER_REMOVED: "Team member removed",
  MEMBER_UPDATE_FAILED: "Failed to update member",
  MEMBER_REMOVE_FAILED: "Failed to remove member",

  // --- Validation ---
  EMAIL_REQUIRED: "Email is required",
  NAME_REQUIRED: "Name is required",

  // --- Bulk Import ---
  BULK_IMPORT_NO_SELECTION: "No parameters selected to import",

  // --- Auth ---
  LOGIN_FAILED: "Email or password is incorrect. Please check your credentials.",
  REGISTER_FAILED: "Registration failed. Please try again.",
  REGISTER_UNEXPECTED: "An unexpected error occurred during registration.",
  SSO_FAILED_GOOGLE: "Google sign-in failed. Please try again.",
  SSO_FAILED_MICROSOFT: "Microsoft sign-in failed. Please try again.",
  PASSWORD_MISMATCH: "Passwords do not match.",
  FORGOT_SEND_FAILED: "Failed to send reset email. Please try again.",

  // --- Bulk Import (dynamic) ---
  bulkImportSuccess: (count: number) => `Imported ${count} parameter${count !== 1 ? "s" : ""} successfully`,
} as const;
