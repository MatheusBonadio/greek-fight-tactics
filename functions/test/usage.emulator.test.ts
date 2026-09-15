import { App, deleteApp, initializeApp } from "firebase-admin/app";
import { Firestore, getFirestore } from "firebase-admin/firestore";
import { afterAll, beforeAll, describe, expect, it } from "vitest";
import { createFirestoreDependencies } from "../src/firestoreAdapters";
import { loadUsage, summarizeUsage } from "../src/usageReport";

let app: App;
let db: Firestore;

beforeAll(() => {
  app = initializeApp({ projectId: "demo-gft" }, "usage-report-admin");
  db = getFirestore(app);
});

afterAll(async () => {
  await deleteApp(app);
});

describe("usage report", () => {
  it("loads only the requested day range and summarizes it for the dashboard", async () => {
    const deps = createFirestoreDependencies(db, { rateLimitPerHour: 60, cacheTtlDays: 90 });
    await deps.recordUsage({ uid: "alice", cacheHit: false, inputTokens: 500, outputTokens: 100, costUsd: 0.002, at: new Date("2031-03-01T10:00:00Z") });
    await deps.recordUsage({ uid: "bob", cacheHit: true, inputTokens: 0, outputTokens: 0, costUsd: 0, at: new Date("2031-03-02T10:00:00Z") });
    await deps.recordUsage({ uid: "carol", cacheHit: false, inputTokens: 400, outputTokens: 90, costUsd: 0.001, at: new Date("2031-03-09T10:00:00Z") });

    const summary = summarizeUsage(await loadUsage(db, "2031-03-01", "2031-03-02"));

    expect(summary.rows.map((row) => row.day)).toEqual(["2031-03-01", "2031-03-02"]);
    expect(summary.totals).toMatchObject({ requests: 2, openaiCalls: 1, cacheHits: 1, inputTokens: 500, outputTokens: 100 });
    expect(summary.totals.cacheHitRate).toBe(0.5);
    expect(summary.rows[1].cumulativeCostUsd).toBeCloseTo(0.002);
  });

  it("rejects malformed day bounds", async () => {
    await expect(loadUsage(db, "yesterday", "2031-03-02")).rejects.toThrow(/yyyy-mm-dd/);
  });
});
