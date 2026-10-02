import test, { afterEach } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../src/ResellManager.Web/wwwroot/barcode-scanner.js", import.meta.url), "utf8");
const scannerModule = await import("data:text/javascript;base64," + Buffer.from(source).toString("base64"));
const nativeSetTimeout = globalThis.setTimeout;
let currentState;

function eventTarget(properties = {}) {
    const listeners = new Map();
    const attributes = new Map();
    return Object.assign(properties, {
        setAttribute(name, value) { attributes.set(name, String(value)); },
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
    const elements = new Map([
        ["[data-barcode-zoom]", zoom],
        ["[data-barcode-zoom-input]", input],
        ["[data-barcode-zoom-value]", output],
        ["[data-barcode-zoom-message]", message],
        [".barcode-camera-feed", feed]
    ]);
    const dialog = eventTarget({
        open: false,
        isConnected: true,
        showModal() { this.open = true; },
        close() { this.open = false; this.dispatch("close"); },
        querySelectorAll: () => [video],
        querySelector: selector => elements.get(selector) ?? null
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
        clear() { this.clears++; }
    }
    const browser = eventTarget({
        isSecureContext: true,
        Html5Qrcode: FakeScanner,
        Html5QrcodeSupportedFormats: {
            EAN_13: 9, EAN_8: 10, UPC_A: 14, UPC_E: 15, CODE_128: 5
        }
    });
    const origin = { isConnected: true, focuses: 0, focus() { this.focuses++; } };
    const page = eventTarget({ body: {}, activeElement: origin, hidden: false });
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
    currentState = { dialog, reference, calls, callbackErrors, instances, tracks, observers, resizeObservers, frame, feed, video, canvas, context, transforms, zoom, input, output, message, browser, page, origin };
    return currentState;
}

async function settle() {
    // Use the real timer even while testing the scanner's 45-second timeout.
    await new Promise(resolve => nativeSetTimeout(resolve, 0));
    await new Promise(resolve => nativeSetTimeout(resolve, 0));
}

afterEach(async () => {
    if (currentState) scannerModule.cerrar(currentState.dialog);
    await settle();
    if (currentState) assert.deepEqual(currentState.callbackErrors, [], "las aserciones de callbacks no se ocultan por el fallback de Blazor");
    currentState = undefined;
});

async function open(state) {
    const result = await scannerModule.abrir(state.dialog, "visor", state.reference);
    await settle();
    return result;
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
    const opening = scannerModule.abrir(state.dialog, "visor", state.reference);
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
        assert.deepEqual(state.calls, [["FinalizarEscaneo", "timeout", null]]);
        assert.equal(cleared, true);
        assertReleased(state, true);
    } finally {
        globalThis.setTimeout = originalSetTimeout;
        globalThis.clearTimeout = originalClearTimeout;
    }
});

test("permiso denegado se comunica y permite reintentar sin scanner activo", async () => {
    const state = setup({ start: () => Promise.reject(Object.assign(new Error("Permission denied"), { name: "NotAllowedError" })) });
    assert.equal(await open(state), "denied");
    assertReleased(state);
    assert.equal(await open(state), "denied");
});

test("reporta cámara inexistente y navegador incompatible sin mostrar dialog", async () => {
    const state = setup({ start: () => Promise.reject(Object.assign(new Error("Requested device not found"), { name: "NotFoundError" })) });
    assert.equal(await open(state), "no-camera");
    assertReleased(state);
    window.isSecureContext = false;
    assert.equal(await open(state), "unsupported");
    assert.equal(state.instances.length, 1);
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
        assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "CODE128-00042"]]);
        assertReleased(state, true);
    });
}