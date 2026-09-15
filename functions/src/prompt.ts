import { argumentSchema, examples, findEntry, philosopherTones, Profile, sophistTones } from "./content";
import { toStrictSchema } from "./contract";

export interface ArgumentRequest {
  opponentId: string;
  opponentName: string;
  profile: Profile;
  act: 1 | 2;
  catalogId: string;
  seed: number;
  topic?: string;
  tone?: string;
}

export interface ChatMessage {
  role: "system" | "user" | "assistant";
  content: string;
}

export interface PromptRequest {
  model: string;
  temperature: number;
  maxTokens: number;
  messages: ChatMessage[];
  schema: unknown;
}

export interface PromptConfig {
  model: string;
  maxTokens: number;
}

const characterTemperatures: Readonly<Record<string, number>> = { thrasybios: 0.9, damaris: 0.5, menexeno: 0.3 };
const profileTemperatures: Readonly<Record<Profile, number>> = { sophist: 0.8, philosopher: 0.4 };

const profileVoices: Readonly<Record<Profile, string>> = {
  sophist: "You are a sophist: ornate, confident and theatrical. You speak to win the crowd, not to find the truth.",
  philosopher: "You are a philosopher: terse and precise. Every word carries weight and you never flatter the audience."
};

const hardRules = [
  "Never declare a winner or say who is right.",
  "Never mention game rules, turns, bars, points, the player or anything outside the scene.",
  "Never break the fourth wall and never mention being an AI or a language model.",
  "Write speech and premises in Brazilian Portuguese; keep JSON keys and the tone value in English."
];

export function temperatureFor(opponentId: string, profile: Profile): number {
  return characterTemperatures[opponentId.toLowerCase()] ?? profileTemperatures[profile];
}

export function toneFor(request: ArgumentRequest): string {
  const allowed = request.profile === "sophist" ? sophistTones : philosopherTones;
  return request.tone && allowed.includes(request.tone) ? request.tone : allowed[request.seed % allowed.length];
}

export function buildPrompt(request: ArgumentRequest, config: PromptConfig): PromptRequest {
  const entry = findEntry(request.catalogId);
  if (!entry) {
    throw new Error(`unknown catalog entry ${request.catalogId}`);
  }

  const isParadox = entry.category === "Paradox";
  const task = isParadox
    ? `Present the paradox "${entry.displayName}" (${entry.description}) honestly and without any fallacy. Set fallacy_id to null.`
    : `Commit exactly the fallacy "${entry.displayName}" (${entry.description}). Set fallacy_id to "${entry.id}".`;

  const system = [
    `You are ${request.opponentName}, speaking in a public debate in Athens, 402 BC.`,
    profileVoices[request.profile],
    ...hardRules,
    "Answer only with the JSON object described by the schema: speech, fallacy_id, premises, weak_premise and tone.",
    "weak_premise is the zero-based index of the premise that breaks the reasoning."
  ].join("\n");

  const context = [
    `Act: ${request.act}.`,
    `Debate topic: ${request.topic?.trim() || "justice and who has the right to ask questions in the agora"}.`,
    `Desired tone: ${toneFor(request)}.`,
    task,
    `Speech length: between 40 and 300 characters. Variation seed: ${request.seed}.`
  ].join("\n");

  const fewShot = (examples[entry.id] ?? []).flatMap<ChatMessage>((example) => [
    { role: "user", content: `Act: ${request.act}.\n${task}` },
    { role: "assistant", content: JSON.stringify(example) }
  ]);

  return {
    model: config.model,
    temperature: temperatureFor(request.opponentId, request.profile),
    maxTokens: config.maxTokens,
    messages: [{ role: "system", content: system }, ...fewShot, { role: "user", content: context }],
    schema: toStrictSchema(argumentSchema)
  };
}
