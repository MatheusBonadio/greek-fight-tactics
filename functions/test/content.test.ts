import { readdirSync, readFileSync, statSync } from "node:fs";
import { extname, join } from "node:path";
import { describe, expect, it } from "vitest";
import { buildContent, generatedContentPath, serializeContent } from "../scripts/sync-content.mjs";

const repositoryRoot = join(__dirname, "..", "..");
const textExtensions = new Set([".cs", ".json", ".asset", ".unity", ".prefab", ".asmdef", ".txt", ".md", ".inputactions", ".globalconfig"]);
const openAiKeyPattern = /sk-[A-Za-z0-9_-]{16,}/;

function* walk(directory: string): Generator<string> {
  for (const name of readdirSync(directory)) {
    const path = join(directory, name);
    if (statSync(path).isDirectory()) {
      yield* walk(path);
    } else if (textExtensions.has(extname(name))) {
      yield path;
    }
  }
}

describe("generated content", () => {
  it("is in sync with the Unity catalog, fallback bank and argument schema", () => {
    expect(readFileSync(generatedContentPath, "utf8")).toBe(serializeContent(buildContent()));
  });

  it("covers every catalog entry with two few-shot examples", () => {
    const content = buildContent();

    expect(content.entries).toHaveLength(20);
    for (const entry of content.entries) {
      expect(content.examples[entry.id]).toHaveLength(2);
    }
  });
});

describe("secrets", () => {
  it("never ships an OpenAI key inside the Unity project", () => {
    const offenders = [...walk(join(repositoryRoot, "Assets"))].filter((path) => openAiKeyPattern.test(readFileSync(path, "utf8")));

    expect(offenders).toEqual([]);
  });
});
