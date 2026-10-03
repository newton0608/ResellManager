import test, { afterEach } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../src/ResellManager.Web/wwwroot/barcode-scanner.js", import.meta.url), "utf8");
const scannerModule = await import("data:text/javascript;base64," + Buffer.from(source).toString("base64"));
const nativeSetTimeout = globalThis.setTimeout;
const nativeURL = globalThis.URL;
const nativeImage = globalThis.Image;
const nativeFetch = globalThis.fetch;
let currentState;

function eventTarget(properties = {}) {
    const listeners = new Map();
    const attributes = new Map();
    return Object.assign(properties, {
        setAttribute(name, value) { attributes.set(name, String(value)); },
        removeAttribute(name) { attributes.delete(name); },
        getAttribute(name) { return attributes.get(name) ?? null; },
        addEventListener(name, callback) {
            if (!listeners.has(name)) listeners.set(name, new Set());
            listeners.get(name).add(callback);
        },
        removeEventListener(name, callback) {
            listeners.get(name)?.delete(callback);
        },
        dispatch(name, event = {}) {
            for (const callback of [...(listeners.get(name) ?? [])]) callback(event);
        },
        listenerCount() {
            return [...listeners.values()].reduce((total, callbacks) => total + callbacks.size, 0);
        }
    });
}

function setup(options = {}) {
    const observers = [];
    const resizeObservers = [];
    const tracks = [{ stops: 0, stop() { this.stops++; } }];
    const video = {
        srcObject: null,
        videoWidth: options.nativeWidth ?? 1920,
        videoHeight: options.nativeHeight ?? 1080,
        clientWidth: options.videoCssWidth ?? 280,
        clientHeight: options.videoCssHeight ?? 210
    };
    const transforms = [];
    const context = options.noTransform ? {} : {
        setTransform(...values) {
            transforms.push(values);
            if (options.transformError) throw new Error("Canvas transform unsupported");
        }
    };
    const canvas = {
        width: options.canvasWidth ?? 240,
        height: options.canvasHeight ?? 80,
        getContext(name) {
            assert.equal(name, "2d");
            if (options.contextError) throw new Error("Canvas context unavailable");
            return options.noContext ? null : context;
        }
    };
    const frame = { clientWidth: options.viewWidth ?? 280 };
    const feed = {
        parentElement: frame,
        canvas: options.decoderCanvas ? canvas : null,
        querySelector(name) { return name === "canvas" ? this.canvas : name === "video" ? video : null; },
        style: { removeProperty(name) { delete this[name]; } },
        get clientWidth() { return Number.parseFloat(this.style.width) || frame.clientWidth; }
    };
    const zoom = eventTarget({ hidden: true });
    const input = eventTarget({ value: "", min: "", max: "", step: "", disabled: false });
    const output = { textContent: "" };
    const message = { textContent: "", hidden: true };
    const photoInput = eventTarget({ value: "", files: [], disabled: false, clicks: 0,
        click() { this.clicks++; } });
    let photoValue = "";
    Object.defineProperty(photoInput, "value", {
        get() { return photoValue; },
        set(value) { photoValue = value; if (value === "") this.files = []; }
    });
    const capture = eventTarget({ disabled: false });
    const captureLabel = { textContent: "Tomar foto" };
    const useCode = eventTarget({ disabled: false, hidden: true });
    const manual = eventTarget({ disabled: false });
    const live = eventTarget({ disabled: false });
    const photoMode = eventTarget({ disabled: false });
    const photoPanel = eventTarget({ hidden: false });
    const livePanel = eventTarget({ hidden: true });
    const liveStatus = { textContent: "" };
    const photoHelp = { textContent: "", hidden: false };
    const status = eventTarget({ textContent: "", hidden: true });
    const result = eventTarget({ textContent: "", hidden: true });
    const resultCode = { textContent: "" };
    const photoReader = { id: "photo-reader", style: {}, children: [],
        replaceChildren() { this.children = []; }, querySelector() { return null; } };
    const sections = new Map();
    const elements = new Map([
        ["[data-barcode-zoom]", zoom],
        ["[data-barcode-zoom-input]", input],
        ["[data-barcode-zoom-value]", output],
        ["[data-barcode-zoom-message]", message],
        ["[data-barcode-file]", photoInput],
        ["[data-barcode-capture]", capture],
        ["[data-barcode-capture-label]", captureLabel],
        ["[data-barcode-use]", useCode],
        ["[data-barcode-manual]", manual],
        ["[data-barcode-live]", live],
        ["[data-barcode-back-photo]", photoMode],
        ["[data-barcode-photo]", photoPanel],
        ["[data-barcode-live-panel]", livePanel],
        ["[data-barcode-live-status]", liveStatus],
        ["[data-barcode-photo-help]", photoHelp],
        ["[data-barcode-photo-status]", status],
        ["[data-barcode-result]", result],
        ["[data-barcode-code]", resultCode],
        ["[data-barcode-file-reader]", photoReader],
        [".barcode-camera-feed", feed]
    ]);
    const dialog = eventTarget({
        open: false,
        isConnected: true,
        showModal() { this.open = true; },
        close() { this.open = false; this.dispatch("close"); },
        contains: element => element === video || [...elements.values(), ...sections.values()].includes(element),
        querySelectorAll: selector => selector === "video" ? [video] : [...sections.values()],
        querySelector: selector => elements.get(selector) ?? sections.get(selector) ?? null
    });
    const calls = [];
    const callbackErrors = [];
    const reference = {
        async invokeMethodAsync(...args) {
            calls.push(args);
            try {
                if (options.invoke) await options.invoke(args);
            } catch (error) {
                callbackErrors.push(error);
                throw error;
            }
        }
    };
    const instances = [];
    class FakeScanner {
        constructor(id, config) {
            this.id = id;
            this.config = config;
            this.stops = 0;
            this.starts = 0;
            this.clears = 0;
            this.applied = [];
            this.fileScans = [];
            this.settings = { ...(options.settings ?? {}) };
            instances.push(this);
            if (options.noCapabilitiesMethod) this.getRunningTrackCapabilities = undefined;
            if (options.noSettingsMethod) this.getRunningTrackSettings = undefined;
        }
        start(camera, config, success, failure) {
            this.starts++;
            this.camera = camera;
            this.startConfig = config;
            this.startFeedWidth = feed.clientWidth;
            this.decoderBox = config.qrbox(feed.clientWidth, feed.clientWidth * 0.75);
            this.success = success;
            this.failure = failure;
            video.srcObject = { getTracks: () => tracks };
            return options.start?.(this) ?? Promise.resolve();
        }
        getRunningTrackCapabilities() {
            if (options.capabilitiesError) throw new Error("Capabilities unavailable");
            return options.capabilities ?? {};
        }
        getRunningTrackSettings() {
            if (options.settingsError) throw new Error("Settings unavailable");
            return { ...this.settings };
        }
        async applyVideoConstraints(constraints) {
            this.applied.push(constraints);
            await options.apply?.(constraints, this);
            const advanced = constraints.advanced?.[0];
            if (advanced?.zoom !== undefined) {
                this.settings.zoom = options.appliedZoom ?? advanced.zoom;
            }
        }
        async stop() { this.stops++; await options.stop?.(this, video); }
        scanFileV2(file, showImage) {
            this.fileScans.push({ file, showImage });
            assert.equal(video.srcObject, null, "scanFile nunca coincide con una cámara activa");
            // 2.3.8 crea dos URLs para el mismo archivo; ambas deben liberarse.
            URL.createObjectURL(file);
            const url = URL.createObjectURL(file);
            const image = new window.Image();
            image.src = url;
            return options.scanFile?.(file, showImage, this, image) ?? Promise.resolve({ decodedText: "7501234567893" });
        }
        clear() { this.clears++; photoReader.replaceChildren(); }
    }
    const browser = eventTarget({
        isSecureContext: true,
        Html5Qrcode: FakeScanner,
        Html5QrcodeSupportedFormats: {
            EAN_13: 9, EAN_8: 10, UPC_A: 14, UPC_E: 15, CODE_128: 5
        }
    });
    const origin = { isConnected: true, focuses: 0, focus() { this.focuses++; } };
    const manualField = { isConnected: true, value: "EXISTING-00042", focuses: 0, focus() { this.focuses++; } };
    const consumer = { querySelector: () => manualField };
    dialog.parentElement = consumer;
    const page = eventTarget({ body: {}, activeElement: origin, hidden: false });
    page.getElementById = id => id === photoReader.id ? photoReader : id === "visor" ? feed : null;
    globalThis.window = browser;
    globalThis.document = page;
    globalThis.MutationObserver = class {
        constructor(callback) { this.callback = callback; this.disconnected = false; observers.push(this); }
        observe() {}
        disconnect() { this.disconnected = true; }
        notify() { this.callback(); }
    };
    globalThis.ResizeObserver = options.noResizeObserver ? undefined : class {
        constructor(callback) { this.callback = callback; this.disconnected = false; resizeObservers.push(this); }
        observe(target) { this.target = target; }
        disconnect() { this.disconnected = true; }
        notify() { if (!this.disconnected) this.callback(); }
    };
    Object.defineProperty(globalThis, "navigator", {
        configurable: true,
        value: { mediaDevices: { getUserMedia() {} } }
    });
    const objectUrls = new Set();
    const createdUrls = [];
    const revokedUrls = [];
    globalThis.URL = class extends nativeURL {
        static createObjectURL(file) {
            const url = "blob:local-test-" + (createdUrls.length + 1);
            objectUrls.add(url); createdUrls.push({ url, file }); return url;
        }
        static revokeObjectURL(url) { objectUrls.delete(url); revokedUrls.push(url); }
    };
    const images = [];
    globalThis.Image = class {
        constructor() { images.push(this); this.onload = null; this.onerror = null; }
        set src(value) { this.source = value; }
        get src() { return this.source; }
        removeAttribute(name) { if (name === "src") this.source = ""; }
    };
    browser.URL = globalThis.URL;
    browser.Image = globalThis.Image;
    const httpCalls = [];
    globalThis.fetch = (...args) => { httpCalls.push(args); throw new Error("Photo must not use HTTP"); };
    currentState = { dialog, reference, calls, callbackErrors, instances, tracks, observers, resizeObservers, frame, feed, video, canvas, context, transforms, zoom, input, output, message, browser, page, origin, manualField, photoInput, capture, captureLabel, useCode, manual, live, photoMode, photoPanel, livePanel, liveStatus, photoHelp, status, result, resultCode, photoReader, sections, objectUrls, createdUrls, revokedUrls, images, httpCalls };
    return currentState;
}

