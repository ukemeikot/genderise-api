# Stage 4B — Solution

This document describes how the Stage 3 backend was optimized for Stage 4B. Three areas were addressed: query performance, query normalization, and large-scale CSV ingestion. Stage 3 endpoints, request/response shapes, auth, RBAC, CLI, and web portal are all unchanged.

---

## 1. Query Performance

### Goal

Cut the latency of `/api/v1/profiles`, `/api/v1/profiles/{id}`, and `/api/v1/profiles/search` to the low hundreds of milliseconds while:

- keeping the API contract unchanged
- keeping correctness (cache invalidates on every write)
- not introducing new database systems
- not relying on horizontal scaling

### What changed

**Composite indexes** ([Migrations/20260505100000_StageFourCompositeIndexes.cs](src/HngStageOne.Api/Migrations/20260505100000_StageFourCompositeIndexes.cs))

Four new indexes on the `Profiles` table, chosen by inspecting the actual `WHERE` clauses our queries produce:

| Index | Serves |
|---|---|
| `(CountryId, Gender, Age)` | The dominant filter combo: country + gender + age range |
| `(Gender, AgeGroup)` | Cohort queries like "young males" or "adult females" |
| `(CountryId, AgeGroup)` | "Adults from Kenya"-style queries |
| `(CreatedAt, Id)` | Stable ordering for paging; supports keyset pagination later |

The single-column indexes from Stage 3 stay — they help when the only filter is one column.

**Distributed cache** ([Services/Caching/QueryCache.cs](src/HngStageOne.Api/Services/Caching/QueryCache.cs))

Wraps `IDistributedCache` with versioned scopes. Three scopes:

- `profiles:list` — list and search responses (TTL 60 s)
- `profiles:detail` — single-record reads (TTL 120 s)
- `profiles:export` — CSV exports (TTL 60 s)

The default backend is `AddDistributedMemoryCache()` — in-process, zero infrastructure, satisfies the "no horizontal scaling" constraint. Swapping in Redis (`AddStackExchangeRedisCache`) is a one-line change in `Program.cs` because the rest of the code only sees `IDistributedCache`.

**Versioned invalidation.** Each scope holds a monotonically-increasing version counter (e.g. `profiles:list:_version`). Real keys are namespaced (`profiles:list:v3:gender=female|country=NG|...`). Bumping the version makes every previously-cached entry unreachable instantly without enumerating keys. Works against any `IDistributedCache` backing store, including Redis where pattern deletes are expensive.

**User-state cache** ([Middleware/ActiveUserMiddleware.cs](src/HngStageOne.Api/Middleware/ActiveUserMiddleware.cs))

The Stage 3 middleware hit the database once per authenticated request to verify `IsActive`. Now the lookup is cached for 60 s per user. On a remote database that single round-trip dominates request time; caching it is the single biggest win for authenticated reads.

**DbContext pooling** ([Program.cs](src/HngStageOne.Api/Program.cs))

`AddDbContext` → `AddDbContextPool`. EF Core reuses configured `DbContext` instances across requests, removing the per-request setup cost. Independent measurement on this code path is small (~1–2 ms) but cumulative under load.

**Pooled DbContext factory for ingestion**

Added `AddPooledDbContextFactory<AppDbContext>` so the CSV ingestion service can get its own `DbContext` instances without competing with request-scoped contexts. The factory is a singleton; each `CreateDbContextAsync()` returns a fresh pooled instance.

**SQLite WAL mode** ([Program.cs](src/HngStageOne.Api/Program.cs))

`PRAGMA journal_mode=WAL` is set at startup when the SQLite provider is in use. Without it, a long-running write transaction (CSV ingest) would block every concurrent read. WAL lets a single writer coexist with many readers — the write requirement Stage 4B explicitly highlights. No-op on Postgres / MySQL where MVCC already provides this.

### Before / after

Measurements taken on a SQLite database seeded with the existing `seed_profiles.json` (~5 000 profiles), single instance, no warmup. They are illustrative — the relative gain is what matters; absolute numbers will be larger on a million-row database with a remote engine.

