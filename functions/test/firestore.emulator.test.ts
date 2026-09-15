import { assertFails, assertSucceeds, initializeTestEnvironment, RulesTestEnvironment } from "@firebase/rules-unit-testing";
import { deleteApp, initializeApp, App } from "firebase-admin/app";
import { getFirestore, Firestore } from "firebase-admin/firestore";
import { doc, getDoc, setDoc } from "firebase/firestore";
import { readFileSync } from "node:fs";
import { join } from "node:path";
import { afterAll, beforeAll, beforeEach, describe, expect, it } from "vitest";
import { ArgumentPayload } from "../src/content";
import { createFirestoreDependencies, dayBucket } from "../src/firestoreAdapters";

const projectId = "demo-gft";
const argument: ArgumentPayload = {
  speech: "Ou você respeita os deuses, ou você é como Sócrates. Não existe meio-termo, menina.",
  fallacy_id: "false_dilemma",
  premises: ["Só existem duas posições", "Quem duvida é como Sócrates"],
  weak_premise: 0,
  tone: "theatrical"
};

let environment: RulesTestEnvironment;
let adminApp: App;
let db: Firestore;

beforeAll(async () => {
  environment = await initializeTestEnvironment({
    projectId,
    firestore: { rules: readFileSync(join(__dirname, "..", "..", "firestore.rules"), "utf8") }
  });
  adminApp = initializeApp({ projectId }, "emulator-admin");
  db = getFirestore(adminApp);
});

afterAll(async () => {
  await environment.cleanup();
  await deleteApp(adminApp);
});

beforeEach(async () => {
  await environment.clearFirestore();
});

describe("security rules", () => {
  it("lets a player read and write only their own save and duel progress", async () => {
    const alice = environment.authenticatedContext("alice").firestore();
    const bob = environment.authenticatedContext("bob").firestore();
    const anonymous = environment.unauthenticatedContext().firestore();

    await assertSucceeds(setDoc(doc(alice, "saves/alice"), { actFlag: 1, timestamp: 1 }));
    await assertSucceeds(getDoc(doc(alice, "saves/alice")));
    await assertSucceeds(setDoc(doc(alice, "progress/alice/duels/batilo"), { won: true, victory: "maieutics" }));

    await assertFails(getDoc(doc(bob, "saves/alice")));
    await assertFails(setDoc(doc(bob, "saves/alice"), { actFlag: 2 }));
    await assertFails(getDoc(doc(anonymous, "saves/alice")));
    await assertFails(setDoc(doc(bob, "progress/alice/duels/batilo"), { won: false }));
  });

  it("keeps server collections closed to clients", async () => {
    const alice = environment.authenticatedContext("alice").firestore();

    await assertFails(getDoc(doc(alice, "argument_cache/any")));
    await assertFails(setDoc(doc(alice, "config/runtime"), { llm_enabled: true }));
    await assertFails(getDoc(doc(alice, "usage/2026-09-15")));
  });
});

describe("firestore adapters", () => {
  const now = new Date("2026-09-15T12:30:00Z");

  it("reads the kill switch, defaulting to enabled", async () => {
    const deps = createFirestoreDependencies(db, { rateLimitPerHour: 2, cacheTtlDays: 90 });

    expect(await deps.isLlmEnabled()).toBe(true);
    await db.doc("config/runtime").set({ llm_enabled: false });
    expect(await deps.isLlmEnabled()).toBe(false);
  });

  it("counts calls per uid per hour in a transaction", async () => {
    const deps = createFirestoreDependencies(db, { rateLimitPerHour: 2, cacheTtlDays: 90 });

    expect(await deps.consumeRateLimit("alice", now)).toBe(true);
    expect(await deps.consumeRateLimit("alice", now)).toBe(true);
    expect(await deps.consumeRateLimit("alice", now)).toBe(false);
    expect(await deps.consumeRateLimit("bob", now)).toBe(true);
    expect(await deps.consumeRateLimit("alice", new Date("2026-09-15T13:05:00Z"))).toBe(true);
  });

  it("stores the shared cache with a ninety day expiry", async () => {
    const deps = createFirestoreDependencies(db, { rateLimitPerHour: 60, cacheTtlDays: 90 });

    expect(await deps.readCache("key", now)).toBeNull();
    await deps.writeCache("key", argument, now);

    expect(await deps.readCache("key", now)).toEqual(argument);
    expect(await deps.readCache("key", new Date(now.getTime() + 91 * 24 * 60 * 60 * 1000))).toBeNull();
  });

  it("aggregates daily usage for the dashboard", async () => {
    const deps = createFirestoreDependencies(db, { rateLimitPerHour: 60, cacheTtlDays: 90 });

    await deps.recordUsage({ uid: "alice", cacheHit: false, inputTokens: 500, outputTokens: 100, costUsd: 0.001, at: now });
    await deps.recordUsage({ uid: "bob", cacheHit: true, inputTokens: 0, outputTokens: 0, costUsd: 0, at: now });

    const usage = (await db.doc(`usage/${dayBucket(now)}`).get()).data();
    expect(usage).toMatchObject({ requests: 2, openai_calls: 1, cache_hits: 1, input_tokens: 500, output_tokens: 100 });
    expect(usage?.cost_usd).toBeCloseTo(0.001);
  });
});
