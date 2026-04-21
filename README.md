# HNG Stage 2 Backend - Queryable Intelligence Engine

## Overview

This project upgrades the original stage-one demographic profile API into a queryable intelligence engine for Insighta Labs. It now supports:

- advanced filtering with combinable conditions
- sorting and pagination
- rule-based natural language search
- idempotent seeding of the provided `seed_profiles.json` dataset

The API is built with ASP.NET Core, Entity Framework Core, and SQLite.

## What Changed

The stage-two implementation adds these production-oriented features:

- `GET /api/profiles` now supports advanced filters, sorting, and pagination
- `GET /api/profiles/search` accepts plain-English demographic queries
- the `profiles` schema now matches the required stage-two structure
- startup seeding loads the 2026 provided profiles without creating duplicates on reruns
- all list responses now use the required `{ status, page, limit, total, data }` format
- validation and error handling now return standardized automated-grading-friendly payloads

## Profiles Schema

The `Profiles` table is aligned to the required fields:

| Field | Type | Notes |
| --- | --- | --- |
| `id` | UUID v7 | Primary key |
| `name` | VARCHAR + UNIQUE | Full name |
| `gender` | VARCHAR | `male` or `female` |
| `gender_probability` | FLOAT | Confidence score |
| `age` | INT | Exact age |
| `age_group` | VARCHAR | `child`, `teenager`, `adult`, `senior` |
| `country_id` | VARCHAR(2) | ISO country code |
| `country_name` | VARCHAR | Full country name |
| `country_probability` | FLOAT | Confidence score |
| `created_at` | TIMESTAMP | UTC timestamp |

## Seeding

The repo includes the required dataset in [seed_profiles.json](/c:/Users/User/Desktop/HNG%20INTERNSHIP/HNG-BACKEND/hng-task-one/seed_profiles.json).

- the file contains exactly 2026 profiles
- the application checks for `seed_profiles.json` at startup
- rerunning startup seeding does not insert duplicates because seeding checks existing names before insert

## API Endpoints

### `POST /api/profiles`

Creates a profile from external demographic APIs. Duplicate names return the existing profile instead of creating another row.

### `GET /api/profiles/{id}`

Returns a single profile by UUID.

### `DELETE /api/profiles/{id}`

Deletes a single profile.

### `GET /api/profiles`

Supports these query parameters:

- `gender`
- `age_group`
- `country_id`
- `min_age`
- `max_age`
- `min_gender_probability`
- `min_country_probability`
- `sort_by` = `age` | `created_at` | `gender_probability`
- `order` = `asc` | `desc`
- `page` default `1`
- `limit` default `10`, max `50`

Example:

```text
/api/profiles?gender=male&country_id=NG&min_age=25&sort_by=age&order=desc&page=1&limit=10
```

Example response:

```json
{
  "status": "success",
  "page": 1,
  "limit": 10,
  "total": 2026,
  "data": [
    {
      "id": "01964f9e-bf74-7a91-a1ce-6df7c57e0f0b",
      "name": "Bayo Ouédraogo",
      "gender": "male",
      "gender_probability": 0.99,
      "age": 27,
      "age_group": "adult",
      "country_id": "NG",
      "country_name": "Nigeria",
      "country_probability": 0.81,
      "created_at": "2026-04-21T12:00:00.0000000Z"
    }
  ]
}
```

### `GET /api/profiles/search`

Supports rule-based natural language parsing through the `q` parameter.

Example:

```text
/api/profiles/search?q=young males from nigeria&page=1&limit=10
```

Supported mappings include:

- `young males` -> `gender=male`, `min_age=16`, `max_age=24`
- `females above 30` -> `gender=female`, `min_age=30`
- `people from angola` -> `country_id=AO`
- `adult males from kenya` -> `gender=male`, `age_group=adult`, `country_id=KE`
- `male and female teenagers above 17` -> `age_group=teenager`, `min_age=17`

If a query cannot be interpreted, the API returns:

```json
{
  "status": "error",
  "message": "Unable to interpret query"
}
```

## Validation and Errors

All errors use this format:

```json
{
  "status": "error",
  "message": "<error message>"
}
```

Status codes:

- `400 Bad Request` -> missing or empty parameter
- `422 Unprocessable Entity` -> invalid query parameters
- `404 Not Found` -> profile not found
- `500 Internal Server Error` -> unexpected server failure
- `502 Bad Gateway` -> invalid upstream API response

## Local Run

```bash
dotnet build src/HngStageOne.Api/HngStageOne.Api.csproj
dotnet run --project src/HngStageOne.Api/HngStageOne.Api.csproj
```

On startup the app will:

1. apply migrations
2. seed `seed_profiles.json` if present
3. expose the API locally

## Tests

Focused tests were added for:

- natural language parsing
- query validation
- seed file record count

Run locally with:

```bash
dotnet test
```

## Deployment Notes

For production deployment:

1. publish or containerize the app
2. ensure the SQLite database path is persistent
3. keep `seed_profiles.json` alongside the deployed app or adjust startup seed path
4. allow the app to run migrations on startup
5. smoke-test `/api/profiles` and `/api/profiles/search` after deployment

## Important Notes

- CORS is configured with `Access-Control-Allow-Origin: *`
- timestamps are returned in UTC ISO 8601 format
- IDs are generated with UUID v7
- list queries are executed server-side through `IQueryable` filtering, sorting, counting, and paging instead of loading the whole table into memory
