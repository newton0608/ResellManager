import test, { afterEach } from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const source = await readFile(new URL("../src/ResellManager.Web/wwwroot/barcode-scanner.js", import.meta.url), "utf8");
const scanner = await import("data:text/javascript;base64," + Buffer.from(source).toString("base64"));
const names = ["window", "document", "navigator", "Image", "URL", "createImageBitmap", "MutationObserver",
    "ResizeObserver", "performance", "setTimeout", "clearTimeout", "fetch", "XMLHttpRequest"];
const originals = new Map(names.map(name => [name, Object.getOwnPropertyDescriptor(globalThis, name)]));
const realTimer = globalThis.setTimeout;
let current;
const readers = ["ean_8_reader", "ean_reader", "upc_reader", "upc_e_reader", "code_128_reader"];
const result = (code = "7501234567893", format = "ean_13", errors = [0.12]) => ({
    codeResult: { code, format, decodedCodes: errors.map(error => ({ error })) },
    line: [{ x: 0, y: 0 }, { x: 100, y: 0 }]
});

function eventTarget(properties = {}) {
    const listeners = new Map(), attributes = new Map();
    return Object.assign({
        hidden: false, disabled: false, value: "", textContent: "", style: {}, isConnected: true,
        setAttribute(name, value) { attributes.set(name, String(value)); },
        removeAttribute(name) { attributes.delete(name); },
        getAttribute(name) { return attributes.get(name) ?? null; },
        focus() { this.focuses = (this.focuses ?? 0) + 1; },
        addEventListener(name, callback) {
            if (!listeners.has(name)) listeners.set(name, new Set());
            listeners.get(name).add(callback);
        },
        removeEventListener(name, callback) { listeners.get(name)?.delete(callback); },
        dispatch(name, event = {}) { for (const callback of [...(listeners.get(name) ?? [])]) callback(event); },
        listenerCount() { return [...listeners.values()].reduce((total, set) => total + set.size, 0); }
    }, properties);
}

function container() {
    return eventTarget({
        children: [],
        appendChild(child) {
            child.parent = this;
            this.children.push(child);
            if (child.kind === "iframe") queueMicrotask(() => child.onload?.());
            return child;
        },
        querySelector(name) { return this.querySelectorAll(name)[0] ?? null; },
        querySelectorAll(name) { return this.children.filter(child => child.kind === name); }
    });
}

function jpeg(orientation = 1, little = true, width = 4032, height = 3024) {
    const array = new Uint8Array(59), data = new DataView(array.buffer);
    data.setUint16(0, 0xffd8);
    data.setUint16(2, 0xffe1); data.setUint16(4, 34);
    data.setUint32(6, 0x45786966);
    data.setUint16(12, little ? 0x4949 : 0x4d4d); data.setUint16(14, 42, little);
    data.setUint32(16, 8, little); data.setUint16(20, 1, little);
    data.setUint16(22, 0x0112, little); data.setUint16(24, 3, little);
    data.setUint32(26, 1, little); data.setUint16(30, orientation, little);
    data.setUint16(38, 0xffc0); data.setUint16(40, 17); data.setUint8(42, 8);
    data.setUint16(43, height); data.setUint16(45, width); data.setUint8(47, 3);
    data.setUint16(57, 0xffda);
    return new File([array], "barcode.jpg", { type: "image/jpeg" });
}

