import { describe, expectTypeOf, it } from "vitest";
import type { components, paths } from "../../generated/api";

type AuditLogQuery = NonNullable<
  paths["/admin/audit-logs"]["get"]["parameters"]["query"]
>;
type AuditLogPage = components["schemas"]["AuditLogPageDto"];
type AuditLogExportResponses =
  paths["/admin/audit-logs/export"]["get"]["responses"];
type AuditLogExportCsv = AuditLogExportResponses[200]["content"]["text/csv"];
type AuditLogExportJson =
  AuditLogExportResponses[200]["content"]["application/json"];
type AuditLogExportProblem<Status extends 400 | "4XX" | "5XX"> =
  AuditLogExportResponses[Status]["content"]["application/problem+json"];
type GeneratePasswordResetBody =
  paths["/admin/users/{id}/password-reset"]["post"]["responses"][200]["content"]["application/json"];
type ResetPasswordBody =
  paths["/auth/password-resets/{token}/password"]["post"]["requestBody"]["content"]["application/json"];
type AccountDetails =
  paths["/auth/me"]["get"]["responses"][200]["content"]["application/json"];
type ChangePasswordBody =
  paths["/auth/password"]["put"]["requestBody"]["content"]["application/json"];

describe("generated API contract", () => {
  it("includes project access levels", () => {
    expectTypeOf<components["schemas"]["ProjectDto"]["accessLevel"]>()
      .toEqualTypeOf<string>();
  });

  it("uses numbers for pagination queries and response counts", () => {
    expectTypeOf<AuditLogQuery["page"]>().toEqualTypeOf<
      number | undefined
    >();
    expectTypeOf<AuditLogQuery["pageSize"]>().toEqualTypeOf<
      number | undefined
    >();
    expectTypeOf<AuditLogPage["page"]>().toEqualTypeOf<number>();
    expectTypeOf<AuditLogPage["pageSize"]>().toEqualTypeOf<number>();
    expectTypeOf<AuditLogPage["totalCount"]>().toEqualTypeOf<number>();
    expectTypeOf<AuditLogPage["totalPages"]>().toEqualTypeOf<number>();
  });

  it("types streamed export success and problem responses", () => {
    expectTypeOf<AuditLogExportCsv>().toEqualTypeOf<string>();
    expectTypeOf<AuditLogExportJson>().toEqualTypeOf<string>();
    expectTypeOf<AuditLogExportProblem<400>>().toEqualTypeOf<
      components["schemas"]["ApiProblemDetails"]
    >();
    expectTypeOf<AuditLogExportProblem<"4XX">>().toEqualTypeOf<
      components["schemas"]["ApiProblemDetails"]
    >();
    expectTypeOf<AuditLogExportProblem<"5XX">>().toEqualTypeOf<
      components["schemas"]["ApiProblemDetails"]
    >();
  });
});

describe("generated password API contract", () => {
  it("types password reset operations", () => {
    expectTypeOf<GeneratePasswordResetBody>().toEqualTypeOf<
      components["schemas"]["GeneratePasswordResetResponse"]
    >();
    expectTypeOf<ResetPasswordBody>().toEqualTypeOf<
      components["schemas"]["ResetPasswordRequest"]
    >();
  });

  it("types current-account and password-change operations", () => {
    expectTypeOf<AccountDetails>().toEqualTypeOf<
      components["schemas"]["AccountDetailsResponse"]
    >();
    expectTypeOf<ChangePasswordBody>().toEqualTypeOf<
      components["schemas"]["ChangePasswordRequest"]
    >();
    expectTypeOf<components["schemas"]["UserDto"]["passwordEnabled"]>().toEqualTypeOf<boolean>();
  });
});