async function settle() {
    // Use the real timer even while testing the scanner's 45-second timeout.
    await new Promise(resolve => nativeSetTimeout(resolve, 0));
    await new Promise(resolve => nativeSetTimeout(resolve, 0));
}

afterEach(async () => {
    if (currentState) await scannerModule.cerrar(currentState.dialog);
    await settle();
    if (currentState) assert.deepEqual(currentState.callbackErrors, [], "las aserciones de callbacks no se ocultan por el fallback de Blazor");
    currentState = undefined;
    globalThis.URL = nativeURL;
    globalThis.Image = nativeImage;
    globalThis.fetch = nativeFetch;
});

async function open(state) {
    const result = await scannerModule.abrir(state.dialog, "visor", state.reference);
    if (result !== "ready") return result;
    const liveResult = await scannerModule.escanearEnVivo(state.dialog);
    await settle();
    return liveResult;
}

async function openPhoto(state) {
    const result = await scannerModule.abrir(state.dialog, "visor", state.reference);
    await settle();
    return result;
}

async function selectPhoto(state, file = { name: "barcode.jpg", type: "image/jpeg", size: 245000 }) {
    state.photoInput.value = "C:\\fakepath\\" + file.name;
    state.photoInput.files = [file];
    state.photoInput.dispatch("change", { target: state.photoInput });
    await settle();
    return file;
}

async function confirmCode(state) {
    assert.deepEqual(state.calls, [], "OnDetected no se dispara antes de confirmar");
    state.useCode.dispatch("click", { preventDefault() {} });
    await settle();
}

function assertCameraIdeals(constraints) {
    assert.deepEqual(constraints.facingMode, { ideal: "environment" });
    assert.deepEqual(constraints.width, { ideal: 1920 });
    assert.deepEqual(constraints.height, { ideal: 1080 });
}
function zoomRequests(scanner) {
    return scanner.applied
        .map(constraints => constraints.advanced?.[0]?.zoom)
        .filter(value => value !== undefined);
}

function changeZoom(state, value) {
    state.input.value = String(value);
    state.input.dispatch("input", { target: state.input });
}

function assertReleased(state, callbackExpected = false) {
    assert.equal(state.instances[0].stops, 1);
    assert.equal(state.instances[0].clears, 1);
    assert.equal(state.tracks[0].stops, 1);
    assert.equal(state.dialog.open, false);
    assert.equal(state.dialog.listenerCount(), 0);
    assert.equal(state.browser.listenerCount(), 0);
    assert.equal(state.page.listenerCount(), 0);
    assert.equal(state.input.listenerCount(), 0);
    assert.equal(state.observers[0].disconnected, true);
    assert.ok(state.resizeObservers.every(observer => observer.disconnected));
    assert.equal(state.origin.focuses, 1);
    if (!callbackExpected) assert.deepEqual(state.calls, []);
}

test("la región del decoder es horizontal, responsive y queda dentro del stream", async () => {
    const state = setup();
    assert.equal(await open(state), "started");
    const qrbox = state.instances[0].startConfig.qrbox;
    assert.equal(typeof qrbox, "function");
    let previousWidth = 0;
    for (const [width, height] of [[240, 180], [280, 210], [320, 240], [500, 360], [1280, 720]]) {
        const box = qrbox(width, height);
        assert.ok(Number.isInteger(box.width) && Number.isInteger(box.height));
        assert.ok(box.width > 0 && box.width <= width);
        assert.ok(box.height > 0 && box.height <= height);
        assert.ok(box.width / box.height >= 2.4 && box.width / box.height <= 3.2);
        assert.ok(box.width >= Math.min(width * 0.75, 400), "ocupa gran parte del ancho en móvil");
        assert.ok(box.width >= previousWidth);
        previousWidth = box.width;
    }
    for (const [width, height] of [[140, 60], [256, 60], [100, 100], [320, 80], [120, 300]]) {
        const box = qrbox(width, height);
        assert.ok(box.width > 0 && box.width <= width);
        assert.ok(box.height >= 50 && box.height <= height);
        assert.ok(box.width >= 50 && box.width <= width);
    }
});

test("prefiere cámara trasera con resolución ideal flexible, diez FPS y sólo formatos existentes", async () => {
    const state = setup();
    assert.equal(await open(state), "started");
    const scanner = state.instances[0];
    assert.equal(scanner.camera.facingMode, "environment");
    assert.equal(scanner.startConfig.fps, 10);
    assert.equal(scanner.startConfig.disableFlip, true);
    assert.deepEqual(scanner.startConfig.videoConstraints.facingMode, { ideal: "environment" });
    assert.deepEqual(scanner.startConfig.videoConstraints.width, { ideal: 1920 });
    assert.deepEqual(scanner.startConfig.videoConstraints.height, { ideal: 1080 });
    assert.equal(scanner.config.useBarCodeDetectorIfSupported, false);
    assert.deepEqual(scanner.config.formatsToSupport, [9, 10, 14, 15, 5]);
});

for (const [description, options] of [
    ["sin capacidad zoom", { capabilities: {} }],
    ["sin API de capacidades", { noCapabilitiesMethod: true }],
    ["con API de capacidades que falla", { capabilitiesError: true }],
    ["con zoom fijo", { capabilities: { zoom: { min: 1, max: 1, step: 0.1 } } }],
    ["con rango invertido", { capabilities: { zoom: { min: 3, max: 1, step: 0.1 } } }],
    ["con mínimo no positivo", { capabilities: { zoom: { min: 0, max: 3, step: 0.1 } } }],
    ["con step cero", { capabilities: { zoom: { min: 1, max: 3, step: 0 } } }],
    ["sin step", { capabilities: { zoom: { min: 1, max: 3 } } }],
    ["con step mayor que el rango", { capabilities: { zoom: { min: 1, max: 1.2, step: 1 } } }],
    ["con límites no finitos", { capabilities: { zoom: { min: 1, max: Infinity, step: 0.1 } } }],
    ["con capacidades no numéricas", { capabilities: { zoom: { min: "1", max: "3", step: "0.1" } } }]
]) {
    test("inicia " + description + " sin mostrar un control inútil", async () => {
        const state = setup(options);
        assert.equal(await open(state), "started");
        assert.equal(state.zoom.hidden, true);
        assert.deepEqual(zoomRequests(state.instances[0]), []);
        state.instances[0].success("0012345678905");
        await settle();
        await confirmCode(state);
        assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "0012345678905"]]);
        assertReleased(state, true);
    });
}