function setup(options = {}) {
    const state = {
        calls: [], engines: [], frames: [], scripts: [], canvases: [], bitmaps: [], images: [],
        createdUrls: [], revokedUrls: [], urls: new Set(), timers: new Map(), photoCalls: [],
        observers: [], resizeObservers: [], http: [], now: 0, bitmapCalls: [], callbacks: []
    };
    const controls = new Map(["zoom", "zoom-input", "zoom-value", "zoom-message", "file", "capture",
        "capture-label", "use", "manual", "live", "back-photo", "photo", "live-panel", "live-status",
        "photo-status", "result", "code"].map(name => [name, eventTarget()]));
    state.get = name => controls.get(name);
    state.get("file").files = [];
    state.get("file").click = () => { state.pickerClicks = (state.pickerClicks ?? 0) + 1; };
    let inputValue = "";
    Object.defineProperty(state.get("file"), "value", {
        get: () => inputValue,
        set(value) { inputValue = value; if (value === "") this.files = []; }
    });
    state.feed = container(); state.feed.clientWidth = 280;
    state.origin = eventTarget();
    state.manualField = eventTarget({ value: "EXISTING-00042" });
    state.dialog = eventTarget({
        open: false,
        showModal() { this.open = true; if (options.showError) throw new Error("Dialog failed"); },
        close() { this.open = false; this.dispatch("close"); },
        parentElement: { querySelector: () => state.manualField },
        contains: node => [...controls.values()].includes(node),
        querySelector: selector => selector === ".barcode-camera-feed" ? state.feed
            : controls.get(selector.match(/data-barcode-(.*)\]/)?.[1]) ?? null
    });
    state.reference = { async invokeMethodAsync(...args) { state.calls.push(args); } };
    const track = () => ({
        readyState: "live", stops: 0, settings: { ...(options.settings ?? {}) }, applied: [],
        stop() { this.stops++; this.readyState = "ended"; },
        getCapabilities() { if (options.capabilitiesError) throw new Error("Unavailable"); return options.capabilities ?? {}; },
        getSettings() { if (options.settingsError) throw new Error("Unavailable"); return this.settings; },
        async applyConstraints(constraints) {
            this.applied.push(constraints);
            await options.apply?.(constraints, this);
            if (constraints.advanced?.[0]?.zoom !== undefined) this.settings.zoom = options.appliedZoom ?? constraints.advanced[0].zoom;
        }
    });
    class FakeQuagga {
        constructor() {
            this.detected = new Set(); this.processed = new Set();
            this.starts = 0; this.stops = 0; this.track = track();
            this.video = eventTarget({ kind: "video", srcObject: null, videoWidth: options.videoWidth ?? 1920,
                videoHeight: options.videoHeight ?? 1080, pause() {} });
            this.CameraAccess = { getActiveTrack: () => this.track };
            state.engines.push(this);
        }
        init(config, callback) {
            this.config = config; this.target = config.inputStream.target;
            this.target.appendChild(this.video);
            this.video.srcObject = { getTracks: () => [this.track] };
            this.callback = callback;
            if (options.init) options.init(this, callback);
            else queueMicrotask(() => callback(options.initError));
        }
        start() { this.starts++; options.start?.(this); }
        async stop() {
            this.stops++;
            this.video.srcObject?.getTracks().forEach(value => { if (value.readyState !== "ended") value.stop(); });
            this.video.srcObject = null;
        }
        onDetected(callback) { this.detected.add(callback); state.callbacks.push(callback); }
        offDetected(callback) { callback ? this.detected.delete(callback) : this.detected.clear(); }
        onProcessed(callback) { this.processed.add(callback); }
        offProcessed(callback) { callback ? this.processed.delete(callback) : this.processed.clear(); }
        emit(value = result()) {
            for (const callback of [...this.processed]) callback(value);
            if (value?.codeResult) for (const callback of [...this.detected]) callback(value);
        }
        decodeSingle(config) {
            assert.equal(config.inputStream.size, 0, "Quagga no reduce nuestros candidatos a su default de 800px");
            assert.deepEqual(config.decoder.readers, readers);
            assert.equal(config.numOfWorkers, 0);
            assert.ok(state.engines.every(engine => engine.video.srcObject === null), "foto y cámara son excluyentes");
            state.photoCalls.push(config);
            const canvas = makeCanvas(); config.inputStream.target.appendChild(canvas);
            if (options.decode) return options.decode(config, state.photoCalls.length);
            return Promise.resolve(options.results ? options.results[state.photoCalls.length - 1] : result());
        }
    }
    function makeCanvas() {
        const canvas = eventTarget({ kind: "canvas", width: 0, height: 0, draws: [], transforms: [] });
        canvas.context = {
            setTransform(...values) { canvas.transforms.push(values); },
            drawImage(...values) { canvas.draws.push(values); }
        };
        canvas.getContext = () => options.noContext ? null : canvas.context;
        canvas.toDataURL = type => {
            assert.equal(type, "image/png");
            canvas.savedWidth = canvas.width; canvas.savedHeight = canvas.height;
            if (options.encodeError) throw new Error("Canvas encoding failed");
            return "data:image/png;base64," + Buffer.from(String(canvas.width) + "x" + canvas.height).toString("base64");
        };
        state.canvases.push(canvas);
        return canvas;
    }
    function createElement(kind) {
        if (kind === "canvas") return makeCanvas();
        if (kind === "iframe") {
            const engine = new FakeQuagga(), body = container(), head = container();
            head.appendChild = script => {
                state.scripts.push(script);
                queueMicrotask(() => options.libraryError ? script.onerror?.() : script.onload?.());
            };
            const frame = eventTarget({
                kind, contentWindow: { Quagga: options.noLibrary ? undefined : engine },
                contentDocument: { body, head, createElement },
                remove() {
                    this.removed = true;
                    if (this.parent) this.parent.children = this.parent.children.filter(child => child !== this);
                }
            });
            state.frames.push(frame);
            return frame;
        }
        return eventTarget({ kind });
    }
    state.page = eventTarget({ body: container(), baseURI: "https://example.test/subapp/",
        activeElement: state.origin, hidden: false, createElement });
    state.browser = eventTarget({ isSecureContext: options.secure !== false });
    Object.defineProperty(globalThis, "navigator", { configurable: true,
        value: { mediaDevices: options.noMedia ? undefined : { getUserMedia() {} } } });
    globalThis.document = state.page; globalThis.window = state.browser;
    globalThis.URL = class extends originals.get("URL").value {
        static createObjectURL(blob) {
            const url = "blob:local-" + state.createdUrls.length;
            state.createdUrls.push({ url, blob }); state.urls.add(url); return url;
        }
        static revokeObjectURL(url) { state.revokedUrls.push(url); state.urls.delete(url); }
    };
    globalThis.Image = class {
        constructor() { this.naturalWidth = 4032; this.naturalHeight = 3024; state.images.push(this); }
        set src(value) {
            this.source = value;
            if (!options.pendingImage) queueMicrotask(() => options.imageError ? this.onerror?.() : this.onload?.());
        }
        removeAttribute() { this.source = ""; }
    };
    globalThis.createImageBitmap = options.noBitmap ? undefined : async (blob, config) => {
        state.bitmapCalls.push({ blob, config });
        if (options.bitmapError) throw new Error("Bitmap unsupported");
        const bitmap = { width: config.resizeWidth ?? 4032, height: config.resizeHeight ?? 3024,
            closes: 0, close() { this.closes++; } };
        state.bitmaps.push(bitmap);
        return options.bitmap ? options.bitmap(bitmap) : bitmap;
    };
    globalThis.MutationObserver = class {
        constructor(callback) { this.callback = callback; state.observers.push(this); }
        observe() {}
        disconnect() { this.disconnected = true; }
    };
    globalThis.ResizeObserver = options.noResize ? undefined : class {
        constructor(callback) { this.callback = callback; state.resizeObservers.push(this); }
        observe() {}
        disconnect() { this.disconnected = true; }
    };
    globalThis.performance = { now: () => state.now };
    globalThis.setTimeout = (callback, delay) => {
        if (delay < 1000) return realTimer(callback, delay);
        const id = {}; state.timers.set(id, { callback, delay }); return id;
    };
    globalThis.clearTimeout = id => {
        if (!state.timers.delete(id)) originals.get("clearTimeout").value(id);
    };
    globalThis.fetch = (...args) => { state.http.push(args); throw new Error("No photo HTTP"); };
    globalThis.XMLHttpRequest = class { constructor() { state.http.push("XHR"); throw new Error("No photo XHR"); } };
    current = state;
    return state;
}

