import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";
import { argumentSchema, refutableIds, tones } from "../src/content";
import { toStrictSchema, validateArgument } from "../src/contract";

const valid = {
  speech: "Todo homem justo é temido pelos injustos. Sócrates é temido. Logo, Sócrates é justo.",
  fallacy_id: "affirming_the_consequent",
  premises: ["Todo justo é temido pelos injustos", "Sócrates é temido"],
  weak_premise: 1,
  tone: "arrogant"
};

describe("validateArgument", () => {
  it("accepts a well formed argument and a paradox", () => {
    expect(validateArgument(valid).violation).toBe("none");
    expect(validateArgument({ ...valid, fallacy_id: null, tone: "calm" }).violation).toBe("none");
  });

  it.each([
    [null, "malformed"],
    [{ ...valid, weak_premise: "1" }, "malformed"],
    [{ ...valid, speech: "   " }, "missing_speech"],
    [{ ...valid, speech: "a".repeat(39) }, "speech_too_short"],
    [{ ...valid, speech: "a".repeat(301) }, "speech_too_long"],
    [{ ...valid, fallacy_id: "zeno_tortoise" }, "unknown_fallacy"],
    [{ ...valid, premises: [] }, "missing_premises"],
    [{ ...valid, premises: ["ok", " "] }, "empty_premise"],
    [{ ...valid, weak_premise: 2 }, "weak_premise_out_of_range"],
    [{ ...valid, tone: "furious" }, "unknown_tone"]
  ])("rejects %j with %s", (payload, violation) => {
    expect(validateArgument(payload).violation).toBe(violation);
  });

  it("uses the same limits as the Unity validator", () => {
    expect(validateArgument({ ...valid, speech: "a".repeat(40) }).violation).toBe("none");
    expect(validateArgument({ ...valid, speech: "a".repeat(300) }).violation).toBe("none");
  });
});

describe("argument schema", () => {
  it("is the schema versioned in docs", () => {
    const documented = JSON.parse(readFileSync(join(__dirname, "..", "..", "docs", "argument.schema.json"), "utf8"));
    expect(argumentSchema).toEqual(documented);
    expect(refutableIds).toHaveLength(15);
    expect(tones).toHaveLength(8);
  });

  it("drops keywords that strict structured outputs reject but keeps the shape", () => {
    const strict = toStrictSchema(argumentSchema) as Record<string, any>;
    const serialized = JSON.stringify(strict);

    expect(serialized).not.toMatch(/minLength|maxLength|minItems|minimum|\$schema/);
    expect(strict.additionalProperties).toBe(false);
    expect(strict.required).toEqual(["speech", "fallacy_id", "premises", "weak_premise", "tone"]);
    expect(strict.properties.fallacy_id.enum).toContain(null);
  });
});
