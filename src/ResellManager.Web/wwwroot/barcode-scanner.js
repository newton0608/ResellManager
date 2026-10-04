// html5-qrcode 2.3.8 se carga desde wwwroot solo al abrir el lector.
let libraryPromise;
let active;

function loadLibrary() {
    if (window.Html5Qrcode && window.Html5QrcodeSupportedFormats) return Promise.resolve();
    if (!libraryPromise) {
        libraryPromise = new Promise((resolve, reject) => {
            const script = document.createElement("script");
            script.src = new URL("vendor/html5-qrcode/html5-qrcode.min.js", document.baseURI).href;
            script.onload = () => {
                if (window.Html5Qrcode && window.Html5QrcodeSupportedFormats) resolve();
                else reject(new Error("La librería de escaneo no está disponible."));
            };
            script.onerror = () => reject(new Error("No se pudo cargar la librería de escaneo."));
            document.head.appendChild(script);
        }).catch(error => {
            libraryPromise = undefined;
            throw error;
        });
    }
    return libraryPromise;
}

function classify(error) {
    const name = error?.name ?? "";
    const message = String(error?.message ?? error ?? "");
    if (name === "NotAllowedError" || name === "PermissionDeniedError" || /permission|denied|not allowed/i.test(message))
        return "denied";
    if (name === "NotFoundError" || name === "DevicesNotFoundError" || /no cameras|no camera|not found|requested device not found/i.test(message))
        return "no-camera";
    if (name === "SecurityError" || /secure context|https|not supported/i.test(message))
        return "unsupported";
    return "startup-error";
}

// Dimensiones CSS reales del video proporcionadas por la librería.
// El video conserva su proporción; el contenedor recorta sólo sus márgenes verticales.
function barcodeBox(width, height) {
    const maxHeight = Math.min(height, Math.max(50, Math.floor(height * 0.8)));
    const boxWidth = Math.floor(Math.min(width * 0.92, maxHeight * 2.8));
    const boxHeight = Math.min(maxHeight, Math.max(Math.min(50, height), Math.floor(boxWidth / 2.8)));
    return { width: Math.min(width, Math.max(Math.min(50, width), boxWidth)), height: boxHeight };
}

// applyConstraints reemplaza el conjunto anterior: conservar preferencias flexibles.
function cameraConstraints(controls) {
    const constraints = {
        facingMode: { ideal: "environment" },
        width: { ideal: 1920 }, height: { ideal: 1080 }
    };
    if (controls) constraints.advanced = [controls];
    return constraints;
}

function applyControls(session, controls) {
    if (session.focusContinuous) controls = { focusMode: "continuous", ...controls };
    return session.scanner.applyVideoConstraints(cameraConstraints(controls));
}
function zoomRange(capability) {
    if (!capability || !Number.isFinite(capability.min) || !Number.isFinite(capability.max)
        || !Number.isFinite(capability.step) || capability.min <= 0
        || capability.max <= capability.min || capability.step <= 0) return null;
    const steps = Math.floor((capability.max - capability.min) / capability.step + 1e-8);
    if (steps < 1) return null;
    return { min: capability.min, max: capability.max, step: capability.step, steps };
}

function safeZoom(range, value) {
    const limited = Math.max(range.min, Math.min(range.max, value));
    const steps = Math.max(0, Math.min(range.steps, Math.round((limited - range.min) / range.step)));
    return Math.max(range.min, Math.min(range.max, Number((range.min + steps * range.step).toPrecision(15))));
}

function running(session) {
    return alive(session.owner) && session.owner.live === session && !session.cancelled && !session.done;
}

function trackZoom(session, fallback) {
    try {
        const value = session.scanner.getRunningTrackSettings?.().zoom;
        if (Number.isFinite(value) && value >= session.zoom.range.min && value <= session.zoom.range.max)
            return value;
    } catch { /* Algunos navegadores no exponen settings. */ }
    return fallback;
}

function showZoomValue(session, value) {
    const { input, output } = session.zoom;
    input.value = String(value);
    input.setAttribute("aria-valuetext", value.toLocaleString("es", { maximumFractionDigits: 2 }) + "×");
    output.textContent = input.getAttribute("aria-valuetext");
}