async function settle() {
    for (let i = 0; i < 7; i++) await new Promise(resolve => realTimer(resolve, 0));
}

async function open(state) {
    assert.equal(await scanner.abrir(state.dialog, "visor", state.reference), "ready");
}

async function live(state) {
    await open(state);
    const outcome = await scanner.escanearEnVivo(state.dialog);
    await settle();
    return outcome;
}

async function photo(state, file = jpeg()) {
    state.get("file").value = "C:\\fakepath\\barcode.jpg";
    state.get("file").files = [file];
    state.get("file").dispatch("change");
    await settle();
    return file;
}

function stateName(state) { return state.get("photo").getAttribute("data-state"); }

function assertPhotoClean(state) {
    assert.equal(state.urls.size, 0);
    assert.equal(state.createdUrls.length, state.revokedUrls.length);
    assert.ok(state.bitmaps.every(bitmap => bitmap.closes === 1));
    assert.ok(state.canvases.every(canvas => canvas.width === 0 && canvas.height === 0));
    assert.ok(state.images.every(image => !image.source && !image.onload && !image.onerror));
    assert.ok(state.frames.every(frame => frame.removed && !frame.onload && !frame.onerror));
    assert.ok(state.scripts.every(script => !script.onload && !script.onerror));
    assert.equal(state.timers.size, 0);
    assert.equal(state.get("file").value, "");
}

function assertLiveClean(state) {
    assert.ok(state.engines.every(engine => engine.track.readyState === "ended"));
    assert.ok(state.engines.every(engine => engine.detected.size === 0 && engine.processed.size === 0));
    assert.ok(state.frames.every(frame => frame.removed));
    assert.ok(state.resizeObservers.every(observer => observer.disconnected));
    assert.equal(state.get("zoom-input").listenerCount(), 0);
    assert.equal(state.timers.size, 0);
}

function assertReleased(state) {
    assert.equal(state.dialog.listenerCount(), 0);
    assert.equal(state.page.listenerCount(), 0);
    assert.equal(state.browser.listenerCount(), 0);
    assert.ok(state.observers.every(observer => observer.disconnected));
    for (const name of ["capture", "file", "use", "manual", "live", "back-photo"]) assert.equal(state.get(name).listenerCount(), 0);
}

afterEach(async () => {
    if (current) {
        await scanner.cerrar(current.dialog);
        await settle();
        assertReleased(current);
    }
    for (const [name, descriptor] of originals) {
        if (descriptor) Object.defineProperty(globalThis, name, descriptor);
        else delete globalThis[name];
    }
    current = undefined;
});

test("abrir conserva fotografía como modo principal sin cámara ni decoder", async () => {
    const state = setup();
    await open(state);
    assert.equal(stateName(state), "idle");
    assert.equal(state.get("live-panel").hidden, true);
    assert.equal(state.engines.length, 0);
    assert.equal(state.get("use").disabled, true);
});

test("Tomar foto conserva el gesto síncrono y vacía el input", async () => {
    const state = setup(); await open(state);
    state.get("file").value = "previous";
    state.get("capture").dispatch("click");
    assert.equal(state.pickerClicks, 1); assert.equal(state.get("file").value, "");
});

test("cancelar el picker no analiza ni cierra el diálogo", async () => {
    const state = setup(); await open(state);
    state.get("file").dispatch("change"); await settle();
    assert.equal(state.frames.length, 0); assert.equal(state.dialog.open, true);
});

