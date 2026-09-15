import { describe, expect, it } from "vitest";
import { cacheKey } from "../src/cacheKey";
import { ArgumentRequest, buildPrompt, temperatureFor, toneFor } from "../src/prompt";

const config = { model: "test-model", maxTokens: 400 };

function request(overrides: Partial<ArgumentRequest> = {}): ArgumentRequest {
  return {
    opponentId: "thrasybios",
    opponentName: "Thrasybios",
    profile: "sophist",
    act: 1,
    catalogId: "false_dilemma",
    seed: 3,
    topic: "a piedade na ágora",
    ...overrides
  };
}

describe("buildPrompt", () => {
  it("gives sophists and philosophers different voices", () => {
    const sophist = buildPrompt(request(), config).messages[0].content;
    const philosopher = buildPrompt(request({ profile: "philosopher", opponentId: "eudoro", opponentName: "Eudoro" }), config).messages[0].content;

    expect(sophist).toMatch(/ornate, confident/);
    expect(philosopher).toMatch(/terse and precise/);
  });

  it("forbids declaring a winner, citing rules and breaking the fourth wall", () => {
    const system = buildPrompt(request(), config).messages[0].content;

    expect(system).toMatch(/Never declare a winner/);
    expect(system).toMatch(/Never mention game rules/);
    expect(system).toMatch(/Never break the fourth wall/);
  });

  it("injects opponent, act, topic and tone", () => {
    const prompt = buildPrompt(request({ act: 2, tone: "cunning" }), config);
    const context = prompt.messages[prompt.messages.length - 1].content;

    expect(prompt.messages[0].content).toMatch(/You are Thrasybios/);
    expect(context).toMatch(/Act: 2\./);
    expect(context).toMatch(/a piedade na ágora/);
    expect(context).toMatch(/Desired tone: cunning/);
  });

  it("adds two handwritten few-shot examples for the requested entry", () => {
    const prompt = buildPrompt(request(), config);
    const assistantExamples = prompt.messages.filter((message) => message.role === "assistant");

    expect(assistantExamples).toHaveLength(2);
    assistantExamples.forEach((message) => expect(JSON.parse(message.content).fallacy_id).toBe("false_dilemma"));
  });

  it("asks paradoxes for a null fallacy", () => {
    const prompt = buildPrompt(request({ catalogId: "liar", profile: "philosopher" }), config);

    expect(prompt.messages[prompt.messages.length - 1].content).toMatch(/Set fallacy_id to null/);
  });

  it("carries the strict schema, model and token cap", () => {
    const prompt = buildPrompt(request(), config);

    expect(prompt.model).toBe("test-model");
    expect(prompt.maxTokens).toBe(400);
    expect(JSON.stringify(prompt.schema)).not.toMatch(/minLength/);
  });

  it("rejects unknown catalog entries", () => {
    expect(() => buildPrompt(request({ catalogId: "made_up" }), config)).toThrow(/unknown catalog entry/);
  });
});

describe("temperatures and tones", () => {
  it.each([
    ["thrasybios", "sophist", 0.9],
    ["damaris", "sophist", 0.5],
    ["menexeno", "philosopher", 0.3],
    ["kritolaos", "sophist", 0.8],
    ["zeno", "philosopher", 0.4]
  ] as const)("%s (%s) uses temperature %d", (opponent, profile, temperature) => {
    expect(temperatureFor(opponent, profile)).toBe(temperature);
  });

  it("keeps the tone inside the profile palette", () => {
    expect(toneFor(request({ tone: "calm" }))).not.toBe("calm");
    expect(toneFor(request({ profile: "philosopher", tone: "calm" }))).toBe("calm");
  });
});

describe("cacheKey", () => {
  it("matches the Unity ArgumentDiskCache key formula", () => {
    const key = cacheKey("thrasybios", "ad_hominem", 7);

    expect(key).toHaveLength(32);
    expect(key).toBe(cacheKey("thrasybios", "ad_hominem", 7));
    expect(key).not.toBe(cacheKey("thrasybios", "ad_hominem", 8));
  });
});