for (const [capability, expected] of [
    [{ min: 1, max: 4, step: 0.1 }, 1.8],
    [{ min: 2, max: 4, step: 0.25 }, 2],
    [{ min: 1, max: 1.6, step: 0.25 }, 1.5],
    [{ min: 1.05, max: 3, step: 0.2 }, 1.85],
    [{ min: 1, max: 2, step: 0.3 }, 1.9]
]) {
    test("el zoom inicial respeta min/max/step " + JSON.stringify(capability), async () => {
        const state = setup({ capabilities: { zoom: capability }, settings: { zoom: capability.min } });
        assert.equal(await open(state), "started");
        const requests = zoomRequests(state.instances[0]);
        assert.equal(requests.length, 1);
        assert.ok(Math.abs(requests[0] - expected) < 1e-8);
        assert.ok(requests[0] >= capability.min && requests[0] <= capability.max);
        assert.ok(Math.abs((requests[0] - capability.min) / capability.step - Math.round((requests[0] - capability.min) / capability.step)) < 1e-8);
        assert.equal(state.zoom.hidden, false);
        assert.equal(Number(state.input.min), capability.min);
        assert.ok(Number(state.input.max) <= capability.max);
        assert.equal(Number(state.input.step), capability.step);
        assert.equal(Number(state.input.value), expected);
        assert.match(state.output.textContent, /×$/);
        assert.equal(state.input.disabled, false);
        assert.equal(state.instances[0].starts, 1);
    });
}

test("cambiar zoom lo limita y alinea al step sin reiniciar la cámara", async () => {
    const state = setup({ capabilities: { zoom: { min: 1, max: 3, step: 0.25 } } });
    assert.equal(await open(state), "started");
    changeZoom(state, 2.17);
    await settle();
    changeZoom(state, 99);
    await settle();
    changeZoom(state, -1);
    await settle();
    const requests = zoomRequests(state.instances[0]);
    assert.ok(Math.abs(requests[1] - 2.25) < 1e-8);
    assert.equal(requests[2], 3);
    assert.equal(requests[3], 1);
    assert.equal(state.instances[0].starts, 1);
    assert.equal(state.instances.length, 1);
    assert.equal(state.instances[0].stops, 0);
    assert.equal(state.input.value, "1");
    assert.equal(state.input.disabled, false);
    assert.match(state.output.textContent, /^1(?:\.0+)?×$/);
});

test("el valor visible refleja el ajuste que realmente informa la cámara", async () => {
    const state = setup({
        capabilities: { zoom: { min: 1, max: 4, step: 0.1 } },
        appliedZoom: 1.7
    });
    assert.equal(await open(state), "started");
    assert.equal(Number(state.input.value), 1.7);
    assert.match(state.output.textContent, /^1[.,]7×$/);
});

test("si settings no está disponible se mantiene el valor aceptado por applyVideoConstraints", async () => {
    const state = setup({
        capabilities: { zoom: { min: 1, max: 4, step: 0.1 } },
        noSettingsMethod: true
    });
    assert.equal(await open(state), "started");
    assert.equal(Number(state.input.value), 1.8);
    assert.equal(state.zoom.hidden, false);
});

test("un fallo al aplicar zoom inicial conserva el scanner y la detección", async () => {
    const state = setup({
        capabilities: { zoom: { min: 1, max: 4, step: 0.1 } },
        settings: { zoom: 1 },
        apply: async () => { throw new Error("OverconstrainedError"); }
    });
    assert.equal(await open(state), "started");
    assert.equal(state.instances[0].starts, 1);
    assert.equal(state.instances[0].stops, 0);
    state.instances[0].success("CODE-128 / 00042");
    await settle();
    await confirmCode(state);
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "CODE-128 / 00042"]]);
    assertReleased(state, true);
});

test("un fallo al cambiar zoom no publica un valor no aplicado ni rompe el escaneo", async () => {
    let zoomCalls = 0;
    const state = setup({
        capabilities: { zoom: { min: 1, max: 4, step: 0.1 } },
        apply: async constraints => {
            if (constraints.advanced?.[0]?.zoom !== undefined && ++zoomCalls > 1) {
                throw new Error("Camera rejected zoom");
            }
        }
    });
    assert.equal(await open(state), "started");
    changeZoom(state, 3);
    await settle();
    assert.notEqual(state.output.textContent, "3×");
    assert.notEqual(state.output.textContent, "3.0×");
    assert.equal(state.instances[0].stops, 0);
    assert.equal(state.instances[0].starts, 1);
    state.instances[0].success("12345670");
    await settle();
    await confirmCode(state);
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "12345670"]]);
    assertReleased(state, true);
});

test("los cambios rápidos de zoom se serializan y conservan el último valor solicitado", async () => {
    let completeChange;
    let zoomCalls = 0;
    const state = setup({
        capabilities: { zoom: { min: 1, max: 4, step: 0.1 } },
        apply: constraints => {
            if (constraints.advanced?.[0]?.zoom !== undefined && ++zoomCalls === 2) {
                return new Promise(resolve => { completeChange = resolve; });
            }
        }
    });
    assert.equal(await open(state), "started");
    changeZoom(state, 2);
    await settle();
    assert.equal(state.input.disabled, true);
    changeZoom(state, 2.5);
    changeZoom(state, 3);
    assert.deepEqual(zoomRequests(state.instances[0]), [1.8, 2]);
    completeChange();
    await settle();
    assert.deepEqual(zoomRequests(state.instances[0]), [1.8, 2, 3]);
    assert.equal(Number(state.input.value), 3);
    assert.equal(state.input.disabled, false);
    assert.equal(state.instances[0].starts, 1);
});

test("sólo intenta enfoque continuo cuando aparece en las capacidades", async () => {
    const state = setup({ capabilities: { focusMode: ["manual", "continuous"] } });
    assert.equal(await open(state), "started");
    assert.equal(state.instances[0].applied.length, 1);
    assert.deepEqual(state.instances[0].applied[0].advanced, [{ focusMode: "continuous" }]);
    assertCameraIdeals(state.instances[0].applied[0]);
    assert.equal(state.zoom.hidden, true);
});

test("un fallo de enfoque continuo no impide zoom ni detección", async () => {
    const state = setup({
        capabilities: { focusMode: ["continuous"], zoom: { min: 1, max: 4, step: 0.1 } },
        apply: async constraints => {
            if (constraints.advanced?.[0]?.focusMode) throw new Error("Focus unsupported");
        }
    });
    assert.equal(await open(state), "started");
    assert.deepEqual(zoomRequests(state.instances[0]), [1.8]);
    assert.equal(state.zoom.hidden, false);
    state.instances[0].success("7501234567890");
    await settle();
    await confirmCode(state);
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "7501234567890"]]);
});

test("detiene cámara y listeners antes de entregar exactamente un código y permite reabrir", async () => {
    let state;
    state = setup({ invoke: args => {
        if (args[0] === "FinalizarEscaneo") {
            assert.equal(state.tracks[0].stops, 1);
            assert.equal(state.page.listenerCount(), 0);
            assert.equal(state.browser.listenerCount(), 0);
        }
    } });
    assert.equal(await open(state), "started");
    state.instances[0].success(" 000123456789 / CODE128 ");
    state.instances[0].success("otro");
    await settle();
    await confirmCode(state);
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", " 000123456789 / CODE128 "]]);
    assertReleased(state, true);

    assert.equal(await open(state), "started");
    scannerModule.cerrar(state.dialog);
    await settle();
    assert.equal(state.instances[1].stops, 1);
    assert.equal(state.dialog.open, false);
});

test("resultados vacíos y fallos de un fotograma no cierran la cámara", async () => {
    const state = setup();
    assert.equal(await open(state), "started");
    state.instances[0].success("");
    state.instances[0].failure("No barcode in this frame");
    await settle();
    assert.equal(state.instances[0].stops, 0);
    assert.equal(state.dialog.open, true);
    assert.deepEqual(state.calls, []);
});

test("evita scanners simultáneos sin alterar el activo", async () => {
    const state = setup();
    assert.equal(await open(state), "started");
    const otherDialog = eventTarget({ showModal() { throw new Error("Must not open"); } });
    assert.equal(await scannerModule.abrir(otherDialog, "otro", state.reference), "busy");
    scannerModule.cerrar(otherDialog);
    assert.equal(state.instances.length, 1);
    assert.equal(state.dialog.open, true);
    assert.equal(state.instances[0].stops, 0);
});