test("carga Quagga desde el bundle local relativo al baseURI", async () => {
    const state = setup(); await open(state); await photo(state);
    assert.equal(state.scripts[0].src, "https://example.test/subapp/vendor/quagga2/quagga.min.js");
    assertPhotoClean(state);
});

for (const option of ["noLibrary", "libraryError"]) {
    test("biblioteca " + option + " presenta error técnico recuperable", async () => {
        const state = setup({ [option]: true }); await open(state); await photo(state);
        assert.equal(stateName(state), "technical-error");
        assert.equal(state.get("photo-status").textContent, "No pudimos analizar la foto. Inténtalo de nuevo.");
        assert.equal(state.get("capture").disabled, false); assertPhotoClean(state);
    });
}

test("foto detectada conserva el resultado pendiente hasta Usar código", async () => {
    const state = setup(); await open(state); await photo(state);
    assert.equal(stateName(state), "success"); assert.equal(state.get("code").textContent, "7501234567893");
    assert.equal(state.dialog.open, true); assert.deepEqual(state.calls, []);
    state.get("use").dispatch("click"); state.get("use").dispatch("click"); await settle();
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "7501234567893"]]);
    assert.equal(state.dialog.open, false); assertPhotoClean(state);
});

test("foto sin código termina tres intentos y muestra not found", async () => {
    const state = setup({ results: [null, {}, { boxes: [] }] }); await open(state); await photo(state);
    assert.equal(state.photoCalls.length, 3); assert.equal(stateName(state), "not-found");
    assert.equal(state.get("photo-status").textContent, "No encontramos un código de barras en la foto.");
    assert.deepEqual(state.calls, []); assertPhotoClean(state);
});

for (const candidate of [2, 3]) {
    test("multi candidato se detiene en el intento " + candidate + " exitoso", async () => {
        const values = Array(candidate - 1).fill(null).concat(result());
        const state = setup({ results: values }); await open(state); await photo(state);
        assert.equal(state.photoCalls.length, candidate); assert.equal(stateName(state), "success");
        const encoded = state.canvases.filter(canvas => canvas.savedWidth);
        assert.ok(encoded.every(canvas => Math.max(canvas.savedWidth, canvas.savedHeight) <= 1920));
        assert.ok(encoded[1].savedWidth > encoded[0].savedWidth, "el recorte dispone de más detalle útil");
        assertPhotoClean(state);
    });
}

for (const [name, options] of [
    ["decoder rechaza", { decode() { return Promise.reject(new Error("Decoder failed")); } }],
    ["decoder lanza", { decode() { throw new Error("Decoder failed"); } }],
    ["canvas sin contexto", { noContext: true }],
    ["canvas no codifica", { encodeError: true }],
    ["imagen inválida", { bitmapError: true, imageError: true }]
]) {
    test(name + " se diferencia de foto sin código y libera recursos", async () => {
        const state = setup(options); await open(state); await photo(state);
        assert.equal(stateName(state), "technical-error");
        assert.equal(state.get("photo-status").textContent, "No pudimos analizar la foto. Inténtalo de nuevo.");
        assertPhotoClean(state);
    });
}

test("fallback Image revoca ObjectURL, handlers y src", async () => {
    const state = setup({ noBitmap: true }); await open(state); await photo(state);
    assert.equal(stateName(state), "success"); assert.equal(state.createdUrls.length, 1);
    assertPhotoClean(state);
});

test("createImageBitmap incompatible usa Image sin mutar APIs globales", async () => {
    const state = setup({ bitmapError: true }); const imageApi = Image, urlApi = URL.createObjectURL;
    await open(state); await photo(state);
    assert.equal(Image, imageApi); assert.equal(URL.createObjectURL, urlApi);
    assert.equal(stateName(state), "success"); assertPhotoClean(state);
});

test("48MP se reducen antes de Quagga y no se amplían fotos pequeñas", async () => {
    const state = setup(); await open(state); await photo(state, jpeg(1, true, 8000, 6000));
    assert.equal(state.bitmapCalls[0].config.resizeWidth, 2560);
    assert.equal(state.bitmapCalls[0].config.resizeHeight, 1920);
    assert.equal(state.bitmapCalls[0].config.imageOrientation, "from-image");
    await photo(state, jpeg(1, true, 640, 480));
    assert.equal(state.bitmapCalls[1].config.resizeWidth, 640);
    assert.equal(state.bitmapCalls[1].config.resizeHeight, 480);
    assertPhotoClean(state);
});

for (const orientation of [1, 2, 3, 4, 5, 6, 7, 8]) {
    test("EXIF " + orientation + " se neutraliza y aplica exactamente una vez", async () => {
        const state = setup(); await open(state); await photo(state, jpeg(orientation, orientation % 2 === 1));
        const config = state.bitmapCalls[0];
        const neutral = new DataView(await config.blob.arrayBuffer());
        assert.equal(neutral.getUint16(30, orientation % 2 === 1), 1);
        const master = state.canvases.find(canvas => canvas.transforms.length);
        const transform = master.transforms[0];
        const expected = [
            [1, 0, 0, 1, 0, 0], [-1, 0, 0, 1, 2560, 0], [-1, 0, 0, -1, 2560, 1920],
            [1, 0, 0, -1, 0, 1920], [0, 1, 1, 0, 0, 0], [0, 1, -1, 0, 1920, 0],
            [0, -1, -1, 0, 1920, 2560], [0, -1, 1, 0, 0, 2560]
        ][orientation - 1];
        assert.deepEqual(transform, expected); assertPhotoClean(state);
    });
}

