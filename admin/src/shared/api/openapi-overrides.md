/**
 * OpenAPI Client-Side Overrides & Patches
 * =========================================
 * This file documents every place where the frontend manually patches, narrows,
 * or extends the auto-generated schema (`generated.ts`). Each entry has:
 *   - The generated shape (what the spec says)
 *   - The client shape (what we actually use)
 *   - Why the override exists
 *   - A tracking reference to the backend issue that should fix it
 *
 * When `generated.ts` is regenerated (run `npx openapi-typescript` / CI script),
 * review each override below and confirm it is still necessary.
 *
 * DO NOT import from this file at runtime — it is documentation only.
 * The actual types live in `src/entities/*/model/types.ts`.
 *
 * @see scripts/verify-openapi.sh — fails CI if generated.ts changes without a review.
 */

// ---------------------------------------------------------------------------
// OVERRIDE 1 — ProjectDto.description is missing from the spec
// ---------------------------------------------------------------------------
//
//  Generated:
//    ProjectDto: {
//      id: number | string;
//      name: string;
//      urlSlug: null | string;
//      environments: string[];
//      createdAt: string;
//      updatedAt: string;
//    }
//
//  Client (src/entities/project/model/types.ts → Project):
//    + description?: string
//    + urlSlug: string          (null stripped — service layer normalises)
//    + id: string               (number | string coerced to string in service)
//
//  Reason: Backend omitted `description` from the DTO. The field exists in the
//  database; its absence from the spec is a backend oversight.
//  TODO: https://github.com/<org>/<repo>/issues/TODO — add description to ProjectDto

// ---------------------------------------------------------------------------
// OVERRIDE 2 — UpsertConfigEntryRequest uses broad `null | string` for
//              contentType and scope instead of discriminated literal unions
// ---------------------------------------------------------------------------
//
//  Generated:
//    UpsertConfigEntryRequest: {
//      value: string;
//      contentType: null | string;
//      scope: null | string;
//    }
//
//  Client (src/entities/project/model/types.ts → UpdateConfigEntryRequest):
//    contentType: 'text' | 'number' | 'boolean' | 'json';
//    scope: 'client' | 'server' | 'all';
//
//  Reason: The spec does not declare the enum values. The narrower union types
//  are derived from backend source code inspection.
//  TODO: Add `enum` constraints to the OpenAPI spec for these fields

// ---------------------------------------------------------------------------
// OVERRIDE 3 — CreateApiKeyRequest.scope uses broad `string`
// ---------------------------------------------------------------------------
//
//  Generated:
//    CreateApiKeyRequest: { name: string; environment?: null | string; scope?: null | string; }
//
//  Client (src/entities/project/model/types.ts → CreateApiKeyRequest):
//    scope?: 'client' | 'server' | 'all';
//
//  Reason: The spec omits the enum. Scope values are parsed by backend API-key
//  creation and mirrored from the config scope vocabulary.
//  TODO: Add `enum: [client, server, all]` to the OpenAPI spec

// ---------------------------------------------------------------------------
// OVERRIDE 4 — UserDto.id and ProjectDto.id are typed as `number | string`
// ---------------------------------------------------------------------------
//
//  Generated:
//    UserDto: { id: number | string; ... }
//    ProjectDto: { id: number | string; ... }
//
//  Client:
//    User.id: string    (services call String(dto.id))
//    Project.id: string (services call String(dto.id))
//
//  Reason: The generated type reflects JSON Schema's int64 format note, which
//  openapi-typescript widens to number | string for safety. The backend always
//  returns integers that are small enough to be regular JS numbers; we coerce
//  to string at the service layer for consistent key usage.
//  TODO: Consider using `number` only once backend confirms no BigInt overflow

// ---------------------------------------------------------------------------
// OVERRIDE 5 — LoginResponse schema is not in components/schemas
// ---------------------------------------------------------------------------
//
//  Generated:
//    LoginResponse (referenced inline in endpoint responses, but schema body
//    is only visible at components["schemas"]["LoginResponse"])
//    → not directly exported at the path level
//
//  Client (src/entities/auth/model/types.ts → LoginResponse):
//    { token: string; username?: string; role: string; }
//
//  Reason: The generated file wraps all schemas under `components["schemas"]`
//  but provides no top-level named re-exports. The entity model mirrors the
//  actual response payload observed in production.
//  TODO: Verify `username` field nullability once confirmed with backend team

// ---------------------------------------------------------------------------
// OVERRIDE 6 — ConfigEntryDto.contentType / .scope use broad `string | null`
// ---------------------------------------------------------------------------
//
//  Generated:
//    ConfigEntryDto: {
//      key: string;
//      value: string;
//      contentType: null | string;
//      scope: null | string;
//    }
//
//  Client (src/entities/project/model/types.ts → ConfigEntry):
//    contentType: 'text' | 'number' | 'boolean' | 'json';
//    scope: 'client' | 'server' | 'all';
//
//  Same root cause as OVERRIDE 2 — the spec lacks enum annotations.
