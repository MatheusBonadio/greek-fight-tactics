import { describe, expect, it } from "vitest";
import { formatUsageTable, isDay, summarizeUsage, toUsageDay, UsageDay } from "../src/usageReport";

function day(overrides: Partial<UsageDay> & { day: string }): UsageDay {
  return { requests: 0, openaiCalls: 0, cacheHits: 0, inputTokens: 0, outputTokens: 0, costUsd: 0, ...overrides };
}

describe("summarizeUsage", () => {
  it("orders days and computes the daily hit rate and cumulative cost", () => {
    const summary = summarizeUsage([
      day({ day: "2026-09-15", requests: 10, openaiCalls: 3, cacheHits: 7, costUsd: 0.003 }),
      day({ day: "2026-09-14", requests: 4, openaiCalls: 4, cacheHits: 0, costUsd: 0.004 })
    ]);

    expect(summary.rows.map((row) => row.day)).toEqual(["2026-09-14", "2026-09-15"]);
    expect(summary.rows[0].cacheHitRate).toBe(0);
    expect(summary.rows[1].cacheHitRate).toBe(0.7);
    expect(summary.rows[1].cumulativeCostUsd).toBeCloseTo(0.007);
    expect(summary.totals).toMatchObject({ requests: 14, openaiCalls: 7, cacheHits: 7 });
    expect(summary.totals.cacheHitRate).toBe(0.5);
  });

  it("returns zeroed totals when there is no usage", () => {
    const summary = summarizeUsage([]);

    expect(summary.rows).toEqual([]);
    expect(summary.totals).toMatchObject({ requests: 0, costUsd: 0, cacheHitRate: 0 });
  });
});

describe("toUsageDay", () => {
  it("maps Firestore counters and defaults missing ones to zero", () => {
    expect(toUsageDay("2026-09-15", { requests: 3, cache_hits: 1, cost_usd: 0.5, openai_calls: "2" })).toEqual(
      day({ day: "2026-09-15", requests: 3, cacheHits: 1, costUsd: 0.5 })
    );
  });
});

describe("formatUsageTable", () => {
  it("prints a header, one line per day and a total line", () => {
    const table = formatUsageTable(summarizeUsage([day({ day: "2026-09-15", requests: 4, cacheHits: 3, openaiCalls: 1, costUsd: 0.0012 })]));
    const lines = table.split("\n");

    expect(lines).toHaveLength(3);
    expect(lines[0]).toMatch(/^day\s+requests\s+openai\s+hits\s+hit_rate\s+cost_usd\s+cumulative_usd$/);
    expect(lines[1]).toMatch(/^2026-09-15\s+4\s+1\s+3\s+75\.0%\s+0\.0012\s+0\.0012$/);
    expect(lines[2]).toMatch(/^total/);
  });
});

describe("isDay", () => {
  it("accepts only yyyy-mm-dd", () => {
    expect(isDay("2026-09-15")).toBe(true);
    expect(isDay("2026-9-15")).toBe(false);
    expect(isDay("usage")).toBe(false);
  });
});
