# Insighta Labs+ Backend

ASP.NET Core backend for the Insighta Labs+ profile intelligence platform. The Stage 2 queryable engine (filters, sorting, pagination, natural-language search) and the Stage 3 security surface (GitHub OAuth, JWT, RBAC, CLI + web portal) are preserved. Stage 4B adds query performance, query normalization, and large-scale CSV ingestion on top.

For a full write-up of the Stage 4B optimization work, including the design rationale and a before/after performance table, see [../SOLUTION.md](../SOLUTION.md).

## System Architecture

- Backend API: ASP.NET Core, EF Core, SQLite (WAL mode at runtime)
- Auth: GitHub OAuth with PKCE, JWT access tokens, rotating refresh tokens
- Interfaces: web portal via HTTP-only cookies, CLI via bearer tokens
- Authorization: `admin` and `analyst` roles enforced with ASP.NET policies
- Data: profile records plus users, OAuth sessions, and refresh tokens
- Caching: `IDistributedCache` with versioned scopes (in-process by default; Redis-swappable)
- Ingestion: streaming CSV reader, batched inserts on a pooled `DbContext` factory
- Deployment: backend and web portal can run together with `docker-compose.stage3.yml` from the parent folder

## Authentication Flow

Browser login:

1. Web redirects to `GET /api/v1/auth/github/start?client=web`.
2. Backend creates OAuth state and PKCE verifier/challenge.
3. GitHub redirects to `/api/v1/auth/github/callback`.
4. Backend exchanges the code, creates/updates the user, sets HTTP-only auth cookies, and redirects to the web portal.

CLI login:

1. CLI calls `POST /api/v1/auth/cli/start`.
2. Backend returns a GitHub authorization URL and state.
3. User completes GitHub login in the browser.
4. CLI polls `POST /api/v1/auth/cli/exchange`.
5. CLI stores credentials at `~/.insighta/credentials.json`.

## Token Handling Approach

- Access tokens are JWTs with a short lifetime, default `3` minutes.
- Refresh tokens are random secrets stored only as SHA-256 hashes in the database.
- Refresh tokens rotate on every refresh.
- Refresh tokens expire after `5` minutes by default.
- Web clients receive tokens in HTTP-only cookies.
- Web unsafe requests must send `X-CSRF-TOKEN` matching the readable `XSRF-TOKEN` cookie.
- CLI clients send `Authorization: Bearer <access_token>` and refresh automatically.

## Role Enforcement Logic

Roles:

- `admin`: read, search, export, create, delete, and CSV upload
- `analyst`: read, search, and export

Admin users are assigned by GitHub username through:

```bash
Auth__AdminGitHubUsernames=your-github-username,another-admin
```

Protected profile routes:

```text
GET    /api/v1/profiles                analyst or admin
GET    /api/v1/profiles/{id}           analyst or admin
GET    /api/v1/profiles/search         analyst or admin
GET    /api/v1/profiles/export.csv     analyst or admin
POST   /api/v1/profiles                admin only
POST   /api/v1/profiles/upload         admin only   (Stage 4B — CSV ingestion)
DELETE /api/v1/profiles/{id}           admin only
```

The canonical Stage 3 routes also live under `/api/profiles` and remain protected with the same policies.

## Natural Language Parsing & Query Normalization (Stage 4B)

Natural-language search remains rule-based and deterministic in [`NaturalLanguageProfileQueryParser`](src/HngStageOne.Api/Services/NaturalLanguageProfileQueryParser.cs). It converts supported phrases into the same `ProfileQueryOptions` used by structured filtering, then a canonical key generator hashes those options into a stable cache key.

Supported phrasings include:

```text
young males from nigeria
females above 30
adult males from kenya
male and female teenagers above 17
nigerian females between ages 20 and 45
women aged 20-45 living in nigeria
british teenagers
south african men over 40
```

Normalization rules implemented by the parser:

