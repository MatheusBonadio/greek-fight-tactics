import { initializeApp } from "firebase-admin/app";
import { getAuth } from "firebase-admin/auth";
import { getFirestore } from "firebase-admin/firestore";
import * as logger from "firebase-functions/logger";
import { defineInt, defineSecret, defineString } from "firebase-functions/params";
import { onRequest } from "firebase-functions/v2/https";
import OpenAI from "openai";
import { createFirestoreDependencies } from "./firestoreAdapters";
import { Dependencies, handleGenerateArgument } from "./handler";
import { createOpenAiDependencies } from "./openaiAdapters";

initializeApp();

const openAiApiKey = defineSecret("OPENAI_API_KEY");
const openAiModel = defineString("OPENAI_MODEL");
const maxCompletionTokens = defineInt("MAX_COMPLETION_TOKENS", { default: 400 });
const rateLimitPerHour = defineInt("RATE_LIMIT_PER_HOUR", { default: 60 });
const cacheTtlDays = defineInt("CACHE_TTL_DAYS", { default: 90 });
const blockedTerms = defineString("BLOCKED_TERMS", { default: "" });
const inputPrice = defineString("INPUT_PRICE_PER_MILLION_TOKENS", { default: "0" });
const outputPrice = defineString("OUTPUT_PRICE_PER_MILLION_TOKENS", { default: "0" });

export const generateArgument = onRequest(
  {
    region: "southamerica-east1",
    secrets: [openAiApiKey],
    cors: true,
    maxInstances: 10,
    timeoutSeconds: 30
  },
  async (request, response) => {
    const db = getFirestore();
    const deps: Dependencies = {
      verifyIdToken: async (token) => {
        try {
          return (await getAuth().verifyIdToken(token)).uid;
        } catch {
          return null;
        }
      },
      ...createFirestoreDependencies(db, { rateLimitPerHour: rateLimitPerHour.value(), cacheTtlDays: cacheTtlDays.value() }),
      ...createOpenAiDependencies(new OpenAI({ apiKey: openAiApiKey.value() })),
      log: (entry) => (entry.level === "warn" ? logger.warn(entry.event, entry.data) : logger.info(entry.event, entry.data)),
      now: () => new Date()
    };

    const result = await handleGenerateArgument(request.method, request.get("authorization"), request.body, deps, {
      model: openAiModel.value(),
      maxTokens: maxCompletionTokens.value(),
      blockedTerms: blockedTerms.value().split(",").map((term) => term.trim()).filter((term) => term.length > 0),
      inputPricePerMillionTokens: Number(inputPrice.value()),
      outputPricePerMillionTokens: Number(outputPrice.value())
    });

    for (const [name, value] of Object.entries(result.headers ?? {})) {
      response.set(name, value);
    }

    response.status(result.status);
    if (result.body) {
      response.json(result.body);
    } else {
      response.end();
    }
  }
);