async function updateZoom(session) {
    const zoom = session.zoom;
    if (zoom.applying) return;
    zoom.applying = true;
    zoom.input.disabled = true;
    try {
        while (running(session) && zoom.desired !== null) {
            const requested = zoom.desired;
            zoom.desired = null;
            try {
                await applyControls(session, { zoom: requested });
                if (!running(session)) return;
                zoom.current = trackZoom(session, requested);
                showZoomValue(session, zoom.current);
                zoom.message.textContent = "";
            } catch {
                if (!running(session)) return;
                showZoomValue(session, zoom.current);
                zoom.message.textContent = "No se pudo ajustar el zoom. Puedes seguir escaneando.";
                zoom.desired = null;
            }
        }
    } finally {
        zoom.applying = false;
        if (running(session)) zoom.input.disabled = false;
    }
}

async function configureCamera(session) {
    let capabilities;
    try { capabilities = session.scanner.getRunningTrackCapabilities?.(); }
    catch { return; }
    if (!running(session) || !capabilities) return;

    // Foco opcional: una cámara que no lo expone o rechaza continúa normalmente.
    if (Array.isArray(capabilities.focusMode) && capabilities.focusMode.includes("continuous")) {
        try {
            await applyControls(session, { focusMode: "continuous" });
            session.focusContinuous = true;
        }
        catch { /* El enfoque automático propio de la cámara sigue disponible. */ }
    }
    if (!running(session)) return;
    const range = zoomRange(capabilities.zoom);
    const wrapper = session.dialog.querySelector("[data-barcode-zoom]");
    const input = session.dialog.querySelector("[data-barcode-zoom-input]");
    const output = session.dialog.querySelector("[data-barcode-zoom-value]");
    const message = session.dialog.querySelector("[data-barcode-zoom-message]");
    if (!range || !wrapper || !input || !output || !message
        || typeof session.scanner.applyVideoConstraints !== "function") return;

    session.zoom = { range, wrapper, input, output, message, current: range.min, desired: null, applying: false };
    session.zoom.current = trackZoom(session, range.min);
    input.min = String(range.min);
    input.max = String(safeZoom(range, range.max));
    input.step = String(range.step);
    input.disabled = true;
    const initial = safeZoom(range, 1.8);
    try {
        await applyControls(session, { zoom: initial });
    } catch {
        // Capacidad anunciada pero no aplicable: sin slider engañoso ni error de inicio.
        return;
    }
    if (!running(session)) return;
    session.zoom.current = trackZoom(session, initial);
    showZoomValue(session, session.zoom.current);
    message.textContent = "";
    session.onZoomInput = () => {
        const value = Number(input.value);
        if (!running(session) || !Number.isFinite(value)) return;
        session.zoom.desired = safeZoom(range, value);
        void updateZoom(session);
    };
    input.addEventListener("input", session.onZoomInput);
    input.disabled = false;
    wrapper.hidden = false;
}

function hideZoom(session) {
    const input = session.dialog.querySelector("[data-barcode-zoom-input]");
    if (input) {
        input.removeEventListener("input", session.onZoomInput);
        input.disabled = true;
    }
    const wrapper = session.dialog.querySelector("[data-barcode-zoom]");
    if (wrapper) wrapper.hidden = true;
}