test("cancelar durante el permiso impide callbacks y libera cámara al iniciar", async () => {
    let resolveStart;
    let starts = 0;
    const state = setup({ start: () => ++starts === 1
        ? new Promise(resolve => { resolveStart = resolve; })
        : Promise.resolve() });
    const opening = (async () => {
        assert.equal(await scannerModule.abrir(state.dialog, "visor", state.reference), "ready");
        return scannerModule.escanearEnVivo(state.dialog);
    })();
    await settle();
    assert.equal(state.dialog.open, true);
    scannerModule.cerrar(state.dialog);
    assert.equal(state.dialog.open, false);
    resolveStart();
    assert.equal(await opening, "cancelled");
    await settle();
    assertReleased(state);
    assert.equal(await open(state), "started");
});

test("cerrar durante un ajuste de zoom libera tracks y no restaura el control", async () => {
    let resolveZoom;
    let zoomCalls = 0;
    const state = setup({
        capabilities: { zoom: { min: 1, max: 4, step: 0.1 } },
        apply: constraints => {
            if (constraints.advanced?.[0]?.zoom !== undefined && ++zoomCalls === 2) {
                return new Promise(resolve => { resolveZoom = resolve; });
            }
        }
    });
    assert.equal(await open(state), "started");
    changeZoom(state, 2.5);
    await settle();
    scannerModule.cerrar(state.dialog);
    resolveZoom();
    await settle();
    assertReleased(state);
    assert.equal(state.zoom.hidden, true);
});

test("Escape solicita la cancelación de Blazor y conserva el retorno de foco", async () => {
    let state;
    state = setup({ invoke: args => {
        // La liberación local de Escape debe funcionar incluso sin respuesta de Blazor.
    } });
    assert.equal(await open(state), "started");
    let prevented = false;
    state.dialog.dispatch("cancel", { preventDefault() { prevented = true; } });
    await settle();
    assert.equal(prevented, true);
    assert.deepEqual(state.calls, [["CancelarDesdeTeclado"]]);
    assertReleased(state, true);
});

for (const event of ["visibilitychange", "pagehide"]) {
    test(event + " detiene cámara y entrega una sola notificación de fondo", async () => {
        const state = setup();
        assert.equal(await open(state), "started");
        if (event === "visibilitychange") {
            state.page.hidden = true;
            state.page.dispatch(event);
            state.page.dispatch(event);
        } else {
            state.browser.dispatch(event);
            state.browser.dispatch(event);
        }
        await settle();
        assert.deepEqual(state.calls, [["FinalizarEscaneo", "background", null]]);
        assertReleased(state, true);
    });
}

test("desmontar el componente libera cámara sin callback ni listeners huérfanos", async () => {
    const state = setup();
    assert.equal(await open(state), "started");
    state.dialog.isConnected = false;
    state.observers[0].notify();
    await settle();
    assert.equal(state.instances[0].stops, 1);
    assert.equal(state.tracks[0].stops, 1);
    assert.equal(state.page.listenerCount(), 0);
    assert.equal(state.browser.listenerCount(), 0);
    assert.equal(state.input.listenerCount(), 0);
    assert.equal(state.observers[0].disconnected, true);
    assert.ok(state.resizeObservers.every(observer => observer.disconnected));
    assert.deepEqual(state.calls, []);
});

test("el timeout existente libera cámara y no entrega códigos posteriores", async () => {
    const originalSetTimeout = globalThis.setTimeout;
    const originalClearTimeout = globalThis.clearTimeout;
    let timeoutCallback;
    let cleared = false;
    const timer = {};
    globalThis.setTimeout = (callback, delay) => {
        assert.equal(delay, 45000);
        timeoutCallback = callback;
        return timer;
    };
    globalThis.clearTimeout = value => { if (value === timer) cleared = true; };
    try {
        const state = setup();
        assert.equal(await open(state), "started");
        timeoutCallback();
        await settle();
        state.instances[0].success("late-code");
        await settle();
        assert.deepEqual(state.calls, []);
        assert.equal(cleared, true);
        assert.equal(state.dialog.open, true);
        assert.equal(state.photoPanel.hidden, false);
        assert.match(state.status.textContent, /detectar|código/i);
        await scannerModule.cerrar(state.dialog);
        assertReleased(state);
    } finally {
        globalThis.setTimeout = originalSetTimeout;
        globalThis.clearTimeout = originalClearTimeout;
    }
});

test("permiso denegado se comunica y permite reintentar sin scanner activo", async () => {
    const state = setup({ start: () => Promise.reject(Object.assign(new Error("Permission denied"), { name: "NotAllowedError" })) });
    assert.equal(await open(state), "denied");
    assert.equal(state.dialog.open, true);
    assert.equal(state.tracks[0].stops, 1);
    assert.equal(state.photoPanel.hidden, false);
    assert.deepEqual(state.calls, []);
    assert.equal(await scannerModule.escanearEnVivo(state.dialog), "denied");
    await scannerModule.cerrar(state.dialog);
    assert.equal(state.dialog.open, false);
    assert.equal(state.origin.focuses, 1);
});

test("reporta cámara inexistente y navegador incompatible conservando la alternativa de foto", async () => {
    const state = setup({ start: () => Promise.reject(Object.assign(new Error("Requested device not found"), { name: "NotFoundError" })) });
    assert.equal(await open(state), "no-camera");
    assert.equal(state.dialog.open, true);
    assert.equal(state.tracks[0].stops, 1);
    window.isSecureContext = false;
    assert.equal(await scannerModule.escanearEnVivo(state.dialog), "unsupported");
    assert.equal(state.dialog.open, true);
    assert.equal(state.instances.length, 1);
    await scannerModule.cerrar(state.dialog);
    assertReleased(state);
});
test("cerrar el dialog externamente libera la cámara incluso sin callback de Blazor", async () => {
    const state = setup();
    assert.equal(await open(state), "started");
    state.dialog.close();
    await settle();
    assert.deepEqual(state.calls, [["CancelarDesdeTeclado"]]);
    assertReleased(state, true);
});

test("cancelar mientras se configura zoom inicial no deja UI ni tracks activos", async () => {
    let resolveZoom;
    const state = setup({
        capabilities: { zoom: { min: 1, max: 4, step: 0.1 } },
        apply: () => new Promise(resolve => { resolveZoom = resolve; })
    });
    assert.equal(await open(state), "started");
    assert.equal(state.zoom.hidden, true);
    scannerModule.cerrar(state.dialog);
    await settle();
    assertReleased(state);
    resolveZoom();
    await settle();
    assert.equal(state.zoom.hidden, true);
    assert.equal(state.input.disabled, true);
    assert.equal(state.input.listenerCount(), 0);
    assert.deepEqual(state.calls, []);
});

test("un código detectado sincrónicamente durante start libera también los tracks", async () => {
    const state = setup({ start: scanner => {
        scanner.success("0001234567895");
        return Promise.resolve();
    } });
    assert.equal(await open(state), "started");
    await confirmCode(state);
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "0001234567895"]]);
    assertReleased(state, true);
});

test("conserva referencias a tracks aunque stop desmonte el video", async () => {
    const state = setup({ stop: (_scanner, video) => { video.srcObject = null; } });
    assert.equal(await open(state), "started");
    await scannerModule.cerrar(state.dialog);
    assertReleased(state);
});
test("cada cambio opcional conserva constraints flexibles de cámara y resolución", async () => {
    const state = setup({ capabilities: { zoom: { min: 1, max: 4, step: 0.1 } } });
    assert.equal(await open(state), "started");
    changeZoom(state, 2.5);
    await settle();
    assert.deepEqual(zoomRequests(state.instances[0]), [1.8, 2.5]);
    for (const applied of state.instances[0].applied) assertCameraIdeals(applied);
});

test("el enfoque continuo aceptado se conserva al iniciar y cambiar zoom", async () => {
    const state = setup({
        capabilities: { focusMode: ["continuous"], zoom: { min: 1, max: 4, step: 0.1 } }
    });
    assert.equal(await open(state), "started");
    changeZoom(state, 2.5);
    await settle();
    const applied = state.instances[0].applied;
    assert.equal(applied.length, 3);
    for (const constraints of applied) {
        assertCameraIdeals(constraints);
        assert.equal(constraints.advanced[0].focusMode, "continuous");
    }
    assert.deepEqual(zoomRequests(state.instances[0]), [1.8, 2.5]);
    assert.equal(state.instances[0].starts, 1);
});

