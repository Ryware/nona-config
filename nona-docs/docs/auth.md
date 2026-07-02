# Authentication

DustHowl uses opaque server-side sessions for browser, REST, upload, realtime,
and voice auth.

## Migration Notes

The previous app auth model used long-lived JWTs stored by frontend code and
sent through REST headers, upload requests, and WebSocket connection params.
That made stolen tokens durable and hard to invalidate server-side.

Current app auth is a hard cut from that model:

- App JWTs are not issued or accepted for the normal session flow.
- Auth secrets are not stored in `localStorage` or `sessionStorage`.
- REST, upload, realtime, and browser voice auth all use the session cookie.
- Existing JWT-only sessions must log in again.

## Session Model

On login, signup, or successful password restore, the API creates a random
32-byte session token and returns it only as the `dusthowl-session` cookie. The
raw token is not stored in the database. The `auth_sessions` table stores:

- `user_id`
- SHA-256 `token_hash`
- `created_at`
- `last_used_at`
- `expires_at`
- `revoked_at`
- `persistent`

Session rows live in Postgres and can be revoked server-side. Password restore
revokes all existing sessions for that user before creating the new session.
Logout revokes the current session and clears auth cookies.

## Cookies

The API sets two auth-related cookies:

- `dusthowl-session`: HttpOnly session token cookie.
- `dusthowl-csrf`: readable CSRF token cookie.

Both cookies use `Path=/`. In HTTPS requests they are set with `Secure` and
`SameSite=None`; in HTTP development requests they are set with `SameSite=Lax`.

`Login automatically` controls cookie persistence:

- Off: browser-session cookie, no `Max-Age`.
- On: persistent cookie with `Max-Age=90 days`.

Browsers may keep session cookies across restarts when session restore or
background browser processes are enabled. Server-side expiry still applies.

## Lifetimes

Sessions use sliding expiry:

- Temporary sessions last 24 hours from the latest refresh.
- Persistent sessions last 90 days from the latest refresh.
- A session is refreshed when its `last_used_at` is at least 10 minutes old, or
  when it is within 10 minutes of expiry.
- There is no absolute lifetime cap.

REST API requests and uploads refresh the session when needed and reissue the
cookie. WebSocket and voice activity validate the session but do not extend its
lifetime.

## HTTP API

Frontend HTTP calls use cookie credentials:

```ts
fetch(url, {
  credentials: 'include'
});
```

Authenticated requests read identity from the `dusthowl-session` cookie.

Session endpoints:

- `POST /api/session/refresh`: validates the current cookie session, refreshes
  it if needed, and returns current auth state.
- `POST /api/session/logout`: revokes the current session and clears auth
  cookies.

Login, signup, password restore, and session-refresh responses do not serialize
the session token into JSON. Browser auth comes from the cookie set by the HTTP
response.

## CSRF

Unsafe cookie-authenticated HTTP requests must include the CSRF token:

```http
X-Dusthowl-CSRF: <value of dusthowl-csrf cookie>
```

The backend compares the header with the readable `dusthowl-csrf` cookie. CSRF
checks are skipped for:

- `GET`, `HEAD`, and `OPTIONS`
- auth bootstrap routes: login, signup, password restore, and
  `/api/session/refresh`
- unsafe requests with no `dusthowl-session` cookie

This is a double-submit cookie pattern. The session cookie remains HttpOnly;
only the CSRF token is readable by frontend code.

## Realtime WebSocket

The main realtime WebSocket at `/ws` authenticates from the
`dusthowl-session` cookie during connection setup. WebSocket connection params
may include non-secret metadata such as `clientId`.

The API validates the WebSocket `Origin` header. Requests are allowed when the
origin host matches the API host, or when both hosts are loopback hosts for
local development.

## Voice Relay

Browser voice connections authenticate with the same `dusthowl-session` cookie.
The voice relay forwards the inbound cookie header to the backend internal
endpoint:

```text
POST /internal/voice-relay/auth/session
```

The backend validates the session and returns the user to the voice relay. WHIP
access-key auth remains a separate external credential and is not part of the
browser session-cookie flow.