// html5-qrcode calcula su crop una vez. Mantener la geometría CSS interna
// al cambiar orientación y adaptar el conjunto video/marco evita desalinearlo.
// Esta escala es sólo layout responsive; NO es el zoom de cámara ni altera el decoder.
// En 2.3.8, ZXing lee el backing canvas, pero drawImage usa el destino
// qrRegion en píxeles CSS. Con disableFlip:true podemos aprovechar hasta 2×
// detalle nativo sin cambiar el crop, el marco ni presentar un zoom visual.
function improveDecoderSampling(session) {
    if (!running(session)) return;
    const feed = session.dialog.querySelector(".barcode-camera-feed");
    const canvas = feed?.querySelector?.("canvas");
    const video = feed?.querySelector?.("video");
    if (!canvas || !video || session.sampledCanvas === canvas) return;
    if (!(video.clientWidth > 0 && video.clientHeight > 0 && video.videoWidth > 0 && video.videoHeight > 0)) return;
    const scale = Math.min(2, video.videoWidth / video.clientWidth, video.videoHeight / video.clientHeight);
    if (!Number.isFinite(scale) || scale < 1) return;
    const width = canvas.width, height = canvas.height;
    let context;
    try { context = canvas.getContext?.("2d"); } catch { return; }
    if (!width || !height || !context || typeof context.setTransform !== "function") return;
    session.sampledCanvas = canvas;
    if (scale === 1) return;
    try {
        canvas.width = Math.floor(width * scale);
        canvas.height = Math.floor(height * scale);
        context.setTransform(canvas.width / width, 0, 0, canvas.height / height, 0, 0);
    } catch {
        // Mantener el decoder original si el canvas no permite este ajuste.
        canvas.width = width;
        canvas.height = height;
    }
}
function adaptView(session) {
    const feed = session.dialog.querySelector(".barcode-camera-feed");
    const frame = feed?.parentElement;
    const width = feed?.clientWidth;
    if (!frame || !width) return;
    feed.style.width = width + "px";
    feed.style.left = "50%";
    feed.style.right = "auto";
    const resize = () => {
        if (!running(session)) return;
        const scale = frame.clientWidth / width;
        feed.style.transform = "translate(-50%, -50%) scale(" + scale + ")";
    };
    if (typeof ResizeObserver !== "undefined") {
        session.resizeObserver = new ResizeObserver(resize);
        session.resizeObserver.observe(frame);
    } else {
        session.onResize = resize;
        window.addEventListener("resize", resize);
    }
    resize();
}

function resetView(dialog) {
    const feed = dialog.querySelector(".barcode-camera-feed");
    if (!feed) return;
    for (const property of ["width", "left", "right", "transform"]) feed.style.removeProperty(property);
}
function alive(session) {
    return !!session && active === session && !session.cancelled && !session.done;
}

function element(session, name) {
    return session.dialog.querySelector("[data-barcode-" + name + "]");
}

function photoState(session, state, message = "", code = null) {
    session.mode = "photo";
    session.pendingCode = code;
    const photo = element(session, "photo");
    if (photo) {
        photo.hidden = false;
        photo.setAttribute("aria-busy", String(state === "analysing"));
    }
    const panel = element(session, "live-panel");
    if (panel) panel.hidden = true;
    const result = element(session, "result");
    if (result) result.hidden = code === null;
    const output = element(session, "code");
    if (output) output.textContent = code ?? "";
    const status = element(session, "photo-status");
    if (status) status.textContent = message;
    const label = element(session, "capture-label");
    if (label) label.textContent = state === "idle" ? "Tomar foto" : "Tomar otra foto";
    for (const name of ["capture", "file", "live", "use"]) {
        const control = element(session, name);
        if (control) control.disabled = state === "analysing" || session.transitioning
            || (name === "use" && code === null);
    }
}

function scannerInstance(id) {
    const formats = window.Html5QrcodeSupportedFormats;
    return new window.Html5Qrcode(id, {
        formatsToSupport: [formats.EAN_13, formats.EAN_8, formats.UPC_A, formats.UPC_E, formats.CODE_128],
        useBarCodeDetectorIfSupported: false
    });
}

function closeDialog(session, manual = false) {
    if (session.dialog.open) session.dialog.close();
    // Preferir el input hermano del consumidor, sin modificar su valor ni contrato.
    const field = manual ? session.dialog.parentElement?.querySelector(
        "input:not([type='file']):not([type='range']), textarea") : null;
    const target = field && !session.dialog.contains?.(field) && !field.disabled ? field : session.origin;
    if (target?.isConnected) target.focus();
}

function captureTracks(session) {
    session.dialog.querySelectorAll("video").forEach(video => {
        video.srcObject?.getTracks?.().forEach(track => session.tracks.add(track));
    });
}

function releaseTracks(session) {
    session.tracks.forEach(track => track.stop());
    session.tracks.clear();
    session.dialog.querySelectorAll("video").forEach(video => { video.srcObject = null; });
}