test("el enfoque rechazado no se vuelve a imponer en las solicitudes de zoom", async () => {
    const state = setup({
        capabilities: { focusMode: ["continuous"], zoom: { min: 1, max: 4, step: 0.1 } },
        apply: async constraints => {
            if (constraints.advanced[0].focusMode) throw new Error("Rejected focus");
        }
    });
    assert.equal(await open(state), "started");
    changeZoom(state, 2.5);
    await settle();
    const zoomApplies = state.instances[0].applied.filter(constraints => constraints.advanced[0].zoom !== undefined);
    assert.equal(zoomApplies.length, 2);
    for (const constraints of zoomApplies) {
        assertCameraIdeals(constraints);
        assert.equal(constraints.advanced[0].focusMode, undefined);
    }
    assert.equal(state.zoom.hidden, false);
});

test("resize adapta juntos cámara y marco manteniendo el crop y sin reiniciar", async () => {
    const state = setup({ viewWidth: 280 });
    assert.equal(await open(state), "started");
    const scanner = state.instances[0];
    const originalBox = { ...scanner.decoderBox };
    const observer = state.resizeObservers[0];
    assert.equal(observer.target, state.frame);
    assert.equal(state.feed.style.width, "280px");
    assert.match(state.feed.style.transform, /scale\(1\)$/);
    state.frame.clientWidth = 500;
    observer.notify();
    assert.equal(state.feed.clientWidth, 280, "el decoder conserva las dimensiones internas originales");
    assert.equal(state.feed.style.width, "280px");
    assert.match(state.feed.style.transform, /scale\(1\.7857142857142858\)$/);
    assert.deepEqual(scanner.decoderBox, originalBox);
    assert.equal(scanner.starts, 1);
    assert.equal(state.instances.length, 1);
    await scannerModule.cerrar(state.dialog);
    assert.equal(observer.disconnected, true);
    const closedTransform = state.feed.style.transform;
    state.frame.clientWidth = 180;
    observer.callback();
    assert.equal(state.feed.style.transform, closedTransform, "un callback pendiente no modifica una sesión cerrada");

    assert.equal(await open(state), "started");
    assert.equal(state.instances[1].startFeedWidth, 180, "reabrir elimina la anchura fijada de la sesión anterior");
    assert.equal(state.feed.style.width, "180px");
    assert.match(state.feed.style.transform, /scale\(1\)$/);
    assert.equal(state.resizeObservers.length, 2);
    assert.equal(state.resizeObservers[0].disconnected, true);
    await scannerModule.cerrar(state.dialog);
    assert.ok(state.resizeObservers.every(item => item.disconnected));
    assert.equal(state.browser.listenerCount(), 0);
});

test("fallback resize sin ResizeObserver se elimina al cerrar y reabrir", async () => {
    const state = setup({ viewWidth: 280, noResizeObserver: true });
    assert.equal(await open(state), "started");
    state.frame.clientWidth = 420;
    state.browser.dispatch("resize");
    assert.equal(state.feed.clientWidth, 280);
    assert.match(state.feed.style.transform, /scale\(1\.5\)$/);
    assert.equal(state.instances[0].starts, 1);
    await scannerModule.cerrar(state.dialog);
    assertReleased(state);
    const closedTransform = state.feed.style.transform;
    state.frame.clientWidth = 210;
    state.browser.dispatch("resize");
    assert.equal(state.feed.style.transform, closedTransform);
    assert.equal(await open(state), "started");
    assert.equal(state.instances[1].startFeedWidth, 210);
    assert.equal(state.feed.style.width, "210px");
    await scannerModule.cerrar(state.dialog);
    assert.equal(state.browser.listenerCount(), 0);
});
test("los límites decimales muy cercanos nunca producen zoom fuera del rango", async () => {
    const capability = { min: 1.000000000001, max: 1.000000000005, step: 0.000000000001 };
    const state = setup({ capabilities: { zoom: capability }, settings: { zoom: capability.min } });
    assert.equal(await open(state), "started");
    assert.equal(state.zoom.hidden, false);
    for (const requested of zoomRequests(state.instances[0])) {
        assert.ok(requested >= capability.min);
        assert.ok(requested <= capability.max);
    }
    assert.ok(Number(state.input.value) >= capability.min);
    assert.ok(Number(state.input.value) <= capability.max);
    changeZoom(state, 100);
    await settle();
    assert.ok(zoomRequests(state.instances[0]).every(value => value >= capability.min && value <= capability.max));
});
for (const [description, nativeWidth, nativeHeight, cssWidth, cssHeight, scale] of [
    ["detalle nativo 4x limitado a 2x", 1280, 720, 320, 180, 2],
    ["detalle nativo 2x", 640, 360, 320, 180, 2],
    ["detalle nativo 1,5x", 480, 270, 320, 180, 1.5],
    ["detalle nativo 1x", 320, 180, 320, 180, 1],
    ["sin detalle extra nativo", 256, 144, 320, 180, 1]
]) {
    test("el backing del decoder aprovecha " + description + " sin cambiar qrbox ni zoom", async () => {
        const state = setup({
            decoderCanvas: true,
            nativeWidth, nativeHeight,
            videoCssWidth: cssWidth, videoCssHeight: cssHeight
        });
        assert.equal(await open(state), "started");
        assert.equal(state.canvas.width, 240 * scale);
        assert.equal(state.canvas.height, 80 * scale);
        assert.equal(state.transforms.length, scale > 1 ? 1 : 0);
        if (scale > 1) {
            assert.deepEqual(state.transforms[0], [scale, 0, 0, scale, 0, 0]);
        }
        const originalBox = { ...state.instances[0].decoderBox };
        state.observers[0].notify();
        state.observers[0].notify();
        assert.equal(state.canvas.width, 240 * scale, "múltiples mutaciones no acumulan escala");
        assert.equal(state.canvas.height, 80 * scale);
        assert.deepEqual(state.instances[0].decoderBox, originalBox);
        assert.equal(state.instances[0].starts, 1);
        assert.deepEqual(zoomRequests(state.instances[0]), []);
        assert.equal(state.zoom.hidden, true);
        state.instances[0].success("0001234567895");
        await settle();
        await confirmCode(state);
        assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "0001234567895"]]);
        assertReleased(state, true);
    });
}

test("canvas creado después de playing se adapta una vez y no se modifica tras cerrar", async () => {
    const state = setup();
    assert.equal(await open(state), "started");
    assert.equal(state.feed.canvas, null);
    state.feed.canvas = state.canvas;
    state.observers[0].notify();
    assert.equal(state.canvas.width, 480);
    assert.equal(state.canvas.height, 160);
    assert.deepEqual(state.transforms, [[2, 0, 0, 2, 0, 0]]);
    state.observers[0].notify();
    assert.equal(state.canvas.width, 480);
    assert.equal(state.transforms.length, 1);
    await scannerModule.cerrar(state.dialog);
    const lateCanvas = { ...state.canvas, width: 240, height: 80 };
    state.feed.canvas = lateCanvas;
    state.observers[0].callback();
    assert.equal(lateCanvas.width, 240);
    assert.equal(lateCanvas.height, 80);
    assert.equal(state.transforms.length, 1);
    assertReleased(state);
});

