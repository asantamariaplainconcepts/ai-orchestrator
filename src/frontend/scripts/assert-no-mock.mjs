// The mock must never ship (#95). MODE-guarded dead code elimination should exclude it from
// production bundles; this asserts the outcome rather than trusting the mechanism (ADR-0004).
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

const assets = "../root/AiOrchestrator.Server/wwwroot/assets";
const offenders = readdirSync(assets)
  .filter((name) => name.endsWith(".js"))
  .filter((name) => readFileSync(join(assets, name), "utf8").includes("AIO_MOCK" + "_MARKER"));

if (offenders.length > 0) {
  console.error(`The production bundle contains the mock adapter: ${offenders.join(", ")}`);
  process.exit(1);
}
console.log("✓ production bundle carries no mock adapter");
