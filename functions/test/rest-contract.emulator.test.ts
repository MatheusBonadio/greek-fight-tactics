import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, expect, it } from "vitest";

const projectId = "demo-gft";
const authHost = process.env.FIREBASE_AUTH_EMULATOR_HOST ?? "127.0.0.1:9099";
const firestoreHost = process.env.FIRESTORE_EMULATOR_HOST ?? "127.0.0.1:8080";
const documentsRoot = `projects/${projectId}/databases/(default)/documents`;
const goldenCommit = readFileSync(join(__dirname, "fixtures", "save_commit.json"), "utf8");

interface SignUpGrant {
  idToken: string;
  refreshToken: string;
  expiresIn: string;
  localId: string;
}

async function signUp(): Promise<SignUpGrant> {
  const response = await fetch(`http://${authHost}/identitytoolkit.googleapis.com/v1/accounts:signUp?key=emulator-api-key`, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: '{"returnSecureToken":true}'
  });
  expect(response.status).toBe(200);
  return (await response.json()) as SignUpGrant;
}

function commit(body: string, idToken: string): Promise<Response> {
  return fetch(`http://${firestoreHost}/v1/${documentsRoot}:commit`, {
    method: "POST",
    headers: { Authorization: `Bearer ${idToken}`, "Content-Type": "application/json" },
    body
  });
}

describe("REST contract used by the Unity client", () => {
  it("signs up anonymously with the fields FirebaseAnonymousAuth reads", async () => {
    const grant = await signUp();

    expect(grant.idToken).toEqual(expect.any(String));
    expect(grant.refreshToken).toEqual(expect.any(String));
    expect(grant.localId).toEqual(expect.any(String));
    expect(grant.expiresIn).toMatch(/^\d+$/);
  });

  it("refreshes the id token with the form body FirebaseAnonymousAuth sends", async () => {
    const grant = await signUp();

    const response = await fetch(`http://${authHost}/securetoken.googleapis.com/v1/token?key=emulator-api-key`, {
      method: "POST",
      headers: { "Content-Type": "application/x-www-form-urlencoded" },
      body: `grant_type=refresh_token&refresh_token=${encodeURIComponent(grant.refreshToken)}`
    });

    expect(response.status).toBe(200);
    const refreshed = (await response.json()) as Record<string, string>;
    expect(refreshed.user_id).toBe(grant.localId);
    expect(refreshed.id_token).toEqual(expect.any(String));
    expect(refreshed.refresh_token).toEqual(expect.any(String));
    expect(refreshed.expires_in).toMatch(/^\d+$/);
  });

  it("accepts the golden FirestoreDocuments commit only from the owner and reads it back", async () => {
    const owner = await signUp();
    const stranger = await signUp();
    const body = goldenCommit.replaceAll("uid-golden", owner.localId);

    expect((await commit(body, stranger.idToken)).status).toBe(403);
    expect((await commit(body, owner.idToken)).status).toBe(200);

    const read = await fetch(`http://${firestoreHost}/v1/${documentsRoot}/saves/${owner.localId}`, {
      headers: { Authorization: `Bearer ${owner.idToken}` }
    });
    expect(read.status).toBe(200);

    const document = (await read.json()) as { fields: unknown };
    expect(document.fields).toEqual(JSON.parse(body).writes[0].update.fields);

    const missing = await fetch(`http://${firestoreHost}/v1/${documentsRoot}/saves/${stranger.localId}`, {
      headers: { Authorization: `Bearer ${stranger.idToken}` }
    });
    expect(missing.status).toBe(404);
  });
});