- **Gender forms**: `male/males/man/men/boy/boys` and `female/females/woman/women/girl/girls`.
- **Age groups**: `child/children/kid/kids`, `teenager/teens`, `adult/adults`, `senior/seniors/elderly`.
- **Age ranges**: `between 20 and 45`, `between ages 20 and 45`, `aged 20 to 45`, `aged 20-45`, `ages 20-45`, `20-45 years old`, `20 to 45 years old`. Unicode dashes (`–`, `—`) are normalized to plain hyphens.
- **Single-bound ages**: `above N`, `over N`, `older than N`, `at least N`, `below N`, `under N`, `younger than N`, `at most N`.
- **Locative prepositions**: `from <country>`, `in <country>`, `living in <country>`, `based in <country>`, `located in <country>`, `residing in <country>`.
- **Demonyms**: `Nigerian → NG`, `British → GB`, `South African → ZA`, etc. — see [`Helpers/DemonymLookup.cs`](src/HngStageOne.Api/Helpers/DemonymLookup.cs).

Two semantically equivalent queries collapse to the same canonical key. The serialized form is a fixed-order pipe-delimited string:

```text
gender=female|country=NG|min_age=20|max_age=45|sort=created_at|order=desc|page=1|limit=10
```

Equivalence is enforced by tests in [`tests/HngStageOne.Api.Tests/CanonicalQueryKeyTests.cs`](tests/HngStageOne.Api.Tests/CanonicalQueryKeyTests.cs) — for example, `"Nigerian females between ages 20 and 45"` and `"Women aged 20-45 living in Nigeria"` produce identical keys, so the second call hits cache instead of the database.

Unsupported phrases return the existing standardized `UnableToInterpretQueryException` response. There are no LLMs involved.

## Query Performance (Stage 4B)

Five layers, each justified independently:

1. **Composite indexes** ([`Migrations/20260505100000_StageFourCompositeIndexes.cs`](src/HngStageOne.Api/Migrations/20260505100000_StageFourCompositeIndexes.cs)) chosen from the actual `WHERE` clauses produced by `ProfileRepository.ApplyFilters`:

   | Index | Serves |
   |---|---|
   | `(CountryId, Gender, Age)` | Country + gender + age range — the dominant combo |
   | `(Gender, AgeGroup)` | Cohort queries (`young males`, `adult females`) |
   | `(CountryId, AgeGroup)` | "Adults from Kenya"-style filters |
   | `(CreatedAt, Id)` | Stable ordering for paging; supports keyset pagination later |

   The Stage 3 single-column indexes remain — they help when only one column is filtered.

2. **Versioned distributed cache** ([`Services/Caching/QueryCache.cs`](src/HngStageOne.Api/Services/Caching/QueryCache.cs)) over `IDistributedCache`. Three scopes:

   | Scope | TTL | Used by |
   |---|---|---|
   | `profiles:list` | 60 s | `GET /profiles`, `GET /profiles/search` |
   | `profiles:detail` | 120 s | `GET /profiles/{id}` |
   | `profiles:export` | 60 s | `GET /profiles/export.csv` |

   Each scope holds a monotonically-increasing version counter. Real keys are namespaced (`profiles:list:v3:...`). On a write, the scope's version is bumped — every previously-cached entry is unreachable instantly, no key enumeration, works against any backing store including Redis.

   The default backend is `AddDistributedMemoryCache()` (in-process, zero infrastructure, satisfies the "no horizontal scaling" Stage 4B constraint). Switching to Redis is a one-line change to `AddStackExchangeRedisCache(...)` because the rest of the code only sees `IDistributedCache`.

3. **DbContext pooling** ([`Program.cs`](src/HngStageOne.Api/Program.cs)). `AddDbContextPool<AppDbContext>` reuses configured contexts across requests, removing per-request setup. A separate `AddPooledDbContextFactory<AppDbContext>` is registered so the CSV ingester can take its own contexts without competing with request-scoped ones.

