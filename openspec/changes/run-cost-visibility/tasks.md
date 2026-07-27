# Tasks — run-cost-visibility

## 1. The reads

- [x] 1.1 `ListRuns` exposes tokens and cost (nullable); the exact-shape test updated
      deliberately.
- [x] 1.2 `GET /api/projects/{projectId}/runs/cost`: summed cost of reporting Runs plus the
      count of unknown ones, computed in SQL (design D2/D3).

## 2. Tests

- [x] 2.1 A reporting Run exposes its numbers; a non-reporting one exposes nulls.
- [x] 2.2 The total sums only reported Runs and counts the rest — asserted with a mix, since
      that is the only case where summing nulls as zero would look right.

## 3. The portal

- [x] 3.1 Cost column filled; unknown renders empty-value + word, zero renders `$0.00`
      (design D1); Run detail shows tokens; project page shows the total and its exclusions;
      money and token formatting per D4; catalog copy; lint + build.

## 4. Close-out

- [x] 4.1 CI's own filtered command locally; CI green.