function stopCamera(session) {
    if (!session) return Promise.resolve();
    if (session.stopPromise) return session.stopPromise;
    session.cancelled = true;
    clearTimeout(session.timeout);
    hideZoom(session);
    session.resizeObserver?.disconnect();
    if (session.onResize) window.removeEventListener("resize", session.onResize);
    session.stopPromise = (async () => {
        try { await session.startPromise; } catch { /* Permiso o inicio fallido. */ }
        captureTracks(session);
        try { await session.scanner?.stop(); } catch { /* Puede no haber iniciado. */ }
        releaseTracks(session);
        try { session.scanner?.clear(); } catch { /* Tracks ya liberados. */ }
    })();
    return session.stopPromise;
}

// Adaptador acotado a 2.3.8: scanFileV2 crea dos object URLs y no las libera.
// Las APIs se interceptan sólo durante su llamada síncrona, nunca durante un await.
// Capturar el Image permite cancelar su carga sin dejar un onload sobre un host descartado.
function localPhotoScan(session, file) {
    const urls = new Set();
    const images = new Set();
    const urlApi = globalThis.URL;
    if (typeof urlApi?.createObjectURL !== "function" || typeof urlApi?.revokeObjectURL !== "function"
        || typeof window.Image !== "function") throw new Error("Local image processing unavailable");
    const originalCreate = urlApi.createObjectURL;
    const OriginalImage = window.Image;
    let rejectCancellation;
    let cancelled = false;
    const cancellation = new Promise((_, reject) => { rejectCancellation = reject; });
    const captureUrl = function(blob) {
        const url = originalCreate.call(urlApi, blob);
        if (blob === file) urls.add(url);
        return url;
    };
    const CapturedImage = new Proxy(OriginalImage, {
        construct(target, args) {
            const image = Reflect.construct(target, args);
            images.add(image);
            return image;
        }
    });
    const clean = () => {
        for (const image of images) {
            for (const event of ["onload", "onerror", "onabort", "onstalled", "onsuspend"])
                image[event] = null;
            image.removeAttribute?.("src");
        }
        images.clear();
        for (const url of urls) urlApi.revokeObjectURL(url);
        urls.clear();
    };
    const operation = {
        cancel() {
            if (cancelled) return;
            cancelled = true;
            clean();
            rejectCancellation(new Error("Photo analysis cancelled"));
        }
    };
    session.photoOperation = operation;
    let decode;
    try {
        urlApi.createObjectURL = captureUrl;
        window.Image = CapturedImage;
        if (urlApi.createObjectURL !== captureUrl || window.Image !== CapturedImage)
            throw new Error("Local image cleanup unavailable");
        decode = session.photoScanner.scanFileV2(file, false);
        // El vendor no captura excepciones de onload (p. ej. canvas sin memoria).
        // Convertirlas en fallo recuperable, en vez de dejar Analizando indefinidamente.
        for (const image of images) {
            const onload = image.onload;
            if (typeof onload === "function") image.onload = function(...args) {
                try { return onload.apply(this, args); }
                catch (error) { rejectCancellation(error); }
            };
        }
    } catch (error) {
        decode = Promise.reject(error);
    } finally {
        if (urlApi.createObjectURL === captureUrl) urlApi.createObjectURL = originalCreate;
        if (window.Image === CapturedImage) window.Image = OriginalImage;
    }
    return Promise.race([decode, cancellation]).finally(() => {
        clean();
        if (session.photoOperation === operation) session.photoOperation = null;
        try { session.photoScanner?.clear(); } catch { /* El lector ya puede estar vacío. */ }
    });
}

async function analysePhoto(session, file) {
    if (!alive(session) || session.mode !== "photo" || session.analysing || session.transitioning || !file) return;
    session.analysing = true;
    photoState(session, "analysing", "Analizando código…");
    // File permanece exclusivamente en JS; ningún byte cruza la interop de Blazor.
    session.photoPromise = (async () => {
        try {
            await Promise.race([loadLibrary(), session.closed]);
            if (!alive(session)) return;
            const reader = element(session, "file-reader");
            session.photoScanner ??= scannerInstance(reader.id);
            const result = await localPhotoScan(session, file);
            if (!alive(session)) return;
            const code = result?.decodedText;
            if (typeof code !== "string" || !code.length) throw new Error("No barcode");
            photoState(session, "success", "Revisa el código antes de usarlo.", code);
            element(session, "use")?.focus?.();
        } catch {
            if (alive(session)) photoState(session, "error", "No encontramos un código de barras en la foto.");
        } finally {
            session.analysing = false;
            // No se conserva el File, preview, canvas ni object URL después del análisis.
        }
    })();
    await session.photoPromise;
}

