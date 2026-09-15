import { createHash } from "node:crypto";

const keyHexLength = 32;

export function cacheKey(opponentId: string, catalogId: string, seed: number): string {
  return createHash("sha256").update(`${opponentId}|${catalogId}|${seed}`, "utf8").digest("hex").slice(0, keyHexLength);
}
