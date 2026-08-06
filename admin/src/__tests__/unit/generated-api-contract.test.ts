import { describe, expectTypeOf, it } from "vitest";
import type { components, paths } from "../../generated/api";

type AuditLogQuery = NonNullable<
  paths["/admin/audit-logs"]["get"]["parameters"]["query"]
>;
type AuditLogPage = components["schemas"]["AuditLogPageDto"];

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
});
