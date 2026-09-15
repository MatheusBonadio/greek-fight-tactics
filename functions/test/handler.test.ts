import { beforeEach, describe, expect, it, vi } from "vitest";
import { ArgumentPayload } from "../src/content";
import { Completion, costOf, Dependencies, handleGenerateArgument, HandlerConfig, LogEntry } from "../src/handler";

const config: HandlerConfig = {
  model: "test-model",
  maxTokens: 400,
  blockedTerms: ["palavrão"],
  inputPricePerMillionTokens: 1,
  outputPricePerMillionTokens: 4
};

const body = { opponent_id: "thrasybios", profile: "sophist", act: 1, fallacy_id: "false_dilemma", seed: 11 };

const generated: ArgumentPayload = {
  speech: "Ou você respeita os deuses, ou você é como Sócrates. Não existe meio-termo, menina.",
  fallacy_id: "false_dilemma",
  premises: ["Só existem duas posições", "Quem duvida é como Sócrates"],
  weak_premise: 0,
  tone: "theatrical"
};

function completion(payload: unknown, inputTokens = 500, outputTokens = 100): Completion {
  return { content: typeof payload === "string" ? payload : JSON.stringify(payload), inputTokens, outputTokens };
}

describe("handleGenerateArgument", () => {
  let deps: Dependencies;
  let logs: LogEntry[];

  beforeEach(() => {
    logs = [];
    deps = {
      verifyIdToken: vi.fn(async (token: string) => (token === "good-token" ? "uid-1" : null)),
      isLlmEnabled: vi.fn(async () => true),
      consumeRateLimit: vi.fn(async () => true),
      readCache: vi.fn(async () => null),
      writeCache: vi.fn(async () => undefined),
      complete: vi.fn(async () => completion(generated)),
      isFlagged: vi.fn(async () => false),
      recordUsage: vi.fn(async () => undefined),
      log: (entry) => logs.push(entry),
      now: () => new Date("2026-09-15T12:00:00Z")
    };
  });

  const call = (overrides: { method?: string; authorization?: string; body?: unknown } = {}) =>
    handleGenerateArgument(overrides.method ?? "POST", overrides.authorization ?? "Bearer good-token", overrides.body ?? body, deps, config);

  it("only accepts POST", async () => {
    expect((await call({ method: "GET" })).status).toBe(405);
  });

  it.each([undefined, "", "Basic abc", "Bearer bad-token"])("rejects the authorization header %j", async (authorization) => {
    const result = await handleGenerateArgument("POST", authorization, body, deps, config);

    expect(result.status).toBe(401);
    expect(deps.complete).not.toHaveBeenCalled();
  });

  it.each([
    [{ ...body, profile: "wizard" }],
    [{ ...body, act: 3 }],
    [{ ...body, fallacy_id: "made_up" }],
    [{ ...body, seed: 1.5 }],
    [{ ...body, opponent_id: "Not Valid" }],
    [{ ...body, topic: "x".repeat(121) }],
    ["not an object"]
  ])("rejects malformed request %j", async (invalid) => {
    expect((await call({ body: invalid })).status).toBe(400);
  });

  it("answers 204 without calling OpenAI when the kill switch is off", async () => {
    deps.isLlmEnabled = vi.fn(async () => false);

    expect((await call()).status).toBe(204);
    expect(deps.complete).not.toHaveBeenCalled();
    expect(deps.consumeRateLimit).not.toHaveBeenCalled();
  });

  it("answers 429 when the uid is over its hourly budget", async () => {
    deps.consumeRateLimit = vi.fn(async () => false);

    expect((await call()).status).toBe(429);
    expect(deps.complete).not.toHaveBeenCalled();
  });

  it("serves the shared cache before calling OpenAI", async () => {
    deps.readCache = vi.fn(async () => generated);

    const result = await call();

    expect(result.status).toBe(200);
    expect(result.headers).toEqual({ "X-Cache": "hit" });
    expect(deps.complete).not.toHaveBeenCalled();
    expect(deps.recordUsage).toHaveBeenCalledWith(expect.objectContaining({ cacheHit: true, costUsd: 0 }));
  });

  it("generates, validates, stores in the cache and logs the cost", async () => {
    const result = await call();

    expect(result.status).toBe(200);
    expect(result.body).toEqual(generated);
    expect(result.headers).toEqual({ "X-Cache": "miss" });
    expect(deps.writeCache).toHaveBeenCalledWith(expect.stringMatching(/^[0-9a-f]{32}$/), generated, expect.any(Date));
    expect(deps.recordUsage).toHaveBeenCalledWith(expect.objectContaining({ cacheHit: false, costUsd: costOf(completion(generated), config) }));
    expect(logs.some((entry) => entry.event === "openai_call" && entry.data.costUsd === 0.0009)).toBe(true);
  });

  it("sends the token cap and the strict schema to OpenAI", async () => {
    await call();

    const prompt = (deps.complete as ReturnType<typeof vi.fn>).mock.calls[0][0];
    expect(prompt.maxTokens).toBe(400);
    expect(prompt.schema.additionalProperties).toBe(false);
  });

  it("retries once on an unknown fallacy and then gives up with 204", async () => {
    deps.complete = vi.fn(async () => completion({ ...generated, fallacy_id: "invented" }));

    const result = await call();

    expect(result.status).toBe(204);
    expect(deps.complete).toHaveBeenCalledTimes(2);
    expect(deps.writeCache).not.toHaveBeenCalled();
  });

  it("accepts the retry when the second answer uses the requested fallacy", async () => {
    deps.complete = vi
      .fn()
      .mockResolvedValueOnce(completion({ ...generated, fallacy_id: "straw_man" }))
      .mockResolvedValueOnce(completion(generated));

    const result = await call();

    expect(result.status).toBe(200);
    expect(deps.complete).toHaveBeenCalledTimes(2);
  });

  it("never returns a payload that breaks the contract", async () => {
    deps.complete = vi.fn(async () => completion({ ...generated, weak_premise: 9 }));

    const result = await call();

    expect(result.status).toBe(204);
    expect(result.body).toBeUndefined();
    expect(deps.writeCache).not.toHaveBeenCalled();
  });

  it("treats non JSON output as invalid", async () => {
    deps.complete = vi.fn(async () => completion("não é json"));

    expect((await call()).status).toBe(204);
  });

  it("rejects blocked terms and flagged content with a log for review", async () => {
    deps.complete = vi.fn(async () => completion({ ...generated, speech: `${generated.speech} Um palavrao aqui.` }));
    expect((await call()).status).toBe(204);

    deps.complete = vi.fn(async () => completion(generated));
    deps.isFlagged = vi.fn(async () => true);
    expect((await call()).status).toBe(204);

    expect(logs.filter((entry) => entry.event === "content_rejected")).toHaveLength(2);
    expect(deps.writeCache).not.toHaveBeenCalled();
  });

  it("falls back with 204 when an upstream service throws", async () => {
    deps.complete = vi.fn(async () => {
      throw new Error("OpenAI unavailable");
    });

    const result = await call();

    expect(result.status).toBe(204);
    expect(logs.some((entry) => entry.event === "generation_failed")).toBe(true);
  });

  it("expects null from paradox entries", async () => {
    const paradox = { ...generated, fallacy_id: null, tone: "calm" };
    deps.complete = vi.fn(async () => completion(paradox));

    const result = await call({ body: { ...body, profile: "philosopher", fallacy_id: "liar" } });

    expect(result.status).toBe(200);
    expect((result.body as ArgumentPayload).fallacy_id).toBeNull();
  });
});
