import OpenAI from "openai";
import { Dependencies } from "./handler";

export const moderationModel = "omni-moderation-latest";
export const responseSchemaName = "argument";

export function createOpenAiDependencies(client: OpenAI): Pick<Dependencies, "complete" | "isFlagged"> {
  return {
    async complete(prompt) {
      const response = await client.chat.completions.create({
        model: prompt.model,
        temperature: prompt.temperature,
        max_completion_tokens: prompt.maxTokens,
        messages: prompt.messages,
        response_format: {
          type: "json_schema",
          json_schema: { name: responseSchemaName, strict: true, schema: prompt.schema as Record<string, unknown> }
        }
      });

      return {
        content: response.choices[0]?.message?.content ?? "",
        inputTokens: response.usage?.prompt_tokens ?? 0,
        outputTokens: response.usage?.completion_tokens ?? 0
      };
    },

    async isFlagged(text) {
      const response = await client.moderations.create({ model: moderationModel, input: text });
      return response.results.some((result) => result.flagged);
    }
  };
}
