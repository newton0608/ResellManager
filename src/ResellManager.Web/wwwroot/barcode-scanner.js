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
    return active === session && !session.cancelled && !session.done;
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
function closeDialog(session) {
    if (session.dialog.open) session.dialog.close();
    if (session.origin?.isConnected) session.origin.focus();
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

function release(session) {
    if (session.stopPromise) return session.stopPromise;
    session.cancelled = true;
    clearTimeout(session.timeout);
    hideZoom(session);
    session.dialog.removeEventListener("cancel", session.onCancel);
    session.dialog.removeEventListener("close", session.onClose);
    document.removeEventListener("visibilitychange", session.onVisibilityChange);
    window.removeEventListener("pagehide", session.onPageHide);
    session.observer?.disconnect();
    session.resizeObserver?.disconnect();
    if (session.onResize) window.removeEventListener("resize", session.onResize);
    session.stopPromise = (async () => {
        try { await session.startPromise; } catch { /* Error de inicio ya se comunica arriba. */ }
        captureTracks(session);
        if (session.scanner) {
            try { await session.scanner.stop(); } catch { /* Puede no haber llegado a iniciar. */ }
            releaseTracks(session);
            try { session.scanner.clear(); } catch { /* La cámara ya está cerrada. */ }
        }
        if (active === session) active = undefined;
    })();
    return session.stopPromise;
}

async function finish(session, reason, value) {
    if (session.done || session.cancelled) return;
    session.done = true;
    await release(session);
    closeDialog(session);
    try { await session.reference.invokeMethodAsync("FinalizarEscaneo", reason, value); }
    catch { /* El circuito de Blazor pudo cerrarse durante el escaneo. */ }
}

export async function abrir(dialog, viewId, reference) {
    if (active) return "busy";
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia || typeof dialog.showModal !== "function") return "unsupported";

    const session = {
        dialog, reference, origin: document.activeElement,
        cancelled: false, done: false, scanner: null,
        startPromise: null, stopPromise: null, timeout: null, tracks: new Set()
    };
    session.onCancel = event => {
        event.preventDefault();
        // Escape libera localmente aunque el circuito .NET ya no responda.
        void cerrar(dialog);
        void reference.invokeMethodAsync("CancelarDesdeTeclado").catch(() => {});
    };
    session.onClose = () => {
        if (running(session)) {
            void cerrar(dialog);
            void reference.invokeMethodAsync("CancelarDesdeTeclado").catch(() => {});
        }
    };
    session.onVisibilityChange = () => { if (document.hidden) void finish(session, "background", null); };
    session.onPageHide = () => { void finish(session, "background", null); };
    active = session;
    hideZoom(session);
    resetView(dialog);
    dialog.addEventListener("cancel", session.onCancel);
    dialog.addEventListener("close", session.onClose);
    document.addEventListener("visibilitychange", session.onVisibilityChange);
    window.addEventListener("pagehide", session.onPageHide);
    if (typeof MutationObserver !== "undefined") {
        session.observer = new MutationObserver(() => {
            if (!dialog.isConnected) {
                session.cancelled = true;
                void release(session);
            } else {
                improveDecoderSampling(session);
            }
        });
        session.observer.observe(document.body, { childList: true, subtree: true });
    }

    try {
        dialog.showModal();
        await loadLibrary();
        if (session.cancelled) return "cancelled";

        const formats = window.Html5QrcodeSupportedFormats;
        session.scanner = new window.Html5Qrcode(viewId, {
            formatsToSupport: [formats.EAN_13, formats.EAN_8, formats.UPC_A, formats.UPC_E, formats.CODE_128],
            useBarCodeDetectorIfSupported: false
        });
        // La promesa existe antes de cualquier callback sincrónico de start.
        session.startPromise = Promise.resolve().then(() => session.scanner.start(
            { facingMode: "environment" },
            {
                fps: 10, disableFlip: true, qrbox: barcodeBox,
                // videoConstraints sustituye cameraIdOrConfig en html5-qrcode.
                videoConstraints: cameraConstraints()
            },
            text => { if (text) void finish(session, "detected", text); },
            () => { /* Sin coincidencia en este fotograma; continúa buscando. */ }
        ));
        await session.startPromise;
        captureTracks(session);
        if (session.done) return "started";
        if (session.cancelled) {
            await release(session);
            return "cancelled";
        }
        improveDecoderSampling(session);
        adaptView(session);
        session.timeout = setTimeout(() => void finish(session, "timeout", null), 45000);
        // Los ajustes opcionales nunca bloquean el inicio ni la detección.
        void configureCamera(session);
        return "started";
    } catch (error) {
        const reason = classify(error);
        await release(session);
        closeDialog(session);
        return reason;
    }
}

export async function cerrar(dialog) {
    const session = active;
    if (!session || session.dialog !== dialog) return;
    session.cancelled = true;
    closeDialog(session);
    await release(session);
}