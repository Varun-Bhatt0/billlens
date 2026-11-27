# OCR benchmark results

Held-out test set: 200 synthetic bills generated with `python tools/ocr-benchmark/generate_bills.py --count 200 --seed 2026`
(receipts, invoices, utility and pharmacy bills; 4 degradation levels from clean scans to low-resolution, noisy,
rotated phone photos). The pipeline was tuned on a separate development set (seed 7), never on this one.

Engine: tesseract 5.3.4, English model. Machine: 2-core Linux container, 2 bills in parallel.
A field counts as correct only if it matches exactly (bill number, ISO date, amount to the paisa).

Reproduce:

```
python tools/ocr-benchmark/generate_bills.py --count 200 --seed 2026 --out tools/ocr-benchmark/test
dotnet run -c Release --project tools/OcrBenchmark -- --data tools/ocr-benchmark/test --mode both
```

## Baseline (original pipeline)

| Subset | Bills | Bill number | Date | Amount | All three correct | Avg time |
|---|---:|---:|---:|---:|---:|---:|
| **All** | 200 | 49.5% | 30.0% | 2.5% | 1.0% | 330 ms |
| clean degradation | 52 | 55.8% | 38.5% | 1.9% | 1.9% | 356 ms |
| mild degradation | 52 | 75.0% | 38.5% | 3.8% | 0.0% | 428 ms |
| moderate degradation | 48 | 64.6% | 41.7% | 4.2% | 2.1% | 324 ms |
| heavy degradation | 48 | 0.0% | 0.0% | 0.0% | 0.0% | 204 ms |
| invoice | 50 | 28.0% | 30.0% | 2.0% | 0.0% | 333 ms |
| pharmacy | 50 | 56.0% | 42.0% | 8.0% | 4.0% | 325 ms |
| receipt | 50 | 42.0% | 28.0% | 0.0% | 0.0% | 280 ms |
| utility | 50 | 72.0% | 20.0% | 0.0% | 0.0% | 384 ms |

Field accuracy: **27.3%**. Errors: 0.

## Improved pipeline

| Subset | Bills | Bill number | Date | Amount | All three correct | Avg time |
|---|---:|---:|---:|---:|---:|---:|
| **All** | 200 | 83.0% | 87.0% | 83.5% | 77.5% | 657 ms |
| clean degradation | 52 | 100.0% | 100.0% | 100.0% | 100.0% | 494 ms |
| mild degradation | 52 | 100.0% | 100.0% | 96.2% | 96.2% | 560 ms |
| moderate degradation | 48 | 97.9% | 93.8% | 95.8% | 89.6% | 484 ms |
| heavy degradation | 48 | 31.3% | 52.1% | 39.6% | 20.8% | 1114 ms |
| invoice | 50 | 82.0% | 90.0% | 84.0% | 80.0% | 614 ms |
| pharmacy | 50 | 88.0% | 94.0% | 90.0% | 86.0% | 573 ms |
| receipt | 50 | 80.0% | 72.0% | 74.0% | 64.0% | 761 ms |
| utility | 50 | 82.0% | 92.0% | 86.0% | 80.0% | 681 ms |

Field accuracy: **84.5%**. Errors: 0.

Confidence: fields marked confident (>= 0.6) are correct 95.8% of the time (528 fields); fields marked unsure are correct 5.0% (20 fields).

<details><summary>Misses</summary>

