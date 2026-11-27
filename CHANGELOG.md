# Changelog

## 3.0.0 — November 2026

### OCR
- New pipeline in `Services/Ocr`: background flattening, deskew, text-height-based scaling, contrast stretch.
- Tesseract 5 LSTM with TSV output (word positions and confidences); words regrouped into visual rows.
- Multi-pass page segmentation (modes 6, then 4 and 11 only when the result isn't confident).
- New field parser with scored candidates.
  - Tolerates OCR misreads in labels and numbers.
  - Recognises month-name dates and Indian digit grouping.
  - Ignores due and printed dates, sub-totals, tax lines and account numbers.
  - Checks sub-total + tax and repairs a currency sign read as a digit.
- Per-field confidence shown on the review page ("check" badges, "matches sub-total + tax").
- Multi-page PDFs (first three pages) at 300 DPI.
- Synthetic benchmark (`tools/ocr-benchmark`, `tools/OcrBenchmark`). On 200 held-out bills, all-fields-correct
  went from 1.0% to 77.5% and field accuracy from 27.3% to 84.5%. CI runs it as a quality gate.

### Modernisation
- Front end: Bootstrap 5.3.9 and jQuery 3.8.0, served locally. Modernizr removed.
- New shared layout with a navigation bar and active-page highlighting. Every page was redesigned.
- Roslyn page compiler (CodeDom 4.1.0), C# `latest`.
- Async upload page: no request thread waits on Tesseract.
- `/health` endpoint for load balancers and uptime monitors.
- Tests and tools target .NET 10.
- Removed unused packages: Entity Framework, FriendlyUrls, Newtonsoft.Json, SQLite EF6/LINQ.
- Renamed throughout: "employee ID" (`employee_id`), app name BillLens, new logo.

## 2.0.0 — November 2026

### Fixed
- Upload → Review → Save flow (the review page used to open empty).
- Crashes from `async void` page handlers.
- PDF uploads left a temporary image locked.
- Dates sent to Oracle are now in the format the stored procedure expects; decimal amounts are accepted.
- Audit log column names; admin flag not recognised; 4 MB request limit blocking 5 MB uploads.

### Security
- Credentials moved to a gitignored `App_Data/secrets.config`.
- Account lockout; equal-time rejection of unknown accounts.
- Shared `SecurePage` base class: session bound to the user, `ViewStateUserKey`, admin checks.
- Upload file-signature checks; random stored file names.
- Security headers; server and version headers removed; HSTS in Release.
- Error page shows fixed messages and a reference ID only. Cookie values are no longer logged.
- Oracle.ManagedDataAccess 19.3.0 → 19.18.0 (CVE-2023-21893).

### Added
- Rate limiting (per IP and per user, sliding window, HTTP 429).
- Structured JSON Lines logging with request IDs and a separate security log.
- Versioned SQLite migrations, foreign keys, CHECK constraints, indexes, WAL, transactional saves.
- Oracle schema script with least-privilege grants.
- xUnit tests, GitHub Actions CI, Dependabot, `.editorconfig`.

### Removed
- Unused second data-access layer, duplicate configuration class, unused async methods, duplicate error page,
  unfinished dashboard, Visual Studio template files, and scripts left over from an earlier prototype.

## 1.0.0 — July 2025

First version: upload with OCR, review, manual entry, bill history, audit log, Oracle with SQLite fallback.