for (const [description, options] of [
    ["getContext que falla", { contextError: true }],
    ["sin contexto", { noContext: true }],
    ["sin setTransform", { noTransform: true }],
    ["setTransform que falla", { transformError: true }]
]) {
    test("mantiene el decoder original con " + description + " y sigue detectando", async () => {
        const state = setup({ decoderCanvas: true, ...options });
        assert.equal(await open(state), "started");
        assert.equal(state.canvas.width, 240);
        assert.equal(state.canvas.height, 80);
        state.observers[0].notify();
        assert.equal(state.canvas.width, 240);
        assert.equal(state.canvas.height, 80);
        if (options.transformError) assert.equal(state.transforms.length, 1, "un transform fallido no se reintenta en cada mutación");
        state.instances[0].success("CODE128-00042");
        await settle();
        await confirmCode(state);
        assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "CODE128-00042"]]);
        assertReleased(state, true);
    });
}
for (const [description, options] of [
    ["ancho CSS cero", { videoCssWidth: 0 }],
    ["alto CSS cero", { videoCssHeight: 0 }],
    ["metadata nativa no disponible", { nativeWidth: 0, nativeHeight: 0 }]
]) {
    test("espera dimensiones válidas con " + description + " sin modificar ni bloquear el decoder", async () => {
        const state = setup({ decoderCanvas: true, ...options });
        assert.equal(await open(state), "started");
        assert.equal(state.canvas.width, 240);
        assert.equal(state.canvas.height, 80);
        assert.deepEqual(state.transforms, []);
        assert.equal(state.instances[0].starts, 1);
        state.video.clientWidth = 320;
        state.video.clientHeight = 180;
        state.video.videoWidth = 1280;
        state.video.videoHeight = 720;
        state.observers[0].notify();
        assert.equal(state.canvas.width, 480);
        assert.equal(state.canvas.height, 160);
        assert.deepEqual(state.transforms, [[2, 0, 0, 2, 0, 0]]);
        assert.equal(state.instances[0].starts, 1);
        state.instances[0].success("CODE128-00042");
        await settle();
        await confirmCode(state);
        assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "CODE128-00042"]]);
        assertReleased(state, true);
    });
}

function assertPhotoResourcesReleased(state) {
    assert.equal(state.objectUrls.size, 0, "todos los object URLs locales son revocados");
    assert.equal(state.photoInput.value, "");
    assert.deepEqual(state.photoInput.files, [], "limpiar value libera el FileList nativo");
    assert.equal(state.photoReader.children.length, 0, "no quedan canvas de análisis");
    assert.equal(state.httpCalls.length, 0, "ninguna fotografía se envía por HTTP");
    for (const image of state.images) {
        assert.equal(image.onload, null, "se libera el listener de carga de la imagen");
        assert.equal(image.onerror, null, "se libera el listener de error de la imagen");
    }
}

function fileScans(state) {
    return state.instances.flatMap(scanner => scanner.fileScans);
}

test("abrir presenta fotografía como modo principal sin pedir permiso ni iniciar cámara", async () => {
    const state = setup();
    assert.equal(await openPhoto(state), "ready");
    assert.equal(state.dialog.open, true);
    assert.equal(state.instances.length, 0);
    assert.equal(state.video.srcObject, null);
    assert.equal(state.photoPanel.hidden, false);
    assert.equal(state.livePanel.hidden, true);
    assert.equal(state.zoom.hidden, true);
    assert.equal(state.result.hidden, true);
    assert.deepEqual(state.calls, []);
});

test("la acción Tomar foto activa síncronamente el input accesible de imagen", async () => {
    const state = setup();
    assert.equal(await openPhoto(state), "ready");
    state.capture.dispatch("click", { preventDefault() {} });
    assert.equal(state.photoInput.clicks, 1, "el click no pierde el gesto del usuario mediante un await");
    assert.equal(state.instances.length, 0);
    const razor = await readFile(new URL("../src/ResellManager.Web/Components/Shared/BarcodeScanner.razor", import.meta.url), "utf8");
    assert.match(razor, /type="file" accept="image\/\*" capture="environment"/);
    assert.match(razor, /aria-label="Tomar o seleccionar una foto del código de barras"/);
});

test("cancelar el selector nativo no inicia análisis ni cierra el diálogo", async () => {
    const state = setup();
    assert.equal(await openPhoto(state), "ready");
    state.photoInput.files = [];
    state.photoInput.value = "";
    state.photoInput.dispatch("change", { target: state.photoInput });
    await settle();
    assert.equal(state.instances.length, 0);
    assert.equal(state.dialog.open, true);
    assert.equal(state.capture.disabled, false);
    assert.deepEqual(state.calls, []);
});

test("scanFileV2 procesa localmente el archivo original sin preview ni cámara", async () => {
    const state = setup();
    assert.equal(await openPhoto(state), "ready");
    const file = await selectPhoto(state);
    assert.equal(fileScans(state).length, 1);
    assert.equal(fileScans(state)[0].file, file, "no se reduce ni transforma la foto antes de scanFileV2");
    assert.equal(fileScans(state)[0].showImage, false);
    assert.equal(state.instances[0].id, state.photoReader.id, "el lector de archivo usa un host separado");
    assert.equal(state.instances[0].config.useBarCodeDetectorIfSupported, false);
    assert.deepEqual(state.instances[0].config.formatsToSupport, [9, 10, 14, 15, 5]);
    assert.equal(state.instances[0].starts, 0);
    assert.equal(state.video.srcObject, null);
    assertPhotoResourcesReleased(state);
});

test("un éxito de fotografía conserva exactamente el texto y espera Usar código", async () => {
    const raw = " 000123456789 / CODE128 ";
    const state = setup({ scanFile: async () => ({ decodedText: raw }) });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(state.dialog.open, true);
    assert.equal(state.result.hidden, false);
    assert.equal(state.resultCode.textContent, raw);
    assert.equal(state.useCode.disabled, false);
    assert.deepEqual(state.calls, []);
    assert.equal(state.manualField.value, "EXISTING-00042");
    await confirmCode(state);
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", raw]]);
    assert.equal(state.dialog.open, false);
    assert.equal(state.origin.focuses, 1);
    assertPhotoResourcesReleased(state);
});

test("Usar código sin resultado válido no entrega ningún valor", async () => {
    const state = setup();
    assert.equal(await openPhoto(state), "ready");
    state.useCode.dispatch("click", { preventDefault() {} });
    await settle();
    assert.deepEqual(state.calls, []);
    assert.equal(state.dialog.open, true);
});

test("doble confirmación de fotografía sólo entrega una vez", async () => {
    const state = setup();
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    state.useCode.dispatch("click", { preventDefault() {} });
    state.useCode.dispatch("click", { preventDefault() {} });
    await settle();
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "7501234567893"]]);
    assert.equal(state.dialog.open, false);
});

test("fallo de detección mantiene el diálogo, ofrece reintento y permite continuar manualmente", async () => {
    const state = setup({ scanFile: async () => { throw new Error("No MultiFormat Readers were able to detect the code"); } });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(state.dialog.open, true);
    assert.match(state.status.textContent, /No encontramos un código de barras en la foto/i);
    assert.equal(state.result.hidden, true);
    assert.equal(state.capture.disabled, false);
    assert.match(state.captureLabel.textContent, /Tomar otra foto/i);
    assert.deepEqual(state.calls, []);
    assertPhotoResourcesReleased(state);
    state.manual.dispatch("click", { preventDefault() {} });
    await settle();
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "manual", null]]);
    assert.equal(state.dialog.open, false);
    assert.equal(state.manualField.focuses, 1);
    assert.equal(state.manualField.value, "EXISTING-00042", "manual no borra el valor existente");
    assert.equal(state.origin.focuses, 0, "se prioriza el campo de código para escritura manual");
});

test("un decoder sin decodedText se trata como fallo recuperable", async () => {
    const state = setup({ scanFile: async () => ({ decodedText: "" }) });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(state.result.hidden, true);
    assert.equal(state.dialog.open, true);
    assert.equal(state.capture.disabled, false);
    assert.deepEqual(state.calls, []);
    assertPhotoResourcesReleased(state);
});

test("reintenta después de fallo sin recargar ni reabrir el diálogo", async () => {
    let attempt = 0;
    const state = setup({ scanFile: async () => {
        if (++attempt === 1) throw new Error("No barcode");
        return { decodedText: "12345670" };
    } });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(state.result.hidden, true);
    state.capture.dispatch("click", { preventDefault() {} });
    assert.equal(state.photoInput.clicks, 1);
    await selectPhoto(state);
    assert.equal(fileScans(state).length, 2);
    assert.equal(state.resultCode.textContent, "12345670");
    assert.equal(state.dialog.open, true);
    assert.deepEqual(state.calls, []);
    assertPhotoResourcesReleased(state);
});

test("seleccionar la misma imagen repetidamente limpia el value y no acumula recursos", async () => {
    const state = setup();
    const file = { name: "same.jpg", type: "image/jpeg", size: 80000 };
    assert.equal(await openPhoto(state), "ready");
    for (let index = 0; index < 4; index++) {
        await selectPhoto(state, file);
        assert.equal(state.photoInput.value, "");
        assertPhotoResourcesReleased(state);
        assert.equal(state.photoInput.listenerCount(), 1, "se conserva un único listener de selección");
        assert.equal(state.capture.listenerCount(), 1);
    }
    assert.equal(fileScans(state).length, 4);
    assert.ok(fileScans(state).every(scan => scan.file === file));
    assert.equal(state.createdUrls.length, state.revokedUrls.length);
    assert.deepEqual(state.calls, []);
});

