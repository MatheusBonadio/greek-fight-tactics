import content from "./generated/content.json";

export type Profile = "sophist" | "philosopher";

export interface CatalogEntry {
  id: string;
  category: "Informal" | "Formal" | "Paradox";
  displayName: string;
  description: string;
}

export interface ArgumentPayload {
  speech: string;
  fallacy_id: string | null;
  premises: string[];
  weak_premise: number;
  tone: string;
}

export const catalogEntries = content.entries as CatalogEntry[];
export const refutableIds: readonly string[] = content.refutableIds;
export const paradoxIds: readonly string[] = content.paradoxIds;
export const tones: readonly string[] = content.tones;
export const speechLimits = content.speech;
export const argumentSchema = content.schema as Record<string, unknown>;
export const examples = content.examples as Record<string, ArgumentPayload[]>;

export const sophistTones: readonly string[] = ["arrogant", "mocking", "cunning", "theatrical"];
export const philosopherTones: readonly string[] = ["calm", "solemn", "curious", "melancholic"];

export function findEntry(id: string): CatalogEntry | undefined {
  return catalogEntries.find((entry) => entry.id === id);
}

export function expectedFallacyId(catalogId: string): string | null | undefined {
  if (refutableIds.includes(catalogId)) {
    return catalogId;
  }

  return paradoxIds.includes(catalogId) ? null : undefined;
}