function release(session) {
    if (session.releasePromise) return session.releasePromise;
    session.cancelled = true;
    session.resolveClosed();
    session.photoOperation?.cancel();
    session.pendingCode = null;
    const file = element(session, "file");
    if (file) file.value = "";
    session.dialog.removeEventListener("cancel", session.onCancel);
    session.dialog.removeEventListener("close", session.onClose);
    document.removeEventListener("visibilitychange", session.onVisibilityChange);
    window.removeEventListener("pagehide", session.onPageHide);
    session.observer?.disconnect();
    for (const [control, name, listener] of session.listeners) control.removeEventListener(name, listener);
    session.listeners.length = 0;
    session.releasePromise = (async () => {
        await stopCamera(session.live);
        try { await session.photoPromise; } catch { /* Resultado descartado. */ }
        try { session.photoScanner?.clear(); } catch { /* El canvas ya está vacío. */ }
        if (active === session) active = undefined;
    })();
    return session.releasePromise;
}

async function finish(session, reason, value = null) {
    if (!alive(session)) return;
    session.done = true;
    closeDialog(session, reason === "manual");
    await release(session);
    try { await session.reference.invokeMethodAsync("FinalizarEscaneo", reason, value); }
    catch { /* El circuito de Blazor pudo cerrarse durante el análisis. */ }
}

async function liveResult(camera, reason, value = null) {
    if (!running(camera)) return;
    camera.done = true;
    const session = camera.owner;
    session.transitioning = true;
    await stopCamera(camera);
    if (!alive(session)) return;
    session.live = null;
    session.transitioning = false;
    if (reason === "detected") {
        photoState(session, "success", "Revisa el código antes de usarlo.", value);
        element(session, "use")?.focus?.();
    } else {
        photoState(session, "error", liveMessage(reason));
    }
}

function liveMessage(reason) {
    return {
        denied: "El permiso de cámara fue denegado. Puedes tomar una foto o introducir el código manualmente.",
        "no-camera": "No se encontró una cámara. Puedes elegir una imagen o introducir el código manualmente.",
        unsupported: "Este navegador no permite usar la cámara aquí. Prueba una foto o introduce el código manualmente.",
        timeout: "No se pudo detectar un código. Prueba una foto o introduce el código manualmente.",
        "startup-error": "No se pudo iniciar la cámara. Prueba una foto o introduce el código manualmente."
    }[reason] ?? "Prueba una foto o introduce el código manualmente.";
}

export async function escanearEnVivo(dialog) {
    const session = active;
    if (!session || session.dialog !== dialog || !alive(session) || session.analysing || session.transitioning || session.live) return "busy";
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
        photoState(session, "error", liveMessage("unsupported"));
        return "unsupported";
    }
    session.transitioning = true;
    session.pendingCode = null;
    session.mode = "live";
    element(session, "photo").hidden = true;
    element(session, "live-panel").hidden = false;
    element(session, "back-photo").disabled = true;
    element(session, "live-status").textContent = "Solicitando acceso a la cámara…";
    try { session.photoScanner?.clear(); } catch { /* Sin imagen activa. */ }
    hideZoom({ dialog });
    resetView(dialog);
    const camera = {
        owner: session, dialog, scanner: null, startPromise: null, stopPromise: null,
        timeout: null, tracks: new Set(), cancelled: false, done: false
    };
    session.live = camera;
    try {
        await loadLibrary();
        if (!alive(session)) return "cancelled";
        camera.scanner = scannerInstance(session.viewId);
        camera.startPromise = Promise.resolve().then(() => camera.scanner.start(
            { facingMode: "environment" },
            {
                fps: 10, disableFlip: true, qrbox: barcodeBox,
                videoConstraints: cameraConstraints()
            },
            text => { if (text) void liveResult(camera, "detected", text); },
            () => { /* Sin coincidencia en este fotograma. */ }
        ));
        await camera.startPromise;
        captureTracks(camera);
        if (camera.done) return "started";
        if (!alive(session) || camera.cancelled) {
            await stopCamera(camera);
            return "cancelled";
        }
        improveDecoderSampling(camera);
        adaptView(camera);
        camera.timeout = setTimeout(() => void liveResult(camera, "timeout"), 45000);
        session.transitioning = false;
        element(session, "back-photo").disabled = false;
        element(session, "live-status").textContent = "Buscando código…";
        void configureCamera(camera);
        return "started";
    } catch (error) {
        const reason = classify(error);
        await stopCamera(camera);
        if (alive(session)) {
            session.live = null;
            session.transitioning = false;
            photoState(session, "error", liveMessage(reason));
        }
        return reason;
    }
}

