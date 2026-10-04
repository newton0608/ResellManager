import { copyFile, mkdir, readFile } from "node:fs/promises";

const root = new URL("../", import.meta.url);
const packageRoot = new URL("node_modules/@ericblade/quagga2/", root);
const { version } = JSON.parse(await readFile(new URL("package.json", packageRoot), "utf8"));
if (version !== "1.11.0") throw new Error("Expected Quagga2 1.11.0, received " + version);
const target = new URL("src/ResellManager.Web/wwwroot/vendor/quagga2/", root);
await mkdir(target, { recursive: true });
await copyFile(new URL("dist/quagga.min.js", packageRoot), new URL("quagga.min.js", target));
await copyFile(new URL("LICENSE", packageRoot), new URL("LICENSE", target));
