import assert from "node:assert/strict";
import { createServer } from "node:http";
import { fileURLToPath, pathToFileURL } from "node:url";
import { readFile, mkdir, writeFile } from "node:fs/promises";

const { chromium } = await import(process.env.SCANNER_PLAYWRIGHT_MODULE ? pathToFileURL(process.env.SCANNER_PLAYWRIGHT_MODULE).href : "playwright");
const { expect } = await import(process.env.SCANNER_PLAYWRIGHT_MODULE ? new URL("test.mjs", pathToFileURL(process.env.SCANNER_PLAYWRIGHT_MODULE)).href : "playwright/test");
const root = new URL("../", import.meta.url);
const artifacts = new URL(".artifacts/scanner-qa/", root);
await mkdir(artifacts, { recursive: true });
const razor = await readFile(new URL("src/ResellManager.Web/Components/Shared/BarcodeScanner.razor", root), "utf8");
const dialog = razor.slice(razor.indexOf("<dialog"), razor.indexOf("</dialog>") + 9)
    .replace(/@ref="[^"]*"|@onclick="[^"]*"/g, "")
    .replace(/@\(VisorId \+ "-photo"\)/g, "visor-photo")
    .replace(/@\(VisorId \+ "-zoom"\)/g, "visor-zoom")
    .replaceAll("@TituloId", "scanner-title").replaceAll("@InstruccionesId", "scanner-help")
    .replaceAll("@VisorId", "visor");
const html = '<!doctype html><html lang="es"><head><meta name="viewport" content="width=device-width, initial-scale=1">' +
    '<link rel="stylesheet" href="/app.css"><link rel="stylesheet" href="/css/tailwind.css"></head><body><main class="p-4">' +
    '<input class="ui-input" aria-label="Código manual" value="EXISTING"><button class="ui-button" id="open">Escanear</button>' +
    dialog + '</main><script type="module">import * as scanner from "/barcode-scanner.js"; window.scanner = scanner; window.calls = [];' +
    'document.querySelector("#open").onclick = () => scanner.abrir(document.querySelector("dialog"), "visor", {' +
    'invokeMethodAsync: async (...args) => window.calls.push(args) });' +
    'document.querySelector("dialog button").onclick = () => scanner.cerrar(document.querySelector("dialog"));</script></body></html>';