export async function volverAFoto(dialog) {
    const session = active;
    if (!session || session.dialog !== dialog || !alive(session) || session.analysing || session.transitioning) return;
    session.transitioning = true;
    const camera = session.live;
    await stopCamera(camera);
    if (!alive(session)) return;
    session.live = null;
    session.transitioning = false;
    photoState(session, "idle", "Puedes tomar una foto o elegir una imagen.");
    element(session, "capture")?.focus?.();
}

export async function abrir(dialog, viewId, reference) {
    if (active) return "busy";
    if (typeof dialog.showModal !== "function") return "unsupported";
    const session = {
        dialog, viewId, reference, origin: document.activeElement,
        cancelled: false, done: false, mode: "photo", live: null,
        analysing: false, transitioning: false, photoScanner: null,
        photoOperation: null, photoPromise: null, releasePromise: null,
        pendingCode: null, listeners: []
    };
    session.closed = new Promise(resolve => { session.resolveClosed = resolve; });
    active = session;
    const listen = (name, event, callback) => {
        const control = element(session, name);
        if (!control) return;
        control.addEventListener(event, callback);
        session.listeners.push([control, event, callback]);
    };
    listen("capture", "click", () => {
        if (!alive(session) || session.mode !== "photo" || session.analysing || session.transitioning) return;
        const file = element(session, "file");
        file.value = "";
        // Sin await/interop: conservar la activación del gesto para la cámara nativa.
        file.click();
    });
    listen("file", "change", () => {
        const input = element(session, "file");
        const file = input.files?.[0];
        input.value = ""; // Permite elegir exactamente la misma fotografía de nuevo.
        void analysePhoto(session, file);
    });
    listen("use", "click", () => {
        if (!session.analysing && !session.transitioning && session.pendingCode !== null)
            void finish(session, "detected", session.pendingCode);
    });
    listen("manual", "click", () => { void finish(session, "manual"); });
    listen("live", "click", () => { void escanearEnVivo(dialog); });
    listen("back-photo", "click", () => { void volverAFoto(dialog); });
    session.onCancel = event => {
        event.preventDefault();
        void cerrar(dialog);
        void reference.invokeMethodAsync("CancelarDesdeTeclado").catch(() => {});
    };
    session.onClose = () => {
        if (alive(session)) {
            void cerrar(dialog);
            void reference.invokeMethodAsync("CancelarDesdeTeclado").catch(() => {});
        }
    };
    // El picker nativo de iOS puede ocultar la página: en modo foto debe seguir abierto.
    session.onVisibilityChange = () => {
        if (document.hidden && session.mode === "live") void finish(session, "background");
    };
    session.onPageHide = () => { void finish(session, "background"); };
    dialog.addEventListener("cancel", session.onCancel);
    dialog.addEventListener("close", session.onClose);
    document.addEventListener("visibilitychange", session.onVisibilityChange);
    window.addEventListener("pagehide", session.onPageHide);
    if (typeof MutationObserver !== "undefined") {
        session.observer = new MutationObserver(() => {
            if (!dialog.isConnected) void release(session);
            else if (session.live) improveDecoderSampling(session.live);
        });
        session.observer.observe(document.body, { childList: true, subtree: true });
    }
    try {
        hideZoom({ dialog });
        photoState(session, "idle", "Puedes tomar una foto o elegir una imagen.");
        const input = element(session, "file");
        if (input) input.value = "";
        dialog.showModal();
        return "ready";
    } catch {
        await release(session);
        closeDialog(session);
        return "startup-error";
    }
}

export async function cerrar(dialog) {
    const session = active;
    if (!session || session.dialog !== dialog) return;
    session.cancelled = true;
    closeDialog(session);
    await release(session);
}
