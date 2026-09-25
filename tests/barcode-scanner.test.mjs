import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../src/ResellManager.Web/wwwroot/barcode-scanner.js", import.meta.url), "utf8");
const scannerModule = await import("data:text/javascript;base64," + Buffer.from(source).toString("base64"));

function setup(startImplementation = () => Promise.resolve()) {
    const events = new Map();
    const observers = [];
    const tracks = [{ stops: 0, stop() { this.stops++; } }];
    const video = { srcObject: { getTracks: () => tracks } };
    const dialog = {
        open: false,
        isConnected: true,
        showModal() { this.open = true; },
        close() { this.open = false; },
        addEventListener(name, callback) { events.set(name, callback); },
        removeEventListener(name) { events.delete(name); },
        querySelectorAll: () => [video]
    };
    const calls = [];
    const reference = {
        async invokeMethodAsync(...args) { calls.push(args); }
    };
    const instances = [];
    class FakeScanner {
        constructor(id, config) {
            this.id = id;
            this.config = config;
            this.stops = 0;
            instances.push(this);
        }
        start(camera, config, success) {
            this.camera = camera;
            this.success = success;
            return startImplementation();
        }
        async stop() { this.stops++; }
        clear() {}
    }
    globalThis.window = {
        isSecureContext: true,
        Html5Qrcode: FakeScanner,
        Html5QrcodeSupportedFormats: {
            EAN_13: 9, EAN_8: 10, UPC_A: 14, UPC_E: 15, CODE_128: 5
        },
        addEventListener() {},
        removeEventListener() {}
    };
    globalThis.MutationObserver = class {
        constructor(callback) { this.callback = callback; observers.push(this); }
        observe() {}
        disconnect() {}
        notify() { this.callback(); }
    };
    globalThis.document = {
        body: {},
        activeElement: { isConnected: true, focus() {} },
        addEventListener() {},
        removeEventListener() {},
        hidden: false
    };
    Object.defineProperty(globalThis, "navigator", {
        configurable: true,
        value: { mediaDevices: { getUserMedia() {} } }
    });
    return { dialog, reference, calls, instances, tracks, events, observers };
}

async function settle() {
    await new Promise(resolve => setTimeout(resolve, 0));
}

test("detiene la cámara antes de entregar un solo código y permite reabrir", async () => {
    const state = setup();
    const first = await scannerModule.abrir(state.dialog, "visor", state.reference);
    assert.equal(first, "started");
    assert.equal(state.instances[0].camera.facingMode, "environment");
    assert.equal(state.instances[0].config.useBarCodeDetectorIfSupported, false);
    assert.deepEqual(state.instances[0].config.formatsToSupport, [9, 10, 14, 15, 5]);
    state.instances[0].success("7501234567890");
    state.instances[0].success("7501234567890");
    await settle();
    assert.equal(state.instances[0].stops, 1);
    assert.equal(state.tracks[0].stops, 1);
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "7501234567890"]]);
    assert.equal(state.dialog.open, false);

    const second = await scannerModule.abrir(state.dialog, "visor", state.reference);
    assert.equal(second, "started");
    scannerModule.cerrar(state.dialog);
    await settle();
    assert.equal(state.instances[1].stops, 1);
    assert.equal(state.dialog.open, false);
});

test("cancelar durante el permiso impide callbacks y libera la cámara al iniciar", async () => {
    let resolveStart;
    let starts = 0;
    const state = setup(() => ++starts === 1
        ? new Promise(resolve => { resolveStart = resolve; })
        : Promise.resolve());
    const opening = scannerModule.abrir(state.dialog, "visor", state.reference);
    await settle();
    assert.equal(state.dialog.open, true);
    scannerModule.cerrar(state.dialog);
    assert.equal(state.dialog.open, false);
    resolveStart();
    assert.equal(await opening, "cancelled");
    await settle();
    assert.equal(state.instances[0].stops, 1);
    assert.equal(state.tracks[0].stops, 1);
    assert.deepEqual(state.calls, []);
    assert.equal(await scannerModule.abrir(state.dialog, "visor", state.reference), "started");
    scannerModule.cerrar(state.dialog);
    await settle();
});

test("explica permiso denegado sin dejar un escáner activo", async () => {
    const state = setup(() => Promise.reject(Object.assign(new Error("Permission denied"), { name: "NotAllowedError" })));
    assert.equal(await scannerModule.abrir(state.dialog, "visor", state.reference), "denied");
    assert.equal(state.dialog.open, false);
    assert.equal(state.instances[0].stops, 1);
    assert.equal(await scannerModule.abrir(state.dialog, "visor", state.reference), "denied");
});



test("desmontar el componente libera la cámara sin callback", async () => {
    const state = setup();
    assert.equal(await scannerModule.abrir(state.dialog, "visor", state.reference), "started");
    state.dialog.isConnected = false;
    state.observers[0].notify();
    await settle();
    assert.equal(state.instances[0].stops, 1);
    assert.equal(state.tracks[0].stops, 1);
    assert.deepEqual(state.calls, []);
});

test("reporta cámara inexistente y navegador incompatible", async () => {
    const state = setup(() => Promise.reject(Object.assign(new Error("Requested device not found"), { name: "NotFoundError" })));
    assert.equal(await scannerModule.abrir(state.dialog, "visor", state.reference), "no-camera");
    assert.equal(state.dialog.open, false);
    window.isSecureContext = false;
    assert.equal(await scannerModule.abrir(state.dialog, "visor", state.reference), "unsupported");
});
