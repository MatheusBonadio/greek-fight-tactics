import { initializeApp } from "firebase-admin/app";
import { getFirestore } from "firebase-admin/firestore";
import { formatUsageTable, loadUsage, summarizeUsage } from "./usageReport";

const defaultWindowDays = 30;
const millisecondsPerDay = 24 * 60 * 60 * 1000;

function dayOf(date: Date): string {
  return date.toISOString().slice(0, 10);
}

async function main(): Promise<void> {
  const [fromArgument, toArgument] = process.argv.slice(2);
  const now = new Date();
  const fromDay = fromArgument ?? dayOf(new Date(now.getTime() - (defaultWindowDays - 1) * millisecondsPerDay));
  const toDay = toArgument ?? dayOf(now);

  initializeApp({ projectId: process.env.GCLOUD_PROJECT ?? process.env.GOOGLE_CLOUD_PROJECT ?? "demo-gft" });
  const summary = summarizeUsage(await loadUsage(getFirestore(), fromDay, toDay));
  console.log(formatUsageTable(summary));
}

main().catch((error: unknown) => {
  console.error(error instanceof Error ? error.message : error);
  process.exitCode = 1;
});
