import { FieldValue, Firestore, Timestamp } from "firebase-admin/firestore";
import { ArgumentPayload } from "./content";
import { Dependencies, UsageEvent } from "./handler";

export interface FirestoreConfig {
  rateLimitPerHour: number;
  cacheTtlDays: number;
}

const millisecondsPerHour = 60 * 60 * 1000;
const millisecondsPerDay = 24 * millisecondsPerHour;
const rateLimitRetentionHours = 2;

export const collections = {
  config: "config",
  runtimeDocument: "runtime",
  rateLimits: "rate_limits",
  argumentCache: "argument_cache",
  usage: "usage"
} as const;

export function hourBucket(now: Date): string {
  return now.toISOString().slice(0, 13).replace(/[-T]/g, "");
}

export function dayBucket(now: Date): string {
  return now.toISOString().slice(0, 10);
}

export function createFirestoreDependencies(
  db: Firestore,
  config: FirestoreConfig
): Pick<Dependencies, "isLlmEnabled" | "consumeRateLimit" | "readCache" | "writeCache" | "recordUsage"> {
  return {
    async isLlmEnabled() {
      const snapshot = await db.collection(collections.config).doc(collections.runtimeDocument).get();
      return snapshot.get("llm_enabled") !== false;
    },

    async consumeRateLimit(uid: string, now: Date) {
      const reference = db.collection(collections.rateLimits).doc(`${uid}_${hourBucket(now)}`);
      return db.runTransaction(async (transaction) => {
        const snapshot = await transaction.get(reference);
        const count = snapshot.exists ? (snapshot.get("count") as number) : 0;
        if (count >= config.rateLimitPerHour) {
          return false;
        }

        transaction.set(reference, {
          uid,
          count: count + 1,
          expires_at: Timestamp.fromMillis(now.getTime() + rateLimitRetentionHours * millisecondsPerHour)
        });
        return true;
      });
    },

    async readCache(key: string, now: Date) {
      const snapshot = await db.collection(collections.argumentCache).doc(key).get();
      if (!snapshot.exists) {
        return null;
      }

      const expiresAt = snapshot.get("expires_at") as Timestamp | undefined;
      if (expiresAt && expiresAt.toMillis() <= now.getTime()) {
        return null;
      }

      return snapshot.get("argument") as ArgumentPayload;
    },

    async writeCache(key: string, argument: ArgumentPayload, now: Date) {
      await db.collection(collections.argumentCache).doc(key).set({
        argument,
        created_at: Timestamp.fromDate(now),
        expires_at: Timestamp.fromMillis(now.getTime() + config.cacheTtlDays * millisecondsPerDay)
      });
    },

    async recordUsage(event: UsageEvent) {
      await db.collection(collections.usage).doc(dayBucket(event.at)).set(
        {
          requests: FieldValue.increment(1),
          openai_calls: FieldValue.increment(event.cacheHit ? 0 : 1),
          cache_hits: FieldValue.increment(event.cacheHit ? 1 : 0),
          input_tokens: FieldValue.increment(event.inputTokens),
          output_tokens: FieldValue.increment(event.outputTokens),
          cost_usd: FieldValue.increment(event.costUsd)
        },
        { merge: true }
      );
    }
  };
}