const requests = [];
const server = createServer(async (request, response) => {
    const path = new URL(request.url, "http://localhost").pathname;
    requests.push({ path, method: request.method });
    response.setHeader("Content-Security-Policy", "frame-ancestors 'none'");
    response.setHeader("X-Frame-Options", "DENY");
    response.setHeader("Permissions-Policy", "camera=(self), microphone=()");
    try {
        if (path === "/") { response.setHeader("Content-Type", "text/html; charset=utf-8"); response.end(html); return; }
        const known = new Map([
            ["/barcode-scanner.js", "src/ResellManager.Web/wwwroot/barcode-scanner.js"],
            ["/css/tailwind.css", "src/ResellManager.Web/wwwroot/css/tailwind.css"],
            ["/app.css", "src/ResellManager.Web/wwwroot/app.css"],
            ["/vendor/quagga2/quagga.min.js", "src/ResellManager.Web/wwwroot/vendor/quagga2/quagga.min.js"],
            ...["ean", "ean_8", "upc", "upc_e", "code_128", "no_code"].map(name => ["/fixtures/" + name + ".jpg", "tests/fixtures/barcodes/" + name + ".jpg"])
        ]);
        if (!known.has(path)) { response.writeHead(404); response.end(); return; }
        response.setHeader("Content-Type", path.endsWith(".js") ? "text/javascript" : path.endsWith(".css") ? "text/css" : "image/jpeg");
        response.end(await readFile(new URL(known.get(path), root)));
    } catch (error) { response.writeHead(500); response.end(String(error)); }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const address = "http://127.0.0.1:" + server.address().port;
const browser = await chromium.launch({
    channel: process.env.SCANNER_BROWSER_CHANNEL || "msedge", headless: true,
    args: ["--use-fake-ui-for-media-stream", "--use-fake-device-for-media-stream"]
});
const report = [], errors = [];
let checks = 0;
try {
    const context = await browser.newContext();
    await context.addInitScript(() => {
        // Canvas-backed stream, never physical camera hardware.
        navigator.mediaDevices.getUserMedia = async () => {
            const image = new Image();
            image.src = window.top.cameraFixture;
            await image.decode();
            const canvas = document.createElement("canvas");
            canvas.width = 1280; canvas.height = 720;
            const ctx = canvas.getContext("2d");
            const paint = () => {
                ctx.fillStyle = "white"; ctx.fillRect(0, 0, 1280, 720);
                if (window.top.pauseCanvas) return;
                // Center the real fixture in the horizontal scan area.
                const scale = Math.min(1100 / image.width, 650 / image.height);
                const width = image.width * scale, height = image.height * scale;
                ctx.drawImage(image, (1280 - width) / 2, (720 - height) / 2, width, height);
            };
            paint();
            const timer = setInterval(paint, 100);
            const stream = canvas.captureStream(10);
            window.top.qaTracks ??= [];
            for (const track of stream.getTracks()) {
                window.top.qaTracks.push(track);
                track.addEventListener("ended", () => clearInterval(timer), { once: true });
                const stop = track.stop.bind(track);
                track.stop = () => { clearInterval(timer); canvas.width = canvas.height = 0; stop(); };
            }
            return stream;
        };
    });
    const page = await context.newPage();
    page.on("pageerror", error => errors.push(error.message));
    await page.goto(address);
    await page.waitForFunction(() => window.scanner);
    const code = "5901234123457";
    const left = ["0001101","0011001","0010011","0111101","0100011","0110001","0101111","0111011","0110111","0001011"];
    const parity = "LGGLLG";
    let bits = "101";
    for (let i = 1; i <= 6; i++) {
        const pattern = left[Number(code[i])];
        bits += parity[i - 1] === "L" ? pattern : [...pattern].reverse().map(bit => bit === "0" ? "1" : "0").join("");
    }
    bits += "01010";
    for (let i = 7; i < 13; i++) bits += [...left[Number(code[i])]].map(bit => bit === "0" ? "1" : "0").join("");
    bits += "101";
    const bars = [...bits].map((bit, i) => bit === "1" ? '<rect x="' + ((i + 16) * 6) + '" y="10" width="6" height="160"/>' : "").join("");
    const cameraData = '<svg xmlns="http://www.w3.org/2000/svg" width="762" height="180"><rect width="762" height="180" fill="white"/>' + bars + '</svg>';
    await page.evaluate(value => { window.cameraFixture = value; }, "data:image/svg+xml;base64," + Buffer.from(cameraData).toString("base64"));
    async function open() {
        await page.locator("#open").click();
        await expect(page.locator("dialog")).toBeVisible();
    }
    async function close() {
        await page.locator("dialog button").first().click();
        await expect(page.locator("dialog")).not.toBeVisible();
        assert.equal(await page.locator("iframe").count(), 0); checks++;
    }
    async function layout(width, state) {
        const geometry = await page.evaluate(() => {
            const dialog = document.querySelector("dialog"), bounds = dialog.getBoundingClientRect();
            return { root: document.documentElement.scrollWidth, viewport: innerWidth,
                dialogScroll: dialog.scrollWidth, dialogClient: dialog.clientWidth, left: bounds.left, right: bounds.right };
        });
        assert.ok(geometry.root <= geometry.viewport, JSON.stringify(geometry));
        assert.ok(geometry.dialogScroll <= geometry.dialogClient + 1, JSON.stringify(geometry));
        assert.ok(geometry.left >= 0 && geometry.right <= width, JSON.stringify(geometry));
        const buttons = await page.locator("dialog button:visible").all();
        for (const button of buttons) {
            const box = await button.boundingBox();
            assert.ok(box.width >= 44 && box.height >= 44, await button.textContent());
        }
        await page.screenshot({ path: fileURLToPath(new URL(width + "-" + state + ".png", artifacts)) });
        checks++;
    }
    async function photo(name) {
        await page.locator("[data-barcode-file]").setInputFiles(fileURLToPath(new URL("tests/fixtures/barcodes/" + name + ".jpg", root)));
        await expect(page.locator("[data-barcode-photo]")).not.toHaveAttribute("data-state", "analyzing", { timeout: 35000 });
        assert.equal(await page.locator("iframe").count(), 0); checks++;
    }
    for (const width of [320, 390, 768, 1440]) {
        await page.setViewportSize({ width, height: 900 });
        await open(); await layout(width, "idle");
        await expect(page.getByText("Tomar foto", { exact: true })).toBeVisible();
        await expect(page.getByRole("button", { name: "Escanear en vivo" })).toBeVisible();
        await expect(page.getByRole("button", { name: "Introducir manualmente" })).toBeVisible();
        let releaseBundle, finishedBundle;
        const bundleDone = new Promise(resolve => { finishedBundle = resolve; });
        const bundleGate = new Promise(resolve => { releaseBundle = resolve; });
        await page.route("**/vendor/quagga2/quagga.min.js", async route => {
            await bundleGate; await route.continue(); finishedBundle();
        });
        await page.locator("[data-barcode-file]").setInputFiles(fileURLToPath(new URL("tests/fixtures/barcodes/ean.jpg", root)));
        await expect(page.locator("[data-barcode-photo]")).toHaveAttribute("data-state", "analyzing");
        await expect(page.getByRole("button", { name: "Escanear en vivo" })).toBeDisabled();
        await layout(width, "analyzing");
        releaseBundle();
        await bundleDone;
        await page.unroute("**/vendor/quagga2/quagga.min.js");
        await expect(page.locator("[data-barcode-photo]")).toHaveAttribute("data-state", "success");
        await close(); await open();
        for (const [name, expected] of [
            ["ean", ["3574660239843"]], ["ean_8", ["42191605"]],
            // Quagga's first EAN reader can represent UPC-A with its leading zero.
            ["upc", ["882428015268", "0882428015268"]],
            ["upc_e", ["04965802"]], ["code_128", ["0001285112001000040801"]]
        ]) {
            await page.evaluate(() => { window.calls = []; });
            await photo(name);
            await expect(page.locator("[data-barcode-photo]")).toHaveAttribute("data-state", "success");
            const code = await page.locator("[data-barcode-code]").textContent();
            assert.ok(expected.includes(code), name + " decoded " + code);
            assert.deepEqual(await page.evaluate(() => window.calls), []);
            await layout(width, name);
            await page.getByRole("button", { name: "Usar código" }).click();
            await expect(page.locator("dialog")).not.toBeVisible();
            assert.deepEqual(await page.evaluate(() => window.calls), [["FinalizarEscaneo", "detected", code]]);
            report.push({ width, mode: "photo", name, code });
            await open();
        }
        await photo("no_code");
        await expect(page.locator("[data-barcode-photo]")).toHaveAttribute("data-state", "not-found");
        await layout(width, "not-found");
        await page.locator("[data-barcode-file]").setInputFiles({ name: "invalid.jpg", mimeType: "image/jpeg", buffer: Buffer.from("invalid photo") });
        await expect(page.locator("[data-barcode-photo]")).toHaveAttribute("data-state", "technical-error");
        await layout(width, "technical-error");
        await close(); await open();
        await page.evaluate(() => { window.calls = []; window.pauseCanvas = true; });
        await page.getByRole("button", { name: "Escanear en vivo" }).click();
        await expect(page.locator("[data-barcode-live-status]")).toHaveText("Buscando código…");
        await layout(width, "live");
        await page.evaluate(() => { window.pauseCanvas = false; });
        await expect(page.locator("[data-barcode-photo]")).toHaveAttribute("data-state", "success", { timeout: 20000 });
        assert.equal(await page.locator("[data-barcode-code]").textContent(), "5901234123457");
        assert.deepEqual(await page.evaluate(() => window.calls), []);
        assert.ok(await page.evaluate(() => window.qaTracks.every(track => track.readyState === "ended")));
        report.push({ width, mode: "live", source: "canvas", code: "5901234123457" });
        await close(); await open();
        await page.getByRole("button", { name: "Introducir manualmente" }).click();
        await expect(page.locator("dialog")).not.toBeVisible();
        assert.equal(await page.getByLabel("Código manual").inputValue(), "EXISTING");
        console.log("QA viewport " + width + " passed");
    }
    await page.setViewportSize({ width: 390, height: 900 });
    for (const orientation of [6, 8]) {
        const encoded = await page.evaluate(async ({ data, orientation }) => {
            const image = new Image(); image.src = "data:image/jpeg;base64," + data; await image.decode();
            const canvas = document.createElement("canvas"); canvas.width = image.height; canvas.height = image.width;
            const ctx = canvas.getContext("2d");
            if (orientation === 6) ctx.setTransform(0, -1, 1, 0, 0, image.width);
            else ctx.setTransform(0, 1, -1, 0, image.height, 0);
            ctx.drawImage(image, 0, 0);
            return canvas.toDataURL("image/jpeg", 0.98).split(",")[1];
        }, { data: (await readFile(new URL("tests/fixtures/barcodes/ean.jpg", root))).toString("base64"), orientation });
        const exif = Buffer.alloc(36);
        exif.writeUInt16BE(0xffe1, 0); exif.writeUInt16BE(34, 2); exif.write("Exif", 4);
        exif.write("II", 10); exif.writeUInt16LE(42, 12); exif.writeUInt32LE(8, 14);
        exif.writeUInt16LE(1, 18); exif.writeUInt16LE(0x0112, 20); exif.writeUInt16LE(3, 22);
        exif.writeUInt32LE(1, 24); exif.writeUInt16LE(orientation, 28);
        const jpeg = Buffer.from(encoded, "base64");
        const buffer = Buffer.concat([jpeg.subarray(0, 2), exif, jpeg.subarray(2)]);
        for (const fallback of [false, true]) {
            await open();
            await page.evaluate(fallback => {
                window.nativeBitmap ??= window.createImageBitmap;
                window.createImageBitmap = fallback ? undefined : window.nativeBitmap;
            }, fallback);
            await page.locator("[data-barcode-file]").setInputFiles({ name: "oriented.jpg", mimeType: "image/jpeg", buffer });
            await expect(page.locator("[data-barcode-photo]")).toHaveAttribute("data-state", "success", { timeout: 15000 });
            assert.equal(await page.locator("[data-barcode-code]").textContent(), "3574660239843");
            assert.equal(await page.locator("iframe").count(), 0);
            report.push({ mode: "photo", orientation, fallback, code: "3574660239843" });
            await close();
        }
    }
    await page.evaluate(() => { window.createImageBitmap = window.nativeBitmap; });
    assert.deepEqual(errors, []);
    assert.ok(requests.every(request => request.method === "GET"), "No photo upload request");
    await writeFile(new URL("report.json", artifacts), JSON.stringify({ checks, report, requests, errors, hardwareCamera: false }, null, 2));
    console.log(JSON.stringify({ checks, decodes: report.length, errors, hardwareCamera: false }));
} finally {
    await browser.close();
    await new Promise(resolve => server.close(resolve));
}