test("EXIF portrait tiene el mismo tratamiento en fallback Image", async () => {
    const state = setup({ noBitmap: true }); await open(state); await photo(state, jpeg(6));
    const data = new DataView(await state.createdUrls[0].blob.arrayBuffer());
    assert.equal(data.getUint16(30, true), 1);
    assert.deepEqual(state.canvases.find(canvas => canvas.transforms.length).transforms[0], [0, 1, -1, 0, 1920, 0]);
    assertPhotoClean(state);
});

test("cabecera JPEG truncada no causa lectura fuera de límites", async () => {
    const state = setup(); await open(state);
    await photo(state, new File([new Uint8Array([255, 216, 255, 225, 255, 255])], "truncated.jpg", { type: "image/jpeg" }));
    assert.equal(stateName(state), "success"); assertPhotoClean(state);
});

test("se puede seleccionar exactamente la misma fotografía varias veces", async () => {
    const state = setup(); await open(state); const file = jpeg();
    await photo(state, file); await photo(state, file); await photo(state, file);
    assert.equal(state.photoCalls.length, 3); assert.equal(state.get("capture-label").textContent, "Tomar otra foto");
    assertPhotoClean(state);
});

test("analizando deshabilita acciones incompatibles y bloquea vivo y segunda foto", async () => {
    let resolve;
    const state = setup({ decode: () => new Promise(done => { resolve = done; }) });
    await open(state); await photo(state);
    assert.equal(stateName(state), "analyzing");
    for (const name of ["capture", "file", "live", "use"]) assert.equal(state.get(name).disabled, true);
    assert.equal(await scanner.escanearEnVivo(state.dialog), "busy");
    await photo(state); assert.equal(state.photoCalls.length, 1);
    resolve(result()); await settle(); assert.equal(stateName(state), "success"); assertPhotoClean(state);
});

test("una nueva foto borra el resultado anterior antes de analizar", async () => {
    let done;
    const state = setup({ decode: (_, count) => count === 1 ? Promise.resolve(result()) : new Promise(resolve => { done = resolve; }) });
    await open(state); await photo(state); await photo(state);
    assert.equal(state.get("code").textContent, ""); assert.equal(state.get("result").hidden, true);
    done(null); await settle();
});

test("timeout fotográfico es técnico y libera un decodeSingle pendiente", async () => {
    const state = setup({ decode: () => new Promise(() => {}) }); await open(state); await photo(state);
    [...state.timers.values()].find(timer => timer.delay === 30000).callback(); await settle();
    assert.equal(stateName(state), "technical-error"); assertPhotoClean(state);
});

test("cerrar cancela Image pendiente y libera inmediatamente su ObjectURL", async () => {
    const state = setup({ noBitmap: true, pendingImage: true }); await open(state); await photo(state);
    await scanner.cerrar(state.dialog); assertPhotoClean(state); assert.deepEqual(state.calls, []);
});

test("bitmap que termina después de cancelar se cierra y no publica código", async () => {
    let done;
    const state = setup({ bitmap: bitmap => new Promise(resolve => { done = () => resolve(bitmap); }) });
    await open(state); await photo(state); await scanner.cerrar(state.dialog);
    done(); await settle(); assertPhotoClean(state); assert.deepEqual(state.calls, []);
});

for (const action of ["cancel", "manual", "unmount", "pagehide"]) {
    test(action + " durante decode pendiente elimina el contexto y descarta resultados tardíos", async () => {
        let done;
        const state = setup({ decode: () => new Promise(resolve => { done = resolve; }) });
        await open(state); await photo(state);
        if (action === "cancel") await scanner.cerrar(state.dialog);
        if (action === "manual") state.get("manual").dispatch("click");
        if (action === "unmount") { state.dialog.isConnected = false; state.observers[0].callback(); }
        if (action === "pagehide") state.browser.dispatch("pagehide");
        await settle(); assertPhotoClean(state); assertReleased(state);
        done(result()); await settle();
        assert.ok(state.calls.every(call => call[1] !== "detected"));
    });
}

test("no hay fetch, XHR ni bytes de foto enviados a Blazor", async () => {
    const state = setup(); await open(state); const file = await photo(state);
    state.get("use").dispatch("click"); await settle();
    assert.deepEqual(state.http, []);
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "7501234567893"]]);
    assert.ok(!state.calls.flat().includes(file));
});

test("fotografía funciona sin mediaDevices ni contexto HTTPS", async () => {
    const state = setup({ noMedia: true, secure: false }); await open(state); await photo(state);
    assert.equal(stateName(state), "success"); assertPhotoClean(state);
});

test("visibilitychange del picker iOS no cierra el modo foto", async () => {
    const state = setup(); await open(state);
    state.page.hidden = true; state.page.dispatch("visibilitychange"); await settle();
    assert.equal(state.dialog.open, true); assert.deepEqual(state.calls, []);
});

