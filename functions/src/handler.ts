import { cacheKey } from "./cacheKey";
import { ArgumentPayload, expectedFallacyId, findEntry, Profile } from "./content";
import { validateArgument, Violation } from "./contract";
import { ArgumentRequest, buildPrompt, PromptRequest } from "./prompt";

export interface Completion {
  content: string;
  inputTokens: number;
  outputTokens: number;
}

export interface UsageEvent {
  uid: string;
  cacheHit: boolean;
  inputTokens: number;
  outputTokens: number;
  costUsd: number;
  at: Date;
}

export interface LogEntry {
  level: "info" | "warn";
  event: string;
  data: Record<string, unknown>;
}

export interface Dependencies {
  verifyIdToken(token: string): Promise<string | null>;
  isLlmEnabled(): Promise<boolean>;
  consumeRateLimit(uid: string, now: Date): Promise<boolean>;
  readCache(key: string, now: Date): Promise<ArgumentPayload | null>;
  writeCache(key: string, argument: ArgumentPayload, now: Date): Promise<void>;
  complete(prompt: PromptRequest): Promise<Completion>;
  isFlagged(text: string): Promise<boolean>;
  recordUsage(event: UsageEvent): Promise<void>;
  log(entry: LogEntry): void;
  now(): Date;
}

export interface HandlerConfig {
  model: string;
  maxTokens: number;
  blockedTerms: readonly string[];
  inputPricePerMillionTokens: number;
  outputPricePerMillionTokens: number;
}

export interface HandlerResult {
  status: 200 | 204 | 400 | 401 | 405 | 429;
  body?: ArgumentPayload | { error: string };
  headers?: Record<string, string>;
}

const maxAttempts = 2;
const maxTopicLength = 120;
const maxNameLength = 40;
const opponentIdPattern = /^[a-z][a-z0-9_]{0,39}$/;

export function parseRequest(body: unknown): ArgumentRequest | null {
  if (typeof body !== "object" || body === null) {
    return null;
  }

  const record = body as Record<string, unknown>;
  const opponentId = record.opponent_id;
  const profile = record.profile;
  const act = record.act;
  const catalogId = record.fallacy_id;
  const seed = record.seed;
  const topic = record.topic;
  const tone = record.tone;
  const opponentName = record.opponent_name;

  const valid =
    typeof opponentId === "string" && opponentIdPattern.test(opponentId) &&
    (profile === "sophist" || profile === "philosopher") &&
    (act === 1 || act === 2) &&
    typeof catalogId === "string" && findEntry(catalogId) !== undefined &&
    Number.isSafeInteger(seed) &&
    (topic === undefined || (typeof topic === "string" && topic.length <= maxTopicLength)) &&
    (tone === undefined || typeof tone === "string") &&
    (opponentName === undefined || (typeof opponentName === "string" && opponentName.length <= maxNameLength));

  if (!valid) {
    return null;
  }

  return {
    opponentId: opponentId as string,
    opponentName: (opponentName as string | undefined) ?? capitalize(opponentId as string),
    profile: profile as Profile,
    act: act as 1 | 2,
    catalogId: catalogId as string,
    seed: seed as number,
    topic: topic as string | undefined,
    tone: tone as string | undefined
  };
}

export async function handleGenerateArgument(
  method: string,
  authorization: string | undefined,
  body: unknown,
  deps: Dependencies,
  config: HandlerConfig
): Promise<HandlerResult> {
  if (method !== "POST") {
    return { status: 405 };
  }

  const token = bearerToken(authorization);
  const uid = token ? await deps.verifyIdToken(token) : null;
  if (!uid) {
    return { status: 401, body: { error: "a valid Firebase ID token is required" } };
  }

  const request = parseRequest(body);
  if (!request) {
    return { status: 400, body: { error: "invalid argument request" } };
  }

  const now = deps.now();
  try {
    if (!(await deps.isLlmEnabled())) {
      deps.log({ level: "info", event: "kill_switch_off", data: { uid } });
      return { status: 204 };
    }

    if (!(await deps.consumeRateLimit(uid, now))) {
      return { status: 429, body: { error: "rate limit exceeded" } };
    }

    const key = cacheKey(request.opponentId, request.catalogId, request.seed);
    const cached = await deps.readCache(key, now);
    if (cached) {
      await deps.recordUsage({ uid, cacheHit: true, inputTokens: 0, outputTokens: 0, costUsd: 0, at: now });
      return { status: 200, body: cached, headers: { "X-Cache": "hit" } };
    }

    return await generate(uid, key, request, deps, config, now);
  } catch (error) {
    deps.log({ level: "warn", event: "generation_failed", data: { uid, message: error instanceof Error ? error.message : String(error) } });
    return { status: 204 };
  }
}

async function generate(uid: string, key: string, request: ArgumentRequest, deps: Dependencies, config: HandlerConfig, now: Date): Promise<HandlerResult> {
  const expected = expectedFallacyId(request.catalogId);
  const prompt = buildPrompt(request, config);

  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    const completion = await deps.complete(prompt);
    const costUsd = costOf(completion, config);
    await deps.recordUsage({ uid, cacheHit: false, inputTokens: completion.inputTokens, outputTokens: completion.outputTokens, costUsd, at: now });
    deps.log({ level: "info", event: "openai_call", data: { uid, attempt, inputTokens: completion.inputTokens, outputTokens: completion.outputTokens, costUsd } });

    const { violation, argument } = validateArgument(parseJson(completion.content));
    const wrongFallacy = violation === "unknown_fallacy" || (violation === "none" && argument?.fallacy_id !== expected);
    if (wrongFallacy) {
      deps.log({ level: "warn", event: "unexpected_fallacy", data: { uid, attempt, expected, received: argument?.fallacy_id } });
      continue;
    }

    if (violation !== "none" || !argument) {
      deps.log({ level: "warn", event: "invalid_payload", data: { uid, attempt, violation: violation as Violation } });
      return { status: 204 };
    }

    if (containsBlockedTerm(argument, config.blockedTerms) || (await deps.isFlagged(textOf(argument)))) {
      deps.log({ level: "warn", event: "content_rejected", data: { uid, catalogId: request.catalogId, speech: argument.speech, premises: argument.premises } });
      return { status: 204 };
    }

    await deps.writeCache(key, argument, now);
    return { status: 200, body: argument, headers: { "X-Cache": "miss" } };
  }

  return { status: 204 };
}

export function costOf(completion: Completion, config: HandlerConfig): number {
  return (completion.inputTokens * config.inputPricePerMillionTokens + completion.outputTokens * config.outputPricePerMillionTokens) / 1_000_000;
}

function containsBlockedTerm(argument: ArgumentPayload, blockedTerms: readonly string[]): boolean {
  const text = normalize(textOf(argument));
  return blockedTerms.some((term) => term.trim().length > 0 && text.includes(normalize(term)));
}

function textOf(argument: ArgumentPayload): string {
  return [argument.speech, ...argument.premises].join("\n");
}

function normalize(text: string): string {
  return text.normalize("NFD").replace(/\p{Diacritic}/gu, "").toLowerCase();
}

function parseJson(content: string): unknown {
  try {
    return JSON.parse(content);
  } catch {
    return undefined;
  }
}

function bearerToken(authorization: string | undefined): string | null {
  const match = /^Bearer\s+(.+)$/i.exec(authorization ?? "");
  return match ? match[1].trim() : null;
}

function capitalize(id: string): string {
  return id.charAt(0).toUpperCase() + id.slice(1);
}
