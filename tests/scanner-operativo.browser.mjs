import assert from "node:assert/strict";
import { createServer } from "node:http";
import { spawnSync } from "node:child_process";
import { readFile, mkdir, writeFile } from "node:fs/promises";
import { fileURLToPath, pathToFileURL } from "node:url";

const { chromium } = await import(process.env.SCANNER_PLAYWRIGHT_MODULE
    ? pathToFileURL(process.env.SCANNER_PLAYWRIGHT_MODULE).href : "playwright");
const root = new URL("../", import.meta.url);
const artifacts = new URL(".artifacts/scanner-operativo-ui/", root);
await mkdir(artifacts, { recursive: true });
// HTML emitido por los componentes reales, con SQLite sintético y callbacks .NET probados.
const pruebas = spawnSync("dotnet", ["test", "ResellManager.sln", "--no-restore", "--filter",
    "FullyQualifiedName~ScannerOperativoTests.MarkupOperativo"], {
    cwd: fileURLToPath(root), stdio: "inherit",
    env: { ...process.env, SCANNER_OPERATIVO_HTML_DIRECTORY: fileURLToPath(artifacts) }
});
assert.equal(pruebas.status, 0, "No se pudo generar el markup operativo real");
const estados = ["venta-inicial", "venta-cantidad", "venta-cantidad-error", "venta-agregadas",
    "venta-revision", "venta-cero", "venta-no-encontrado", "inventario-filtros",
    "inventario-no-encontrado", "inventario-cancelado"];
const conocidos = new Map([
    ["/app.css", new URL("src/ResellManager.Web/wwwroot/app.css", root)],
    ["/css/tailwind.css", new URL("src/ResellManager.Web/wwwroot/css/tailwind.css", root)],
    ["/barcode-scanner.js", new URL("src/ResellManager.Web/wwwroot/barcode-scanner.js", root)]
]);
const server = createServer(async (request, response) => {
    const path = new URL(request.url, "http://localhost").pathname;
    response.setHeader("Content-Security-Policy", "frame-ancestors 'none'");
    response.setHeader("X-Frame-Options", "DENY");
    response.setHeader("Permissions-Policy", "camera=(self), microphone=()");
    try {
        const nombre = path.slice(1);
        if (estados.includes(nombre)) {
            response.setHeader("Content-Type", "text/html; charset=utf-8");
            const html = await readFile(new URL(nombre + ".html", artifacts), "utf8");
            response.end('<!doctype html><html lang="es"><head><meta name="viewport" content="width=device-width, initial-scale=1">' +
                '<link rel="stylesheet" href="/app.css"><link rel="stylesheet" href="/css/tailwind.css"></head>' +
                '<body><main class="mx-auto grid min-w-0 max-w-7xl gap-6 p-4 sm:p-6">' + html + '</main></body></html>');
        } else if (conocidos.has(path)) {
            response.setHeader("Content-Type", path.endsWith(".js") ? "text/javascript" : "text/css");
            response.end(await readFile(conocidos.get(path)));
        } else { response.writeHead(404); response.end(); }
    } catch (error) { response.writeHead(500); response.end(String(error)); }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const address = "http://127.0.0.1:" + server.address().port;
let browser;
const report = [], errors = [];
try {
    browser = await chromium.launch({ channel: process.env.SCANNER_BROWSER_CHANNEL || "msedge", headless: true });
    const page = await browser.newPage();
    page.on("pageerror", error => errors.push(error.message));
    page.on("request", request => assert.equal(new URL(request.url()).origin, address, "Solicitud fuera del origen local"));
    for (const width of [320, 390, 768, 1440]) {
        await page.setViewportSize({ width, height: 900 });
        for (const estado of estados) {
            await page.goto(address + "/" + estado);
            if (estado === "venta-revision") await page.locator("dialog:not(.rm-barcode-scanner)").evaluate(dialog => dialog.showModal());
            const medidas = await page.evaluate(() => ({
                width: window.innerWidth, scroll: document.documentElement.scrollWidth,
                controles: [...document.querySelectorAll("button, input:not([type=file]), select")]
                    .filter(element => element.getClientRects().length > 0)
                    .map(element => ({ nombre: element.textContent.trim() || element.id,
                        ancho: element.getBoundingClientRect().width, alto: element.getBoundingClientRect().height }))
            }));
            assert.ok(medidas.scroll <= width, estado + " tiene overflow a " + width);
            for (const control of medidas.controles) {
                assert.ok(control.ancho >= 44 && control.alto >= 44, estado + ": control pequeño " + JSON.stringify(control));
            }
            if (estado === "venta-cantidad") {
                const cantidad = page.locator("#directa-cantidad-scanner");
                assert.equal(await cantidad.inputValue(), "1");
                assert.equal(await cantidad.getAttribute("max"), "3");
                for (const [valor, valido] of [["0", false], ["4", false], ["1.5", false], ["1", true], ["3", true]]) {
                    await cantidad.fill(valor);
                    assert.equal(await cantidad.evaluate(input => input.checkValidity()), valido);
                }
                await cantidad.fill("1");
            }
            await page.screenshot({ path: fileURLToPath(new URL(estado + "-" + width + ".png", artifacts)), fullPage: true });
            report.push({ width, estado, controles: medidas.controles.length, overflow: false });
        }
        // Apertura/cancelación con el módulo real: no solicitar cámara al abrir el diálogo.
        await page.goto(address + "/inventario-filtros");
        assert.equal(await page.evaluate(async () => {
            window.scanner = await import("/barcode-scanner.js");
            const dialog = document.querySelector("dialog.rm-barcode-scanner");
            return window.scanner.abrir(dialog, dialog.querySelector(".barcode-camera-feed").id,
                { invokeMethodAsync: async () => {} });
        }), "ready");
        assert.equal(await page.locator("dialog[open]").count(), 1);
        await page.evaluate(() => window.scanner.cerrar(document.querySelector("dialog.rm-barcode-scanner")));
        assert.equal(await page.locator("dialog[open]").count(), 0);
        assert.equal(await page.locator("#buscar-unidad").inputValue(), "Producto");
        assert.equal(await page.locator("#estado-unidad").inputValue(), "Disponible");
        console.log("QA operativo " + width + " px: 10 estados correctos");
    }
    assert.deepEqual(errors, []);
    const resultado = { browser: await browser.version(), channel: process.env.SCANNER_BROWSER_CHANNEL || "msedge",
        hardwareCamera: false, liveCircuit: false, estados: report.length, errors, report };
    await writeFile(new URL("report.json", artifacts), JSON.stringify(resultado, null, 2));
    console.log(JSON.stringify({ estados: report.length, errors, hardwareCamera: false, liveCircuit: false }));
} finally {
    await browser?.close();
    await new Promise(resolve => server.close(resolve));
}
