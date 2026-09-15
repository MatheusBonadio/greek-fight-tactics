import { spawnSync } from "node:child_process";

const startedAt = new Date().toISOString();
const result = spawnSync(
  'firebase emulators:exec --only firestore,auth --project demo-gft "vitest run --config vitest.emulators.config.mts"',
  { stdio: "inherit", shell: true }
);

if (process.platform === "win32") {
  const stopOrphans = `
    $since = [datetime]'${startedAt}'
    Get-CimInstance Win32_Process |
      Where-Object { $_.Name -eq 'java.exe' -and $_.CommandLine -match 'cloud-firestore-emulator' -and $_.CreationDate -ge $since } |
      ForEach-Object { Stop-Process -Id $_.ProcessId -Force; Write-Output ('stopped orphaned firestore emulator ' + $_.ProcessId) }
  `;
  const encoded = Buffer.from(stopOrphans, "utf16le").toString("base64");
  spawnSync("powershell", ["-NoProfile", "-NonInteractive", "-EncodedCommand", encoded], { stdio: "inherit" });
}

process.exit(result.status ?? 1);