| id | template | severity | field | expected | got |
|---|---|---:|---|---|---|
| 012 | receipt | 3 | number | R41-JM967 |  |
| 012 | receipt | 3 | date | 2026-01-08 |  |
| 012 | receipt | 3 | amount | 23688.13 | 4208.26 |
| 014 | utility | 3 | number | GST/25-24/777 |  |
| 014 | utility | 3 | date | 2025-02-12 | 2025-03-04 |
| 014 | utility | 3 | amount | 21049.13 | 22069.13 |
| 028 | receipt | 3 | number | R28-GP995 |  |
| 028 | receipt | 3 | date | 2025-08-06 |  |
| 028 | receipt | 3 | amount | 3703.92 |  |
| 029 | invoice | 3 | number | GST/24-25/6505 |  |
| 029 | invoice | 3 | date | 2024-09-15 |  |
| 029 | invoice | 3 | amount | 36689.22 | 66088.22 |
| 030 | utility | 3 | number | R83-DV472 |  |
| 030 | utility | 3 | amount | 14430.19 | 687.15 |
| 031 | pharmacy | 3 | number | INV-2026/0042 |  |
| 031 | pharmacy | 3 | date | 2025-06-08 | 2024-06-08 |
| 031 | pharmacy | 3 | amount | 27739.82 | 10006.44 |
| 040 | receipt | 2 | number | INV-2026/7783 |  |
| 044 | receipt | 3 | date | 2025-06-18 |  |
| 044 | receipt | 3 | amount | 8077.66 | 8977.66 |
| 045 | invoice | 3 | number | 5665 |  |
| 045 | invoice | 3 | date | 2024-02-27 | 2004-10-20 |
| 045 | invoice | 3 | amount | 13162.46 | 808 |
| 046 | utility | 3 | amount | 11363.55 | 11263.55 |
| 047 | pharmacy | 3 | number | GJ52713181 |  |
| 047 | pharmacy | 3 | date | 2025-06-13 |  |
| 047 | pharmacy | 3 | amount | 34591.97 | 3290.95 |
| 060 | receipt | 3 | number | GJ49784785 |  |
| 060 | receipt | 3 | date | 2025-10-08 |  |
| 060 | receipt | 3 | amount | 16476.38 |  |
| 061 | invoice | 3 | number | R31-AS544 |  |
| 061 | invoice | 3 | date | 2024-12-05 |  |
| 061 | invoice | 3 | amount | 35013.53 | 33012.53 |
| 077 | invoice | 3 | number | INV-2025/1780 |  |
| 077 | invoice | 3 | amount | 38956.40 | 34782.5 |
| 078 | utility | 3 | number | INV-2023/9679 |  |
| 079 | pharmacy | 3 | number | GJ57159603 | GJ57159609 |
| 079 | pharmacy | 3 | amount | 45657.52 | 48657.52 |
| 092 | receipt | 3 | number | INV-2024/7722 |  |
| 092 | receipt | 3 | date | 2025-08-10 | 2025-08-12 |
| 092 | receipt | 3 | amount | 2800.80 | 2808.86 |
| 093 | invoice | 3 | number | R17-EU647 |  |
| 093 | invoice | 3 | amount | 57560.25 | 57360.25 |
| 094 | utility | 3 | number | 724542 |  |
| 095 | pharmacy | 3 | number | R88-HN158 |  |
| 108 | receipt | 3 | number | GST/25-26/5029 |  |
| 108 | receipt | 3 | date | 2026-01-20 |  |
| 108 | receipt | 3 | amount | 28715.48 |  |
| 110 | utility | 3 | number | 222474 |  |
| 110 | utility | 3 | date | 2025-03-30 | 2025-03-03 |
| 110 | utility | 3 | amount | 30643.87 |  |
| 124 | receipt | 3 | number | GST/25-26/7222 | OFT/2S |
| 124 | receipt | 3 | date | 2024-06-30 |  |
| 124 | receipt | 3 | amount | 14400.64 |  |
| 125 | invoice | 3 | number | R74-BP784 | R74-BP |
| 127 | pharmacy | 3 | number | R81-DT928 |  |
| 127 | pharmacy | 3 | amount | 25698.72 | 25608.72 |
| 132 | receipt | 1 | amount | 29772.83 | 83 |
| 136 | receipt | 2 | amount | 29298.10 | 298.1 |
| 140 | receipt | 3 | date | 2024-02-03 |  |
| 141 | invoice | 3 | number | GST/25-24/7129 | GS1/25-24/7129 |
| 142 | utility | 3 | number | DL45910966 |  |
| 142 | utility | 3 | date | 2026-04-05 | 2026-04-17 |
| 142 | utility | 3 | amount | 11305.57 | 616.43 |
| 143 | pharmacy | 3 | date | 2024-11-07 |  |
| 152 | receipt | 2 | date | 2024-11-02 |  |
| 152 | receipt | 2 | amount | 15415.48 | 415.48 |
| 156 | receipt | 3 | number | R64-JP541 |  |
| 156 | receipt | 3 | date | 2024-03-14 |  |
| 156 | receipt | 3 | amount | 31657.93 | 22.28 |
| 157 | invoice | 3 | date | 2025-07-31 |  |
| 157 | invoice | 3 | amount | 34310.33 | 21331.75 |
| 158 | utility | 3 | number | GST/25-24/2769 | GST/I25-24/2769 |
| 158 | utility | 3 | amount | 39750.61 | 33608.96 |
| 168 | receipt | 2 | date | 2026-04-03 |  |
| 172 | receipt | 3 | number | GST/24-26/9504 |  |
| 172 | receipt | 3 | date | 2026-01-19 |  |
| 173 | invoice | 3 | number | GST/25-24/7667 | GST/28-24/7667 |
| 173 | invoice | 3 | amount | 62863.00 | 15250.55 |
| 174 | utility | 3 | number | 511217 |  |
| 184 | receipt | 2 | date | 2026-04-13 |  |
| 188 | receipt | 3 | number | TXN95609070 |  |
| 188 | receipt | 3 | date | 2025-03-13 |  |
| 188 | receipt | 3 | amount | 17318.41 | 919.43 |
| 189 | invoice | 3 | number | TXN70679569 |  |
| 189 | invoice | 3 | date | 2025-11-21 |  |
| 189 | invoice | 3 | amount | 53225.48 | 50690.93 |
| 190 | utility | 3 | number | TXN67976028 |  |
| 190 | utility | 3 | date | 2026-02-12 | 2026-02-17 |
| 190 | utility | 3 | amount | 24234.53 | 21857.97 |
| 191 | pharmacy | 3 | number | TXN19913350 |  |
| 191 | pharmacy | 3 | amount | 39413.06 | 6215.76 |
| 196 | receipt | 1 | amount | 24381.28 | 381.28 |

</details>