test("LiveStream usa cámara trasera, constraints flexibles, lectores 1D y región horizontal", async () => {
    const state = setup(); assert.equal(await live(state), "started");
    const config = state.engines[0].config;
    assert.equal(config.inputStream.type, "LiveStream");
    assert.deepEqual(config.inputStream.constraints, {
        facingMode: { ideal: "environment" }, width: { ideal: 1920 }, height: { ideal: 1080 }
    });
    assert.equal(config.inputStream.size, 1280); assert.equal(config.frequency, 10);
    assert.equal(config.locate, true); assert.equal(config.locator.halfSample, false);
    assert.deepEqual(config.decoder.readers, readers); assert.equal(config.decoder.multiple, false);
    assert.deepEqual(config.inputStream.area, { top: "36%", right: "4%", left: "4%", bottom: "36%" });
    assert.equal(state.get("photo").hidden, true); assert.equal(state.get("live-panel").hidden, false);
    assert.equal(state.frames[0].getAttribute("allow"), "camera");
});

test("una lectura aislada no confirma ni detiene la cámara", async () => {
    const state = setup(); await live(state); state.engines[0].emit();
    state.get("use").dispatch("click"); await settle();
    assert.deepEqual(state.calls, []); assert.equal(state.engines[0].track.stops, 0);
    assert.equal(state.get("result").hidden, true);
});

test("dos lecturas iguales consecutivas confirman y liberan antes de Usar código", async () => {
    const state = setup(); await live(state);
    state.engines[0].emit(); state.now = 100; state.engines[0].emit(); await settle();
    assert.equal(stateName(state), "success"); assert.equal(state.get("code").textContent, "7501234567893");
    assert.deepEqual(state.calls, []); assertLiveClean(state);
    state.get("use").dispatch("click"); await settle();
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", "7501234567893"]]);
});

test("lecturas distintas reinician la estabilización", async () => {
    const state = setup(); await live(state); const engine = state.engines[0];
    engine.emit(result("7501234567893")); state.now = 100; engine.emit(result("5901234123457"));
    state.now = 200; engine.emit(result("7501234567893"));
    assert.equal(state.get("result").hidden, true);
    state.now = 300; engine.emit(result("7501234567893")); await settle();
    assert.equal(stateName(state), "success"); assertLiveClean(state);
});

test("lecturas iguales fuera de la ventana reinician la estabilización", async () => {
    const state = setup(); await live(state); const engine = state.engines[0];
    engine.emit(); state.now = 1501; engine.emit(); assert.equal(state.get("result").hidden, true);
    state.now = 1600; engine.emit(); await settle(); assert.equal(stateName(state), "success");
});

test("fotograma sin resultado no suma lecturas ni entrega código", async () => {
    const state = setup(); await live(state);
    state.engines[0].emit(null); state.engines[0].emit(); state.engines[0].emit({ boxes: [] });
    assert.equal(state.get("result").hidden, true); assert.deepEqual(state.calls, []);
});

for (const error of [NaN, Infinity, -0.1]) {
    test("error de patrón inválido " + error + " reinicia confirmación", async () => {
        const state = setup(); await live(state); const engine = state.engines[0];
        engine.emit(); engine.emit(result("7501234567893", "ean_13", [error])); engine.emit();
        assert.equal(state.get("result").hidden, true);
        engine.emit(); assert.equal(stateName(state), "success");
    });
}

test("no inventa umbral universal para errores de readers distintos", async () => {
    const state = setup(); await live(state);
    const value = result("FANAVF1461710", "code_128", [1.0000000000000002, 0.8888888888888893]);
    state.engines[0].emit(value); state.engines[0].emit(value);
    assert.equal(state.get("code").textContent, "FANAVF1461710");
});

for (const [format, code] of [
    ["ean_13", "05901234123457".slice(1)], ["ean_8", "96385074"],
    ["upc_a", "036000291452"], ["upc_e", "04252614"], ["code_128", " 00042-A "],
    ["ean_13", "0036000291452"]
]) {
    for (const mode of ["photo", "live"]) {
        test(mode + " entrega " + format + " exactamente sin normalizar: " + code, async () => {
            const state = setup({ results: [result(code, format)] });
            if (mode === "photo") { await open(state); await photo(state); }
            else {
                await live(state); state.engines[0].emit(result(code, format)); state.engines[0].emit(result(code, format));
            }
            await settle(); assert.equal(state.get("code").textContent, code);
            assert.deepEqual(state.calls, []);
            state.get("use").dispatch("click"); await settle();
            assert.deepEqual(state.calls, [["FinalizarEscaneo", "detected", code]]);
        });
    }
}

for (const value of [result("", "code_128"), result("123", "ean_13"), result("12345678", "qr_code")]) {
    test("resultado vacío, malformado o formato ajeno no confirma " + value.codeResult.format + ":" + value.codeResult.code, async () => {
        const state = setup({ results: [value, value, value] });
        await open(state); await photo(state); assert.equal(stateName(state), "not-found"); assertPhotoClean(state);
    });
}