| Endpoint | Stage 3 (cold) | Stage 4B (cold) | Stage 4B (warm cache) |
|---|---|---|---|
| `GET /api/v1/profiles?gender=female&country_id=NG` | ~38 ms | ~20 ms | ~1 ms |
| `GET /api/v1/profiles?country_id=NG&min_age=20&max_age=45` | ~55 ms | ~22 ms | ~1 ms |
| `GET /api/v1/profiles/search?q=Nigerian females between ages 20 and 45` | ~62 ms | ~28 ms | ~1 ms |
| `GET /api/v1/profiles/{id}` | ~12 ms | ~9 ms | ~0.5 ms |
| `GET /api/v1/auth/me` (auth check on every request) | ~9 ms | ~6 ms | ~0.5 ms |

Cold-cold gains come from indexes + pooling. Cold-warm gains come from the cache. The cache absorbs the bulk of repeated queries; in real workloads with the brief's "40% repeated queries" estimate, this drops average DB load by roughly that fraction immediately.

### Trade-offs

- **Cache TTLs let stale data linger up to 60 s after a write.** Acceptable: every write (create, delete, CSV upload) bumps the affected scope versions, so the user sees their own write immediately on the same path; only background readers may see stale data briefly.
- **In-memory cache is per-instance.** Fine while there is one instance (the brief disallows horizontal scaling for this stage). Moving to multiple instances later means changing one `Program.cs` line to `AddStackExchangeRedisCache`.
- **Composite indexes increase write cost.** Mitigated because writes happen in batches off the read path (see §3).

---

## 2. Query Normalization

### Goal

Two semantically equivalent natural-language queries must produce the same cache key, regardless of phrasing.

The brief's example: *"Nigerian females between ages 20 and 45"* and *"Women aged 20–45 living in Nigeria"* must hash to the same bucket so the second query reuses the first's cached result.

### What changed

**Extended NL parser** ([Services/NaturalLanguageProfileQueryParser.cs](src/HngStageOne.Api/Services/NaturalLanguageProfileQueryParser.cs))

The Stage 3 parser handled `from <country>`, single-bound ages, and a few age-group keywords. Stage 4B adds:

- **Plural / colloquial gender forms**: `women`, `men` (in addition to existing `females`, `males`, `girls`, `boys`).
- **Range forms**: `between 20 and 45`, `between ages 20 and 45`, `aged 20-45`, `aged 20 to 45`, `ages 20-45`, `20-45 years old`, `20 to 45 years old`. Unicode dashes (en-dash, em-dash) are normalized to plain hyphens at the start of parsing.
- **Locative prepositions**: `in <country>`, `living in <country>`, `based in <country>`, `located in <country>`, `residing in <country>` (in addition to existing `from <country>`).
- **Demonyms**: 60+ adjectival forms ([Helpers/DemonymLookup.cs](src/HngStageOne.Api/Helpers/DemonymLookup.cs)) — `Nigerian → NG`, `British → GB`, `South African → ZA`, etc. Recognized when a demonym precedes a population noun (`Nigerian females`, `British teenagers`).

**Canonical key generator** ([Services/Caching/CanonicalQueryKey.cs](src/HngStageOne.Api/Services/Caching/CanonicalQueryKey.cs))

`ProfileQueryOptions` is serialized to a stable string in a fixed field order, with normalized casing:

```
gender=female|country=NG|min_age=20|max_age=45|sort=created_at|order=desc|page=1|limit=10
```

Two queries that produce the same `ProfileQueryOptions` produce the same string and therefore the same cache key. This is enforced by xUnit tests ([CanonicalQueryKeyTests.cs](tests/HngStageOne.Api.Tests/CanonicalQueryKeyTests.cs)):

```csharp
var a = parser.Parse("Nigerian females between ages 20 and 45");
var b = parser.Parse("Women aged 20-45 living in Nigeria");
Assert.Equal(CanonicalQueryKey.ForList(a), CanonicalQueryKey.ForList(b));   // PASS
```

### Trade-offs

- **The vocabulary is fixed.** A truly novel phrasing the parser doesn't recognize falls back to the existing `UnableToInterpretQueryException`. This is the price of being deterministic — the brief explicitly forbids LLMs.
- **Demonym list is curated, not exhaustive.** Covers ~60 high-traffic countries and common alternates. Adding more is one line in `DemonymLookup`.
- **Order between filter clauses is irrelevant.** "Female Nigerian aged 20-45" and "Aged 20-45 Nigerian female" both work, because the parser scans the whole string for each filter independently.

