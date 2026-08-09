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

describe("generated audit API contract", () => {
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