test("Analizando código deshabilita acciones incompatibles y evita doble procesamiento", async () => {
    let complete;
    const state = setup({ scanFile: () => new Promise(resolve => { complete = resolve; }) });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.match(state.status.textContent, /Analizando código/i);
    assert.equal(state.capture.disabled, true);
    assert.equal(state.live.disabled, true);
    assert.equal(state.photoInput.disabled, true);
    assert.equal(state.useCode.disabled, true);
    state.capture.dispatch("click", { preventDefault() {} });
    await selectPhoto(state, { name: "duplicate.jpg", type: "image/jpeg", size: 5000 });
    assert.equal(state.photoInput.clicks, 0);
    assert.equal(fileScans(state).length, 1);
    complete({ decodedText: "7501234567893" });
    await settle();
    assert.equal(state.capture.disabled, false);
    assert.equal(state.live.disabled, false);
    assert.equal(state.photoInput.disabled, false);
    assert.equal(state.resultCode.textContent, "7501234567893");
    assertPhotoResourcesReleased(state);
});

test("una nueva fotografía borra el resultado anterior antes de comenzar el análisis", async () => {
    let complete;
    let attempt = 0;
    const state = setup({ scanFile: () => ++attempt === 1
        ? Promise.resolve({ decodedText: "00012345" })
        : new Promise(resolve => { complete = resolve; }) });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(state.resultCode.textContent, "00012345");
    await selectPhoto(state);
    assert.equal(state.result.hidden, true);
    assert.equal(state.resultCode.textContent, "");
    state.useCode.dispatch("click", { preventDefault() {} });
    await settle();
    assert.deepEqual(state.calls, []);
    complete({ decodedText: "12345670" });
    await settle();
    assert.equal(state.resultCode.textContent, "12345670");
    assertPhotoResourcesReleased(state);
});

test("cerrar durante el análisis revoca URLs, libera imágenes y descarta resultados tardíos", async () => {
    let complete;
    const state = setup({ scanFile: (_file, _preview, scanner, image) => {
        image.onload = () => {};
        image.onerror = () => {};
        scanner.photoCanvas = {};
        state.photoReader.children.push(scanner.photoCanvas);
        return new Promise(resolve => { complete = resolve; });
    } });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(state.objectUrls.size, 2);
    await scannerModule.cerrar(state.dialog);
    assert.equal(state.dialog.open, false);
    assertPhotoResourcesReleased(state);
    assert.equal(state.photoInput.listenerCount(), 0);
    assert.equal(state.capture.listenerCount(), 0);
    assert.equal(state.useCode.listenerCount(), 0);
    complete({ decodedText: "LATE-CODE" });
    await settle();
    assert.deepEqual(state.calls, []);
    assert.equal(state.result.hidden, true);
    assertPhotoResourcesReleased(state);
});

test("cambio foto → vivo sólo ocurre después de limpiar el lector de archivos", async () => {
    const state = setup();
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assertPhotoResourcesReleased(state);
    assert.equal(await scannerModule.escanearEnVivo(state.dialog), "started");
    await settle();
    assert.equal(state.photoPanel.hidden, true);
    assert.equal(state.livePanel.hidden, false);
    state.useCode.dispatch("click", { preventDefault() {} });
    await settle();
    assert.deepEqual(state.calls, [], "el resultado anterior deja de ser confirmable al entrar en vivo");
    assert.equal(state.instances.filter(scanner => scanner.starts > 0).length, 1);
    assert.equal(state.video.srcObject !== null, true);
    assertPhotoResourcesReleased(state);
});

test("cambio vivo → foto detiene tracks antes de permitir scanFileV2", async () => {
    const state = setup();
    assert.equal(await open(state), "started");
    const liveScanner = state.instances[0];
    await scannerModule.volverAFoto(state.dialog);
    assert.equal(liveScanner.stops, 1);
    assert.equal(state.tracks[0].stops, 1);
    assert.equal(state.video.srcObject, null);
    assert.equal(state.photoPanel.hidden, false);
    assert.equal(state.livePanel.hidden, true);
    assert.equal(state.zoom.hidden, true);
    await selectPhoto(state);
    assert.equal(fileScans(state).length, 1);
    assert.equal(state.resultCode.textContent, "7501234567893");
    assert.deepEqual(state.calls, []);
    assertPhotoResourcesReleased(state);
});

test("no permite activar cámara mientras una fotografía está analizándose", async () => {
    let complete;
    const state = setup({ scanFile: () => new Promise(resolve => { complete = resolve; }) });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    await scannerModule.escanearEnVivo(state.dialog);
    await settle();
    assert.equal(state.instances.some(scanner => scanner.starts > 0), false);
    assert.equal(state.video.srcObject, null);
    assert.equal(fileScans(state).length, 1);
    complete({ decodedText: "12345670" });
    await settle();
    assertPhotoResourcesReleased(state);
});

test("callbacks del modo vivo anterior no alteran el nuevo modo foto", async () => {
    const state = setup();
    assert.equal(await open(state), "started");
    const oldCamera = state.instances[0];
    await scannerModule.volverAFoto(state.dialog);
    oldCamera.success("OLD-LIVE-CODE");
    await settle();
    assert.equal(state.result.hidden, true);
    assert.deepEqual(state.calls, []);
    await selectPhoto(state);
    assert.equal(state.resultCode.textContent, "7501234567893");
    assertPhotoResourcesReleased(state);
});

test("retornar del selector nativo iOS no cancela fotografía por visibilitychange", async () => {
    const state = setup();
    assert.equal(await openPhoto(state), "ready");
    state.page.hidden = true;
    state.page.dispatch("visibilitychange");
    await settle();
    assert.equal(state.dialog.open, true);
    assert.deepEqual(state.calls, []);
    state.page.hidden = false;
    state.page.dispatch("visibilitychange");
    await selectPhoto(state);
    assert.equal(state.result.hidden, false);
    assertPhotoResourcesReleased(state);
});

for (const event of ["cancel", "pagehide", "close"]) {
    test("el modo fotografía libera recursos y listeners ante " + event, async () => {
        const state = setup();
        assert.equal(await openPhoto(state), "ready");
        await selectPhoto(state);
        if (event === "cancel") state.dialog.dispatch("cancel", { preventDefault() {} });
        else if (event === "close") state.dialog.close();
        else state.browser.dispatch("pagehide");
        await settle();
        assert.equal(state.dialog.open, false);
        assert.equal(state.dialog.listenerCount(), 0);
        assert.equal(state.photoInput.listenerCount(), 0);
        assert.equal(state.capture.listenerCount(), 0);
        assert.equal(state.useCode.listenerCount(), 0);
        assert.equal(state.manual.listenerCount(), 0);
        assert.equal(state.live.listenerCount(), 0);
        assert.equal(state.page.listenerCount(), 0);
        assert.equal(state.browser.listenerCount(), 0);
        assertPhotoResourcesReleased(state);
        assert.ok(state.calls.every(call => call[1] !== "detected"), "cerrar nunca confirma un resultado pendiente");
    });
}

test("no usa fetch ni solicitudes HTTP para analizar sucesivos archivos", async () => {
    const state = setup();
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    await selectPhoto(state, { name: "barcode.png", type: "image/png", size: 90000 });
    assert.equal(fileScans(state).length, 2);
    assert.equal(state.httpCalls.length, 0);
    assert.ok(state.createdUrls.every(entry => entry.url.startsWith("blob:")));
    assertPhotoResourcesReleased(state);
});

test("la alternativa fotografía funciona sin API de cámara ni contexto seguro", async () => {
    const state = setup();
    state.browser.isSecureContext = false;
    navigator.mediaDevices.getUserMedia = undefined;
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(state.result.hidden, false);
    assert.equal(state.video.srcObject, null);
    assert.deepEqual(state.calls, []);
    assertPhotoResourcesReleased(state);
});

test("el adaptador restaura las APIs globales inmediatamente aunque el decoder siga pendiente", async () => {
    let complete;
    const state = setup({ scanFile: () => new Promise(resolve => { complete = resolve; }) });
    const originalCreate = URL.createObjectURL;
    const originalImage = window.Image;
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(URL.createObjectURL, originalCreate, "no se deja createObjectURL interceptado durante awaits");
    assert.equal(window.Image, originalImage, "no se deja Image interceptado durante awaits");
    assert.equal(state.objectUrls.size, 2);
    complete({ decodedText: "00012345" });
    await settle();
    assertPhotoResourcesReleased(state);
});