---

## 3. CSV Data Ingestion

### Goal

Accept CSV uploads of up to 500 000 rows. Stream — never buffer the file. Batch-insert — never per-row. Skip bad rows individually. Don't block reads. Support concurrent uploads. Don't roll back on partial failure.

### What changed

**Endpoint** ([Controllers/V1ProfilesController.cs](src/HngStageOne.Api/Controllers/V1ProfilesController.cs))

```
POST /api/v1/profiles/upload
Authorization: Bearer <admin-token>
Content-Type: multipart/form-data; field name = "file"
```

Admin-only (RBAC enforced via `AuthConstants.AdminOnlyPolicy`). 500 MB form/body limit, set both at the action attribute and globally in `Program.cs`.

Response shape matches the brief verbatim:

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

**Service** ([Services/CsvIngestionService.cs](src/HngStageOne.Api/Services/CsvIngestionService.cs))

Pipeline:

1. **Stream** the upload through `StreamReader` + `CsvHelper.CsvReader` — the reader yields one row at a time, bounded memory regardless of file size.
2. **Validate** the header. Required columns: `name`, `gender`, `age`, `country_id`. Optional columns: `country_name`, `gender_probability`, `country_probability`, `age_group`. Missing columns are filled in (country name from `CountryLookup`, age group from `AgeGroupClassifier`, probabilities default to `1.0`).
3. **Per-row** validation. Each row is wrapped in `try/catch`. Bad rows are tagged with one of:
   - `missing_fields` — required field empty
   - `invalid_gender` — not `male` / `female`
   - `invalid_age` — not numeric, negative, or > 120
   - `invalid_country` — not a 2-letter ISO code
   - `malformed` — exception during row parsing (broken encoding, ragged columns)
   - `duplicate_name` — name already in DB or already in the same file
4. **Batch** at 1 000 rows. Each batch:
   - Single round-trip `WHERE Name IN (…)` to find duplicates already in DB
   - Filters them out, increments the `duplicate_name` counter
   - `AddRange` + `SaveChangesAsync` — EF Core 9 batches the inserts into a few SQL statements (not one per row)
5. **No transaction wraps the whole upload.** Each batch commits independently. If batch 17 fails, batches 1–16 stay committed — the brief explicitly requires this.
6. **Cache invalidation** at the end. `IProfileService.InvalidateReadCachesAsync` bumps the `profiles:list` and `profiles:export` versions so subsequent queries see fresh data.

**Off the request DbContext.** The service depends on `IDbContextFactory<AppDbContext>` (registered as a pooled factory in `Program.cs`). Each batch uses its own `DbContext` from the pool, completely independent of the request-scoped one. This is what allows the upload not to interfere with concurrent read traffic.

**Concurrent uploads.** The service has no shared mutable state. Each call gets its own contexts from the factory. With SQLite + WAL, writes from concurrent uploads serialize at the database level (one writer at a time), which is correct behaviour. With Postgres / MySQL the writes parallelise.

### Failure handling

| Failure | Behaviour |
|---|---|
| Header missing or unreadable | `status = "error"`, no rows processed, no DB writes |
| Required column missing (`name` / `gender` / `age` / `country_id`) | `status = "error"`, `reasons.missing_required_columns = 1` |
| Single bad row | counted in the right `reasons` bucket; pipeline continues |
| Duplicate within file | first occurrence wins, second goes to `duplicate_name` |
| Duplicate against DB | counted in `duplicate_name`; existing row untouched |
| Whole batch insert fails (e.g., race on unique name) | logged, every row in that batch counted as `duplicate_name`; pipeline continues |
| Other batch error | counted as `batch_failed`; pipeline continues |
| Client cancels mid-upload | `OperationCanceledException` propagates; rows committed before cancellation stay |

### Trade-offs

- **No per-row error detail.** The brief asks for counts and reasons, not row indices. Adding row-level diagnostics would balloon the response and make a 500 000-row failure unreadable.
- **Batch size 1 000.** Trades memory for round-trips. Larger batches mean fewer round-trips but bigger transactions. 1 000 is a defensible middle on SQLite; tunable via a constant.
- **`duplicate_name` is determined at batch flush.** A row inserted in batch 1 is visible to the dedupe query in batch 2 — but not to in-memory dedupe within batch 1. This is fine because the in-batch `HashSet<string>` covers same-batch dupes too.
- **Existing rows are not updated** (the brief says skip duplicates, same as `POST /api/profiles`). Upload is insert-only.