4. **SQLite WAL mode** ([`Program.cs`](src/HngStageOne.Api/Program.cs)). At startup, when the SQLite provider is detected, `PRAGMA journal_mode=WAL` and `PRAGMA synchronous=NORMAL` are issued so a single writer (e.g. CSV ingest) can coexist with many concurrent readers. No-op on Postgres / MySQL where MVCC already provides this.

5. **Active-user lookup cache** ([`Middleware/ActiveUserMiddleware.cs`](src/HngStageOne.Api/Middleware/ActiveUserMiddleware.cs)). The `IsActive` check that previously hit the DB on every authenticated request is now cached for 60 s per user. On a remote database that single round-trip dominated request time; this is the largest win for authenticated reads.

Cache-correctness rules:

- Every write (`CreateProfileAsync`, `DeleteProfileAsync`, CSV upload completion) calls `InvalidateReadCachesAsync`, bumping `profiles:list` and `profiles:export` versions.
- `DeleteProfileAsync` additionally removes the affected `profiles:detail` key.
- Best-effort cache invalidation: a cache failure never fails a write — the next read just falls through to the database and repopulates.

See [../SOLUTION.md](../SOLUTION.md) §1 for the before/after measurement table and trade-offs.

## CSV Data Ingestion (Stage 4B)

Endpoint:

```text
POST /api/v1/profiles/upload
Authorization: Bearer <admin-token>
Content-Type: multipart/form-data; field name = "file"
```

Admin-only. Body limit 500 MB (set both at the action and globally via `FormOptions`). Implementation in [`Services/CsvIngestionService.cs`](src/HngStageOne.Api/Services/CsvIngestionService.cs).

### Required CSV columns

`name`, `gender`, `age`, `country_id` (2-letter ISO).

Optional columns are filled in when omitted:

- `country_name` → resolved from `CountryLookup`
- `age_group` → derived from `AgeGroupClassifier`
- `gender_probability`, `country_probability` → default to `1.0`

### Pipeline

1. **Stream** the upload through `StreamReader` + `CsvHelper.CsvReader` — one row at a time, bounded memory regardless of file size. The whole file is never loaded.
2. **Validate the header**. Required columns are checked once. Missing required columns short-circuit with `status = "error"` and no DB writes.
3. **Per-row validation**. Each row is wrapped in `try/catch`. A bad row is tagged with one of:
   - `missing_fields` — required field empty
   - `invalid_gender` — not `male` / `female`
   - `invalid_age` — not numeric, negative, or > 120
   - `invalid_country` — not a 2-letter alphabetic ISO code
   - `malformed` — exception during row parsing (broken encoding, ragged columns)
   - `duplicate_name` — already in DB or already seen earlier in the same file
4. **Batch** at 1 000 rows. Each batch:
   - Single round-trip `WHERE Name IN (...)` to find duplicates already in the DB
   - Filters them out, increments the `duplicate_name` counter
   - `AddRange` + `SaveChangesAsync` — EF Core 9 batches inserts into a few SQL statements, never one-per-row
5. **No outer transaction** wraps the upload. Each batch commits independently — if batch 17 fails, batches 1–16 stay committed (the brief explicitly requires this).
6. **Cache invalidation** at the end. `IProfileService.InvalidateReadCachesAsync` bumps `profiles:list` and `profiles:export`.

The service depends on `IDbContextFactory<AppDbContext>`. Each batch gets a fresh context from the pool, completely independent of the request-scoped one. Combined with WAL, this is what allows the upload to run without blocking concurrent reads. Concurrent uploads are supported because the service holds no shared mutable state.

### Response shape

Matches the brief verbatim:

```json
{
  "status": "success",
  "total_rows": 50000,
  "inserted": 48231,
  "skipped": 1769,
  "reasons": {
    "duplicate_name": 1203,
    "invalid_age": 312,
    "missing_fields": 254
  }
}
```

### Failure handling

