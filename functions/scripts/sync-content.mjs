import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const functionsRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const repositoryRoot = join(functionsRoot, "..");
const examplesPerEntry = 2;

export const generatedContentPath = join(functionsRoot, "src", "generated", "content.json");

function readJson(relativePath) {
  return JSON.parse(readFileSync(join(repositoryRoot, relativePath), "utf8").replace(/^﻿/, ""));
}

export function buildContent() {
  const source = readJson("Assets/_Game/Data/Source/fallacies.json");
  const schema = readJson("docs/argument.schema.json");
  const entries = source.fallacies.map((fallacy) => ({
    id: fallacy.id,
    category: fallacy.category,
    displayName: fallacy.display_name,
    description: fallacy.description
  }));

  const examples = Object.fromEntries(
    entries.map((entry) => [entry.id, readJson(`Assets/_Game/Data/Fallback/${entry.id}.json`).slice(0, examplesPerEntry)])
  );

  return {
    entries,
    refutableIds: entries.filter((entry) => entry.category !== "Paradox").map((entry) => entry.id),
    paradoxIds: entries.filter((entry) => entry.category === "Paradox").map((entry) => entry.id),
    tones: schema.properties.tone.enum,
    speech: {
      minLength: schema.properties.speech.minLength,
      maxLength: schema.properties.speech.maxLength
    },
    schema,
    examples
  };
}

export function serializeContent(content) {
  return `${JSON.stringify(content, null, 2)}\n`;
}

if (process.argv[1] && fileURLToPath(import.meta.url) === process.argv[1]) {
  mkdirSync(dirname(generatedContentPath), { recursive: true });
  writeFileSync(generatedContentPath, serializeContent(buildContent()));
  console.log(`wrote ${generatedContentPath}`);
}