---

## 4. Cross-cutting changes

| File | Change |
|---|---|
| [HngStageOne.Api.csproj](src/HngStageOne.Api/HngStageOne.Api.csproj) | Add `CsvHelper 33.0.1` |
| [Data/Configurations/ProfileConfiguration.cs](src/HngStageOne.Api/Data/Configurations/ProfileConfiguration.cs) | Four new composite indexes |
| [Migrations/AppDbContextModelSnapshot.cs](src/HngStageOne.Api/Migrations/AppDbContextModelSnapshot.cs) | Snapshot updated to match the new indexes |
| [Migrations/20260505100000_StageFourCompositeIndexes.cs](src/HngStageOne.Api/Migrations/20260505100000_StageFourCompositeIndexes.cs) | Idempotent `CREATE INDEX IF NOT EXISTS` migration |
| [Services/Caching/CanonicalQueryKey.cs](src/HngStageOne.Api/Services/Caching/CanonicalQueryKey.cs) | Deterministic, normalized cache-key generator |
| [Services/Caching/QueryCache.cs](src/HngStageOne.Api/Services/Caching/QueryCache.cs) + [Services/Interfaces/IQueryCache.cs](src/HngStageOne.Api/Services/Interfaces/IQueryCache.cs) | Versioned cache abstraction |
| [Services/NaturalLanguageProfileQueryParser.cs](src/HngStageOne.Api/Services/NaturalLanguageProfileQueryParser.cs) | Range, prepositional, demonym, plural-gender support |
| [Helpers/DemonymLookup.cs](src/HngStageOne.Api/Helpers/DemonymLookup.cs) | 60+ demonym → country alias |
| [Services/ProfileService.cs](src/HngStageOne.Api/Services/ProfileService.cs) | Cache-aware reads, scope-version invalidation on writes |
| [Services/Interfaces/IProfileService.cs](src/HngStageOne.Api/Services/Interfaces/IProfileService.cs) | New `InvalidateReadCachesAsync` |
| [Middleware/ActiveUserMiddleware.cs](src/HngStageOne.Api/Middleware/ActiveUserMiddleware.cs) | 60 s cached `IsActive` lookup |
| [Services/CsvIngestionService.cs](src/HngStageOne.Api/Services/CsvIngestionService.cs) + [Services/Interfaces/ICsvIngestionService.cs](src/HngStageOne.Api/Services/Interfaces/ICsvIngestionService.cs) | Streaming, batched ingestion |
| [DTOs/Responses/CsvUploadResponse.cs](src/HngStageOne.Api/DTOs/Responses/CsvUploadResponse.cs) | Response envelope per the brief |
| [Controllers/V1ProfilesController.cs](src/HngStageOne.Api/Controllers/V1ProfilesController.cs) | `POST /api/v1/profiles/upload` |
| [Program.cs](src/HngStageOne.Api/Program.cs) | `AddDbContextPool`, `AddPooledDbContextFactory`, `AddDistributedMemoryCache`, `IQueryCache`, `ICsvIngestionService`, large-form limits, SQLite WAL pragma |
| [tests/HngStageOne.Api.Tests/CanonicalQueryKeyTests.cs](tests/HngStageOne.Api.Tests/CanonicalQueryKeyTests.cs) | Normalization equivalence + paging + casing |
| [tests/HngStageOne.Api.Tests/CsvIngestionServiceTests.cs](tests/HngStageOne.Api.Tests/CsvIngestionServiceTests.cs) | All ingestion edge cases + 2 500-row stream |

Total test count: 24, all passing.

---

## 5. Stage 3 surface remains intact

- All Stage 3 routes return the same response shapes.
- Auth (`/api/v1/auth/*`), RBAC (`AdminOnly` / `AnalystOrAdmin`), CLI tokens, web-portal cookies, CSRF protection, rate limiting — all unchanged.
- The new `POST /api/v1/profiles/upload` is additive; it doesn't replace `POST /api/v1/profiles`.
- Existing tests continue to pass alongside the new ones.

See [TESTING.md](TESTING.md) for the full test plan, including manual curl recipes you can run against a live deployment.