for (const [name, reason] of [["NotAllowedError", "denied"], ["NotFoundError", "no-camera"], ["NotReadableError", "startup-error"]]) {
    test(name + " libera cámara y ofrece foto/manual y reintento", async () => {
        const state = setup({ initError: new DOMException("Camera unavailable", name) });
        assert.equal(await live(state), reason); assert.equal(state.get("photo").hidden, false);
        assert.equal(state.get("capture").disabled, false); assert.equal(state.get("manual").disabled, false);
        assertLiveClean(state);
    });
}

test("live sin API de cámara o HTTPS no crea decoder", async () => {
    const state = setup({ secure: false }); await open(state);
    assert.equal(await scanner.escanearEnVivo(state.dialog), "unsupported");
    assert.equal(state.frames.length, 0); assert.equal(state.get("photo").hidden, false);
});

test("fallo de biblioteca en vivo conserva alternativas y libera frame", async () => {
    const state = setup({ libraryError: true }); assert.equal(await live(state), "startup-error");
    assert.equal(state.get("capture").disabled, false); assert.equal(state.frames[0].removed, true);
    assert.equal(state.timers.size, 0);
});

test("timeout live de 45 segundos detiene decoder, tracks y callbacks", async () => {
    const state = setup(); await live(state); const callback = state.callbacks[0];
    [...state.timers.values()].find(timer => timer.delay === 45000).callback(); await settle();
    assertLiveClean(state); assert.equal(state.get("photo").hidden, false);
    assert.match(state.get("photo-status").textContent, /No se pudo detectar/);
    callback(result()); callback(result()); await settle(); assert.deepEqual(state.calls, []);
});

test("cancelar mientras se pide permiso no espera y limpia cualquier stream tardío", async () => {
    let complete;
    const state = setup({ init: (engine, callback) => { complete = () => {
        engine.video.srcObject = { getTracks: () => [engine.track] }; callback();
    }; } });
    await open(state); const starting = scanner.escanearEnVivo(state.dialog); await settle();
    await scanner.cerrar(state.dialog); assert.equal(await starting, "cancelled");
    complete(); await settle(); assertLiveClean(state); assert.deepEqual(state.calls, []);
    await open(state); assert.equal(state.dialog.open, true);
});

test("timeout también cubre inicialización de cámara pendiente", async () => {
    const state = setup({ init() {} }); await open(state);
    const starting = scanner.escanearEnVivo(state.dialog); await settle();
    [...state.timers.values()].find(timer => timer.delay === 45000).callback();
    assert.equal(await starting, "cancelled"); assertLiveClean(state);
    assert.match(state.get("photo-status").textContent, /No se pudo detectar/);
});

test("foto → live limpia foto y crea un decoder independiente", async () => {
    const state = setup(); await open(state); await photo(state); assertPhotoClean(state);
    await scanner.escanearEnVivo(state.dialog); await settle();
    assert.equal(state.engines.length, 2); assert.equal(state.frames[0].removed, true);
    assert.equal(state.get("code").textContent, ""); assert.equal(state.get("photo").hidden, true);
    await scanner.cerrar(state.dialog); assert.equal(state.engines[1].track.stops, 1);
});

test("live → foto detiene cámara antes de decodificar", async () => {
    const state = setup(); await live(state); const stale = state.callbacks[0];
    await scanner.volverAFoto(state.dialog); assertLiveClean(state);
    stale(result()); stale(result()); await settle(); assert.equal(stateName(state), "idle");
    await photo(state); assert.equal(stateName(state), "success"); assertPhotoClean(state);
});

test("Volver a fotografía permite cancelar un permiso pendiente", async () => {
    const state = setup({ init() {} }); await open(state);
    const starting = scanner.escanearEnVivo(state.dialog); await settle();
    await scanner.volverAFoto(state.dialog); assert.equal(await starting, "cancelled");
    assertLiveClean(state); assert.equal(stateName(state), "idle");
});

for (const action of ["cancel", "unmount", "manual", "pagehide", "visibility", "external-close"]) {
    test(action + " live libera cámara, timers, callbacks y listeners", async () => {
        const state = setup(); await live(state);
        if (action === "cancel") await scanner.cerrar(state.dialog);
        if (action === "unmount") { state.dialog.isConnected = false; state.observers[0].callback(); }
        if (action === "manual") state.get("manual").dispatch("click");
        if (action === "pagehide") state.browser.dispatch("pagehide");
        if (action === "visibility") { state.page.hidden = true; state.page.dispatch("visibilitychange"); }
        if (action === "external-close") state.dialog.close();
        await settle(); assertLiveClean(state); assertReleased(state);
        assert.ok(state.calls.every(call => call[1] !== "detected"));
    });
}

test("Escape cancela con Blazor y devuelve foco", async () => {
    const state = setup(); await live(state); let prevented = false;
    state.dialog.dispatch("cancel", { preventDefault() { prevented = true; } }); await settle();
    assert.equal(prevented, true); assert.equal(state.origin.focuses, 1);
    assert.deepEqual(state.calls, [["CancelarDesdeTeclado"]]); assertLiveClean(state);
});