test("un throw síncrono de scanFileV2 libera ambas URLs y permite reintentar", async () => {
    let attempt = 0;
    const state = setup({ scanFile: () => {
        if (++attempt === 1) throw new Error("Synchronous decoder failure");
        return Promise.resolve({ decodedText: "00012345" });
    } });
    const originalCreate = URL.createObjectURL;
    const originalImage = window.Image;
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(state.dialog.open, true);
    assert.equal(state.result.hidden, true);
    assert.equal(URL.createObjectURL, originalCreate);
    assert.equal(window.Image, originalImage);
    assertPhotoResourcesReleased(state);
    await selectPhoto(state);
    assert.equal(state.resultCode.textContent, "00012345");
    assertPhotoResourcesReleased(state);
});

test("el adaptador no revoca URLs ajenas a la fotografía analizada", async () => {
    let unrelated;
    const state = setup({ scanFile: () => {
        unrelated = URL.createObjectURL({ name: "other-app-resource" });
        return Promise.resolve({ decodedText: "00012345" });
    } });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.deepEqual([...state.objectUrls], [unrelated]);
    assert.equal(state.revokedUrls.includes(unrelated), false);
    URL.revokeObjectURL(unrelated);
    assertPhotoResourcesReleased(state);
});

test("cerrar antes de cargar la librería evita crear un lector de fotografía", async () => {
    const state = setup();
    assert.equal(await openPhoto(state), "ready");
    state.photoInput.files = [{ name: "barcode.jpg", type: "image/jpeg", size: 80000 }];
    state.photoInput.dispatch("change", { target: state.photoInput });
    await scannerModule.cerrar(state.dialog);
    await settle();
    assert.equal(state.instances.length, 0);
    assert.equal(state.dialog.open, false);
    assert.equal(state.photoInput.listenerCount(), 0);
    assert.deepEqual(state.calls, []);
    assertPhotoResourcesReleased(state);
});

test("desmontar el componente durante análisis libera recursos sin publicar código", async () => {
    let complete;
    const state = setup({ scanFile: () => new Promise(resolve => { complete = resolve; }) });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    state.dialog.isConnected = false;
    state.observers[0].notify();
    await settle();
    assert.equal(state.photoInput.listenerCount(), 0);
    assert.equal(state.capture.listenerCount(), 0);
    assert.equal(state.page.listenerCount(), 0);
    assert.equal(state.browser.listenerCount(), 0);
    assertPhotoResourcesReleased(state);
    complete({ decodedText: "LATE-CODE" });
    await settle();
    assert.deepEqual(state.calls, []);
    assertPhotoResourcesReleased(state);
});

test("manual durante el análisis cancela la foto sin esperar un decoder pendiente", async () => {
    let complete;
    const state = setup({ scanFile: () => new Promise(resolve => { complete = resolve; }) });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    state.manual.dispatch("click", { preventDefault() {} });
    await settle();
    assert.equal(state.dialog.open, false);
    assert.equal(state.manualField.value, "EXISTING-00042");
    assert.equal(state.manualField.focuses, 1);
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "manual", null]]);
    assertPhotoResourcesReleased(state);
    complete({ decodedText: "LATE-CODE" });
    await settle();
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "manual", null]]);
});

test("un resultado exitoso puede descartarse manualmente sin ejecutar OnDetected", async () => {
    const state = setup();
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(state.result.hidden, false);
    state.manual.dispatch("click", { preventDefault() {} });
    await settle();
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "manual", null]]);
    assert.equal(state.manualField.value, "EXISTING-00042");
    assertPhotoResourcesReleased(state);
});

test("Escape durante análisis cancela imágenes pendientes y no confirma el resultado", async () => {
    let complete;
    const state = setup({ scanFile: () => new Promise(resolve => { complete = resolve; }) });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    state.dialog.dispatch("cancel", { preventDefault() {} });
    await settle();
    assert.equal(state.dialog.open, false);
    assert.deepEqual(state.calls, [["CancelarDesdeTeclado"]]);
    assertPhotoResourcesReleased(state);
    complete({ decodedText: "LATE-CODE" });
    await settle();
    assert.deepEqual(state.calls, [["CancelarDesdeTeclado"]]);
});

for (const target of ["createObjectURL", "Image"]) {
    test("una API " + target + " de sólo lectura falla limpiamente y permite reintentar", async () => {
        const state = setup();
        const owner = target === "Image" ? window : URL;
        const original = owner[target];
        Object.defineProperty(owner, target, { configurable: true, writable: false, value: original });
        assert.equal(await openPhoto(state), "ready");
        await selectPhoto(state);
        assert.equal(state.dialog.open, true);
        assert.equal(state.result.hidden, true);
        assert.equal(state.capture.disabled, false);
        assert.equal(owner[target], original);
        assert.equal(fileScans(state).length, 0);
        assertPhotoResourcesReleased(state);
        Object.defineProperty(owner, target, { configurable: true, writable: true, value: original });
        await selectPhoto(state);
        assert.equal(state.resultCode.textContent, "7501234567893");
        assertPhotoResourcesReleased(state);
    });
}

test("Image no disponible permite reintento y salida manual sin pedir cámara", async () => {
    const state = setup();
    const originalImage = window.Image;
    window.Image = undefined;
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(state.dialog.open, true);
    assert.equal(state.result.hidden, true);
    assert.equal(state.capture.disabled, false);
    assert.equal(state.video.srcObject, null);
    assert.equal(fileScans(state).length, 0);
    assertPhotoResourcesReleased(state);
    window.Image = originalImage;
    await selectPhoto(state);
    assert.equal(state.result.hidden, false);
    assertPhotoResourcesReleased(state);
});

test("sin revokeObjectURL no crea recursos de imagen que luego no puedan liberarse", async () => {
    const state = setup();
    const revoke = URL.revokeObjectURL;
    URL.revokeObjectURL = undefined;
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.equal(state.dialog.open, true);
    assert.equal(state.result.hidden, true);
    assert.equal(state.capture.disabled, false);
    assert.equal(state.createdUrls.length, 0, "se verifica cleanup disponible antes de crear URLs");
    assert.equal(fileScans(state).length, 0);
    URL.revokeObjectURL = revoke;
    assertPhotoResourcesReleased(state);
    await selectPhoto(state);
    assert.equal(state.result.hidden, false);
    assertPhotoResourcesReleased(state);
});

test("un throw del onload del decoder se convierte en fallo recuperable y libera la imagen", async () => {
    let attempt = 0;
    const state = setup({ scanFile: (_file, _preview, _scanner, image) => {
        if (++attempt === 1) {
            image.onload = () => { throw new Error("Canvas decoder failed during load"); };
            return new Promise(() => {});
        }
        return Promise.resolve({ decodedText: "00012345" });
    } });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    assert.match(state.status.textContent, /Analizando código/i);
    assert.doesNotThrow(() => state.images[0].onload({ target: state.images[0] }));
    await settle();
    assert.equal(state.dialog.open, true);
    assert.equal(state.result.hidden, true);
    assert.equal(state.capture.disabled, false);
    assert.equal(state.photoInput.disabled, false);
    assert.match(state.status.textContent, /No encontramos un código de barras en la foto/i);
    assert.deepEqual(state.calls, []);
    assertPhotoResourcesReleased(state);
    await selectPhoto(state);
    assert.equal(state.resultCode.textContent, "00012345");
    assertPhotoResourcesReleased(state);
});

test("un fallo de carga de la imagen permite reintentar sin listeners ni URLs previas", async () => {
    let attempt = 0;
    const state = setup({ scanFile: (_file, _preview, _scanner, image) => {
        if (++attempt === 1) return new Promise((_resolve, reject) => {
            image.onerror = () => reject(new Error("Image failed to load"));
        });
        return Promise.resolve({ decodedText: "12345670" });
    } });
    assert.equal(await openPhoto(state), "ready");
    await selectPhoto(state);
    state.images[0].onerror({ target: state.images[0] });
    await settle();
    assert.equal(state.dialog.open, true);
    assert.equal(state.result.hidden, true);
    assert.equal(state.capture.disabled, false);
    assertPhotoResourcesReleased(state);
    await selectPhoto(state);
    assert.equal(state.resultCode.textContent, "12345670");
    assertPhotoResourcesReleased(state);
});
