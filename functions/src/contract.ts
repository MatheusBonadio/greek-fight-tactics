import { ArgumentPayload, refutableIds, speechLimits, tones } from "./content";

export type Violation =
  | "none"
  | "malformed"
  | "missing_speech"
  | "speech_too_short"
  | "speech_too_long"
  | "unknown_fallacy"
  | "missing_premises"
  | "empty_premise"
  | "weak_premise_out_of_range"
  | "unknown_tone";

export interface ValidationResult {
  violation: Violation;
  argument?: ArgumentPayload;
}

const strictModeUnsupportedKeywords = new Set(["minLength", "maxLength", "minItems", "maxItems", "minimum", "maximum", "pattern", "format", "$schema", "title", "description"]);

export function validateArgument(value: unknown): ValidationResult {
  if (!isRecord(value)) {
    return { violation: "malformed" };
  }

  const { speech, fallacy_id: fallacyId, premises, weak_premise: weakPremise, tone } = value;
  if (typeof speech !== "string" || (fallacyId !== null && typeof fallacyId !== "string") || !Number.isInteger(weakPremise) || typeof tone !== "string") {
    return { violation: "malformed" };
  }

  const trimmedLength = speech.trim().length;
  if (trimmedLength === 0) {
    return { violation: "missing_speech" };
  }

  if (trimmedLength < speechLimits.minLength) {
    return { violation: "speech_too_short" };
  }

  if (trimmedLength > speechLimits.maxLength) {
    return { violation: "speech_too_long" };
  }

  if (fallacyId !== null && !refutableIds.includes(fallacyId)) {
    return { violation: "unknown_fallacy" };
  }

  if (!Array.isArray(premises) || premises.length === 0) {
    return { violation: "missing_premises" };
  }

  if (premises.some((premise) => typeof premise !== "string" || premise.trim().length === 0)) {
    return { violation: "empty_premise" };
  }

  const weakIndex = weakPremise as number;
  if (weakIndex < 0 || weakIndex >= premises.length) {
    return { violation: "weak_premise_out_of_range" };
  }

  if (!tones.includes(tone)) {
    return { violation: "unknown_tone" };
  }

  return {
    violation: "none",
    argument: { speech, fallacy_id: fallacyId as string | null, premises: premises as string[], weak_premise: weakIndex, tone }
  };
}

export function toStrictSchema(schema: unknown): unknown {
  if (Array.isArray(schema)) {
    return schema.map(toStrictSchema);
  }

  if (!isRecord(schema)) {
    return schema;
  }

  return Object.fromEntries(
    Object.entries(schema)
      .filter(([key]) => !strictModeUnsupportedKeywords.has(key))
      .map(([key, child]) => [key, toStrictSchema(child)])
  );
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