| Failure | Behaviour |
|---|---|
| Header missing or unreadable | `status = "error"`, no rows processed, no DB writes |
| Required column missing | `status = "error"`, `reasons.missing_required_columns = 1` |
| Single bad row | counted in the matching `reasons` bucket; pipeline continues |
| Duplicate within file | first occurrence wins; subsequent rows count as `duplicate_name` |
| Duplicate against DB | counted in `duplicate_name`; existing row untouched |
| Whole batch insert fails (e.g. unique-name race on concurrent uploads) | logged, every row in that batch counted as `duplicate_name`; pipeline continues |
| Other batch error | counted as `batch_failed`; pipeline continues |
| Client cancels mid-upload | `OperationCanceledException` propagates; batches committed before cancellation stay |

## API Versioning And Pagination

Profile requests must include:

```text
X-API-Version: 1
```

Requests without the header return `400 Bad Request`.

The canonical Stage 3 profile routes live under `/api/profiles`; `/api/v1/profiles` remains available for compatibility.

The list/search response uses:

```json
{
  "status": "success",
  "page": 1,
  "limit": 10,
  "total": 2026,
  "total_pages": 203,
  "links": {
    "self": "/api/profiles?page=1&limit=10",
    "next": "/api/profiles?page=2&limit=10",
    "prev": null
  },
  "data": []
}
```

## CSV Export

```text
GET /api/profiles/export?format=csv
GET /api/v1/profiles/export.csv
```

Supports the same filters, sorting, and natural-language `q=` parameter as listing. Export results are cached per canonical key in `profiles:export` for 60 s.

## CLI Usage

From the sibling `stage-3-cli` repo:

```bash
dotnet pack
dotnet tool install --global --add-source ./bin/Release Insighta.Cli
insighta config set-backend https://your-backend-url.com
insighta login
insighta profiles list --gender male --page 1 --limit 10
insighta profiles search "young males from nigeria"
insighta profiles export profiles.csv
```

## Environment Variables

Copy `.env.example` and fill in:

```bash
Auth__BackendPublicUrl=
Auth__WebPortalUrl=
Auth__AdminGitHubUsernames=
Auth__OAuthSessionMinutes=10
ALLOWED_ORIGINS=
GitHub__ClientId=
GitHub__ClientSecret=
Jwt__SigningKey=
Jwt__AccessTokenMinutes=3
Jwt__RefreshTokenMinutes=5
RateLimit__AuthPermitLimit=10
RateLimit__ApiPermitLimit=60
RateLimit__WindowMinutes=1
ExternalApis__GenderizeBaseUrl=https://api.genderize.io
ExternalApis__AgifyBaseUrl=https://api.agify.io
ExternalApis__NationalizeBaseUrl=https://api.nationalize.io
```

GitHub OAuth callback:

```text
https://your-backend-url.com/api/v1/auth/github/callback
```

## Local Run

```bash
dotnet build src/HngStageOne.Api/HngStageOne.Api.csproj
dotnet run --project src/HngStageOne.Api/HngStageOne.Api.csproj
```

Migrations (including the Stage 4B composite indexes) run automatically at startup. SQLite WAL pragmas are applied on the same path.

Swagger in development:

```text
http://localhost:5048/swagger
```

Docker:

```bash
docker compose up --build
```

## Tests

```bash
dotnet test
```

Stage 4B tests added on top of the existing suite:

- [`CanonicalQueryKeyTests.cs`](tests/HngStageOne.Api.Tests/CanonicalQueryKeyTests.cs) — equivalence of differently-phrased queries, casing, paging fields
- [`CsvIngestionServiceTests.cs`](tests/HngStageOne.Api.Tests/CsvIngestionServiceTests.cs) — header errors, per-row validation, dedupe (in-file and against DB), batch flush, partial-failure resilience, large stream of 2 500 rows
- [`NaturalLanguageProfileQueryParserTests.cs`](tests/HngStageOne.Api.Tests/NaturalLanguageProfileQueryParserTests.cs) — extended range, prepositional, and demonym phrasings