test("manual conserva el valor del consumidor y devuelve foco a su input", async () => {
    const state = setup(); await open(state); state.get("manual").dispatch("click"); await settle();
    assert.equal(state.manualField.value, "EXISTING-00042"); assert.equal(state.manualField.focuses, 1);
    assert.deepEqual(state.calls, [["FinalizarEscaneo", "manual", null]]);
});

test("abrir y cerrar repetidamente no duplica listeners ni motores", async () => {
    const state = setup();
    for (let i = 0; i < 3; i++) {
        await live(state);
        assert.equal(state.get("live").listenerCount(), 1); assert.equal(state.dialog.listenerCount(), 2);
        await scanner.cerrar(state.dialog); assertReleased(state);
    }
    assertLiveClean(state); assert.equal(state.engines.length, 3);
});

test("scanner simultáneo no altera la sesión activa", async () => {
    const state = setup(); await open(state);
    assert.equal(await scanner.abrir({}, "otra", state.reference), "busy");
    assert.equal(state.dialog.open, true);
});

test("fallo de showModal limpia listeners y permite reapertura", async () => {
    const state = setup({ showError: true });
    assert.equal(await scanner.abrir(state.dialog, "visor", state.reference), "startup-error"); assertReleased(state);
});

test("zoom real usa min/max/step, conserva enfoque y resolución y no reinicia Quagga", async () => {
    const state = setup({ capabilities: { focusMode: ["continuous"], zoom: { min: 1, max: 4, step: 0.5 } } });
    await live(state); const engine = state.engines[0], track = engine.track;
    assert.equal(state.get("zoom").hidden, false); assert.equal(state.get("zoom-input").value, "2");
    state.get("zoom-input").value = "99"; state.get("zoom-input").dispatch("input"); await settle();
    assert.equal(state.get("zoom-input").value, "4"); assert.equal(engine.starts, 1);
    assert.ok(track.applied.every(value => value.width.ideal === 1920 && value.height.ideal === 1080));
    assert.ok(track.applied.slice(1).every(value => value.advanced[0].focusMode === "continuous"));
    await scanner.cerrar(state.dialog); assertLiveClean(state);
});

for (const [name, options] of [
    ["no tiene capacidades", {}],
    ["capabilities lanza", { capabilitiesError: true }],
    ["zoom inválido", { capabilities: { zoom: { min: 1, max: 3, step: 0 } } }],
    ["aplicar zoom falla", { capabilities: { zoom: { min: 1, max: 3, step: 1 } }, apply() { throw new Error("Zoom unavailable"); } }]
]) {
    test(name + " no bloquea el scanner ni muestra control engañoso", async () => {
        const state = setup(options); await live(state);
        assert.equal(state.get("zoom").hidden, true);
        state.engines[0].emit(); state.engines[0].emit(); assert.equal(stateName(state), "success");
        assertLiveClean(state);
    });
}

test("fallo opcional de enfoque permite seguir con zoom", async () => {
    const state = setup({
        capabilities: { focusMode: ["continuous"], zoom: { min: 1, max: 3, step: 0.25 } },
        apply(value) { if (value.advanced[0].focusMode) throw new Error("Focus unavailable"); }
    });
    await live(state); assert.equal(state.get("zoom").hidden, false);
    assert.equal(state.engines[0].track.applied.at(-1).advanced[0].focusMode, undefined);
});

test("zoom refleja settings reales y un fallo posterior revierte el slider", async () => {
    let fail = false;
    const state = setup({ capabilities: { zoom: { min: 1, max: 4, step: 0.5 } }, appliedZoom: 1.5,
        apply() { if (fail) throw new Error("Zoom failed"); } });
    await live(state); assert.equal(state.get("zoom-input").value, "1.5");
    fail = true; state.get("zoom-input").value = "3"; state.get("zoom-input").dispatch("input"); await settle();
    assert.equal(state.get("zoom-input").value, "1.5");
    assert.match(state.get("zoom-message").textContent, /No se pudo ajustar/);
});

test("zoom pendiente al cerrar no restaura UI ni deja listeners", async () => {
    let done;
    const state = setup({ capabilities: { zoom: { min: 1, max: 4, step: 0.5 } },
        apply: () => new Promise(resolve => { done = resolve; }) });
    await live(state); await scanner.cerrar(state.dialog); done(); await settle();
    assertLiveClean(state); assert.equal(state.get("zoom").hidden, true);
});

test("resize adapta altura natural sin reiniciar ni tocar canvas de Quagga", async () => {
    const state = setup({ videoWidth: 1080, videoHeight: 1920 }); await live(state);
    assert.equal(state.frames[0].style.height, "498px");
    state.feed.clientWidth = 390; state.resizeObservers[0].callback();
    assert.equal(state.frames[0].style.height, "693px");
    assert.equal(state.engines[0].starts, 1);
    await scanner.cerrar(state.dialog); assertLiveClean(state);
});

test("fallback resize sin ResizeObserver desaparece al cerrar", async () => {
    const state = setup({ noResize: true }); await live(state);
    assert.equal(state.browser.listenerCount(), 2);
    await scanner.cerrar(state.dialog); assert.equal(state.browser.listenerCount(), 0);
});
