import { FieldPath, Firestore } from "firebase-admin/firestore";
import { collections } from "./firestoreAdapters";

export interface UsageDay {
  day: string;
  requests: number;
  openaiCalls: number;
  cacheHits: number;
  inputTokens: number;
  outputTokens: number;
  costUsd: number;
}

export interface UsageRow extends UsageDay {
  cacheHitRate: number;
  cumulativeCostUsd: number;
}

export interface UsageSummary {
  rows: UsageRow[];
  totals: UsageDay & { cacheHitRate: number };
}

const dayPattern = /^\d{4}-\d{2}-\d{2}$/;

export function isDay(value: string): boolean {
  return dayPattern.test(value);
}

export function toUsageDay(day: string, data: Record<string, unknown> | undefined): UsageDay {
  const counter = (key: string): number => (typeof data?.[key] === "number" ? (data[key] as number) : 0);
  return {
    day,
    requests: counter("requests"),
    openaiCalls: counter("openai_calls"),
    cacheHits: counter("cache_hits"),
    inputTokens: counter("input_tokens"),
    outputTokens: counter("output_tokens"),
    costUsd: counter("cost_usd")
  };
}

export function summarizeUsage(days: UsageDay[]): UsageSummary {
  const sorted = [...days].sort((left, right) => left.day.localeCompare(right.day));
  let cumulativeCostUsd = 0;
  const rows = sorted.map((day) => {
    cumulativeCostUsd += day.costUsd;
    return { ...day, cacheHitRate: rate(day.cacheHits, day.requests), cumulativeCostUsd };
  });

  const totals = sorted.reduce<UsageDay>(
    (sum, day) => ({
      day: "total",
      requests: sum.requests + day.requests,
      openaiCalls: sum.openaiCalls + day.openaiCalls,
      cacheHits: sum.cacheHits + day.cacheHits,
      inputTokens: sum.inputTokens + day.inputTokens,
      outputTokens: sum.outputTokens + day.outputTokens,
      costUsd: sum.costUsd + day.costUsd
    }),
    toUsageDay("total", undefined)
  );

  return { rows, totals: { ...totals, cacheHitRate: rate(totals.cacheHits, totals.requests) } };
}

export async function loadUsage(db: Firestore, fromDay: string, toDay: string): Promise<UsageDay[]> {
  if (!isDay(fromDay) || !isDay(toDay)) {
    throw new Error(`days must be yyyy-mm-dd, received ${fromDay} and ${toDay}`);
  }

  const snapshot = await db
    .collection(collections.usage)
    .where(FieldPath.documentId(), ">=", fromDay)
    .where(FieldPath.documentId(), "<=", toDay)
    .get();

  return snapshot.docs.map((document) => toUsageDay(document.id, document.data()));
}

export function formatUsageTable(summary: UsageSummary): string {
  const header = ["day", "requests", "openai", "hits", "hit_rate", "cost_usd", "cumulative_usd"];
  const lines = [
    ...summary.rows.map((row) => cells(row, row.cumulativeCostUsd)),
    cells(summary.totals, summary.totals.costUsd)
  ];
  const widths = header.map((title, column) => Math.max(title.length, ...lines.map((line) => line[column].length)));
  const render = (values: string[]): string => values.map((value, column) => (column === 0 ? value.padEnd(widths[column]) : value.padStart(widths[column]))).join("  ");
  return [render(header), ...lines.map(render)].join("\n");
}

function cells(day: UsageDay & { cacheHitRate: number }, cumulativeCostUsd: number): string[] {
  return [
    day.day,
    String(day.requests),
    String(day.openaiCalls),
    String(day.cacheHits),
    `${(day.cacheHitRate * 100).toFixed(1)}%`,
    day.costUsd.toFixed(4),
    cumulativeCostUsd.toFixed(4)
  ];
}

function rate(part: number, whole: number): number {
  return whole > 0 ? part / whole : 0;
}
