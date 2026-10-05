// Quagga2 1.11.0: local bundle, loaded only when a photo/live operation needs it.
// EAN-8 first: the official vertical EAN-8 fixture is misread when EAN runs first.
// The real-browser regression verifies the unchanged printed code (42191605).
const READERS = ["ean_8_reader", "ean_reader", "upc_reader", "upc_e_reader", "code_128_reader"];
const FORMATS = new Map([["ean_13", 13], ["ean_8", 8], ["upc_a", 12], ["upc_e", 8], ["code_128", null]]);
const PHOTO_SOURCE_SIZE = 2560;
const PHOTO_DECODE_SIZE = 1920;
const CONFIRMATION_WINDOW_MS = 1500;
let active;

function aborted() {
    return new DOMException("Scanner operation cancelled", "AbortError");
}

function operation() {
    const resources = new Set();
    let reject;
    const cancellation = new Promise((_, fail) => { reject = fail; });
    cancellation.catch(() => {});
    return {
        disposed: false,
        own(clean) {
            let released = false;
            const release = () => {
                if (released) return;
                released = true;
                resources.delete(release);
                try { clean(); } catch { /* Continue releasing the remaining resources. */ }
            };
            if (this.disposed) release();
            else resources.add(release);
            return release;
        },
        wait(promise) {
            return this.disposed ? Promise.reject(aborted()) : Promise.race([promise, cancellation]);
        },
        cancel(error = aborted()) {
            reject(error);
            this.dispose();
        },
        dispose() {
            this.disposed = true;
            for (const release of [...resources]) release();
        }
    };
}

function stopTrack(track) {
    if (track?.readyState !== "ended") {
        try { track?.stop(); } catch { /* A disconnected device may already be gone. */ }
    }
}

function clearMedia(target) {
    target?.querySelectorAll("video").forEach(video => {
        video.srcObject?.getTracks?.().forEach(stopTrack);
        video.pause?.();
        video.srcObject = null;
        video.removeAttribute("src");
    });
    target?.querySelectorAll("canvas").forEach(canvas => { canvas.width = canvas.height = 0; });
}

// decodeSingle has no public abort API and uses internal Images/canvases/events.
// A disposable same-origin about:blank document owns those resources, including
// live initialization while Safari's permission prompt is still pending.
// No application page is embedded (production X-Frame-Options stays DENY).
function loadDecoder(task, host = document.body, visible = false) {
    return task.wait(new Promise((resolve, reject) => {
        const frame = document.createElement("iframe");
        frame.hidden = !visible;
        frame.title = visible ? "Vista de la cámara" : "Análisis local de código de barras";
        frame.setAttribute("allow", "camera");
        frame.setAttribute("tabindex", "-1");
        frame.src = "about:blank";
        let script, decoder, target;
        task.own(() => {
            frame.onload = frame.onerror = null;
            if (script) script.onload = script.onerror = null;
            clearMedia(target);
            try {
                decoder?.offDetected();
                decoder?.offProcessed();
                decoder?.stop()?.catch?.(() => {});
            } catch { /* The frame might have been removed during initialization. */ }
            frame.remove();
            script = decoder = target = null;
        });
        frame.onerror = () => reject(new Error("Local decoder document unavailable"));
        frame.onload = () => {
            if (task.disposed) return;
            frame.onload = null;
            try {
                const page = frame.contentDocument;
                target = page.body;
                target.style.margin = "0";
                target.style.overflow = "hidden";
                script = page.createElement("script");
                script.src = new URL("vendor/quagga2/quagga.min.js", document.baseURI).href;
                script.onload = () => {
                    script.onload = script.onerror = null;
                    decoder = frame.contentWindow.Quagga;
                    if (!decoder?.init || !decoder?.decodeSingle) reject(new Error("Quagga2 unavailable"));
                    else resolve({ decoder, target, frame });
                };
                script.onerror = () => reject(new Error("Local Quagga2 bundle could not be loaded"));
                page.head.appendChild(script);
            } catch (error) { reject(error); }
        };
        host.appendChild(frame);
    }));
}

function classify(error) {
    const name = error?.name ?? "";
    const message = String(error?.message ?? error ?? "");
    if (name === "NotAllowedError" || name === "PermissionDeniedError" || /permission|denied|not allowed/i.test(message))
        return "denied";
    if (name === "NotFoundError" || name === "DevicesNotFoundError" || /no cameras|no camera|requested device not found/i.test(message))
        return "no-camera";
    if (name === "SecurityError" || /secure context|https|not supported/i.test(message))
        return "unsupported";
    return "startup-error";
}

function confirmedCode(result) {
    const value = result?.codeResult;
    if (!value || !FORMATS.has(value.format) || typeof value.code !== "string" || !value.code.length) return null;
    const length = FORMATS.get(value.format);
    if (length !== null && (value.code.length !== length || !/^[0-9]+$/.test(value.code))) return null;
    // Pattern errors have reader-specific scales, not a calibrated confidence
    // score. Reject only malformed measurements; rely on reader checksums and
    // repeated live readings rather than inventing a numerical threshold.
    if (value.decodedCodes?.some(part => part.error !== undefined
        && (!Number.isFinite(part.error) || part.error < 0))) return null;
    return value.code; // Preserve exactly what Quagga returned, including leading zeros.
}

function alive(session) {
    return !!session && active === session && !session.cancelled && !session.done;
}

function running(camera) {
    return alive(camera?.owner) && camera.owner.live === camera && !camera.cancelled && !camera.done;
}

function element(session, name) {
    return session.dialog.querySelector("[data-barcode-" + name + "]");
}

function photoState(session, state, message = "", code = null) {
    session.mode = "photo";
    session.pendingCode = code;
    const photo = element(session, "photo");
    photo.hidden = false;
    photo.setAttribute("aria-busy", String(state === "analyzing"));
    photo.setAttribute("data-state", state);
    element(session, "live-panel").hidden = true;
    element(session, "result").hidden = code === null;
    element(session, "code").textContent = code ?? "";
    element(session, "photo-status").textContent = message;
    element(session, "capture-label").textContent = state === "idle" ? "Tomar foto" : "Tomar otra foto";
    for (const name of ["capture", "file", "live", "use"]) {
        const control = element(session, name);
        if (control) control.disabled = state === "analyzing" || session.transitioning
            || (name === "use" && code === null);
    }
}

function closeDialog(session, manual = false) {
    if (session.dialog.open) session.dialog.close();
    const field = manual ? session.dialog.parentElement?.querySelector(
        "input:not([type='file']):not([type='range']), textarea") : null;
    const target = field && !session.dialog.contains?.(field) && !field.disabled ? field : session.origin;
    if (target?.isConnected) target.focus();
}

function cameraConstraints(controls) {
    const constraints = {
        facingMode: { ideal: "environment" },
        width: { ideal: 1920 }, height: { ideal: 1080 }
    };
    if (controls) constraints.advanced = [controls];
    return constraints;
}

function applyControls(camera, controls) {
    if (camera.focusContinuous) controls = { focusMode: "continuous", ...controls };
    return camera.track.applyConstraints(cameraConstraints(controls));
}

function zoomRange(capability) {
    if (!capability || !Number.isFinite(capability.min) || !Number.isFinite(capability.max)
        || !Number.isFinite(capability.step) || capability.min <= 0
        || capability.max <= capability.min || capability.step <= 0) return null;
    const steps = Math.floor((capability.max - capability.min) / capability.step + 1e-8);
    return steps < 1 ? null : { min: capability.min, max: capability.max, step: capability.step, steps };
}

function safeZoom(range, value) {
    const limited = Math.max(range.min, Math.min(range.max, value));
    const steps = Math.max(0, Math.min(range.steps, Math.round((limited - range.min) / range.step)));
    return Math.max(range.min, Math.min(range.max, Number((range.min + steps * range.step).toPrecision(15))));
}

function trackZoom(camera, fallback) {
    try {
        const value = camera.track.getSettings?.().zoom;
        if (Number.isFinite(value) && value >= camera.zoom.range.min && value <= camera.zoom.range.max) return value;
    } catch { /* Optional settings API. */ }
    return fallback;
}

function showZoomValue(camera, value) {
    const { input, output } = camera.zoom;
    input.value = String(value);
    input.setAttribute("aria-valuetext", value.toLocaleString("es", { maximumFractionDigits: 2 }) + "×");
    output.textContent = input.getAttribute("aria-valuetext");
}

async function updateZoom(camera) {
    const zoom = camera.zoom;
    if (zoom.applying) return;
    zoom.applying = true;
    zoom.input.disabled = true;
    try {
        while (running(camera) && zoom.desired !== null) {
            const requested = zoom.desired;
            zoom.desired = null;
            try {
                await applyControls(camera, { zoom: requested });
                if (!running(camera)) return;
                zoom.current = trackZoom(camera, requested);
                showZoomValue(camera, zoom.current);
                zoom.message.textContent = "";
            } catch {
                if (!running(camera)) return;
                showZoomValue(camera, zoom.current);
                zoom.message.textContent = "No se pudo ajustar el zoom. Puedes seguir escaneando.";
                zoom.desired = null;
            }
        }
    } finally {
        zoom.applying = false;
        if (running(camera)) zoom.input.disabled = false;
    }
}

function hideZoom(camera) {
    if (camera.zoom?.listener) camera.zoom.input.removeEventListener("input", camera.zoom.listener);
    const wrapper = camera.dialog.querySelector("[data-barcode-zoom]");
    if (wrapper) wrapper.hidden = true;
}

async function configureCamera(camera) {
    let capabilities;
    try { capabilities = camera.track?.getCapabilities?.(); } catch { return; }
    if (!running(camera) || !capabilities || typeof camera.track.applyConstraints !== "function") return;
    if (Array.isArray(capabilities.focusMode) && capabilities.focusMode.includes("continuous")) {
        try {
            await applyControls(camera, { focusMode: "continuous" });
            camera.focusContinuous = true;
        } catch { /* Keep the camera's own autofocus if the optional control fails. */ }
    }
    if (!running(camera)) return;
    const range = zoomRange(capabilities.zoom);
    const wrapper = element(camera.owner, "zoom");
    const input = element(camera.owner, "zoom-input");
    const output = element(camera.owner, "zoom-value");
    const message = element(camera.owner, "zoom-message");
    if (!range || !wrapper || !input || !output || !message) return;
    camera.zoom = { range, wrapper, input, output, message, current: range.min, desired: null, applying: false };
    camera.zoom.current = trackZoom(camera, range.min);
    input.min = String(range.min);
    input.max = String(safeZoom(range, range.max));
    input.step = String(range.step);
    input.disabled = true;
    const initial = safeZoom(range, 1.8);
    try { await applyControls(camera, { zoom: initial }); } catch { return; }
    if (!running(camera)) return;
    camera.zoom.current = trackZoom(camera, initial);
    showZoomValue(camera, camera.zoom.current);
    message.textContent = "";
    camera.zoom.listener = () => {
        const value = Number(input.value);
        if (!running(camera) || !Number.isFinite(value)) return;
        camera.zoom.desired = safeZoom(range, value);
        void updateZoom(camera);
    };
    input.addEventListener("input", camera.zoom.listener);
    input.disabled = false;
    wrapper.hidden = false;
}

function adaptView(camera) {
    const video = camera.target.querySelector("video");
    if (!video) return;
    video.style.width = "100%";
    video.style.height = "auto";
    video.style.display = "block";
    const feed = camera.dialog.querySelector(".barcode-camera-feed");
    const resize = () => {
        if (!running(camera)) return;
        const ratio = video.videoHeight / video.videoWidth || 9 / 16;
        camera.frame.style.height = Math.round(feed.clientWidth * ratio) + "px";
    };
    if (typeof ResizeObserver !== "undefined") {
        const observer = new ResizeObserver(resize);
        observer.observe(feed);
        camera.task.own(() => observer.disconnect());
    } else {
        window.addEventListener("resize", resize);
        camera.task.own(() => window.removeEventListener("resize", resize));
    }
    resize();
}

function captureTracks(camera) {
    camera.target?.querySelectorAll("video").forEach(video => {
        video.srcObject?.getTracks?.().forEach(track => camera.tracks.add(track));
    });
}

function stopCamera(camera) {
    if (!camera || camera.cancelled) return;
    camera.cancelled = true;
    clearTimeout(camera.timeout);
    hideZoom(camera);
    captureTracks(camera);
    try {
        if (camera.detected) camera.decoder?.offDetected(camera.detected);
        if (camera.processed) camera.decoder?.offProcessed(camera.processed);
    } catch { /* The isolated decoder may already be gone. */ }
    camera.tracks.forEach(stopTrack);
    camera.tracks.clear();
    camera.task.cancel();
    camera.decoder = camera.target = camera.frame = camera.track = null;
    camera.candidate = null;
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

function liveResult(camera, reason, value = null) {
    if (!running(camera)) return;
    camera.done = true;
    const session = camera.owner;
    stopCamera(camera);
    if (!alive(session)) return;
    session.live = null;
    session.transitioning = false;
    photoState(session, reason === "detected" ? "success" : "technical-error",
        reason === "detected" ? "Revisa el código antes de usarlo." : liveMessage(reason), value);
    if (reason === "detected") element(session, "use")?.focus?.();
}

export async function escanearEnVivo(dialog) {
    const session = active;
    if (!session || session.dialog !== dialog || !alive(session) || session.analysing || session.transitioning || session.live) return "busy";
    if (!window.isSecureContext || !navigator.mediaDevices?.getUserMedia) {
        photoState(session, "technical-error", liveMessage("unsupported"));
        return "unsupported";
    }
    session.transitioning = true;
    session.pendingCode = null;
    element(session, "code").textContent = "";
    element(session, "result").hidden = true;
    session.mode = "live";
    element(session, "photo").hidden = true;
    element(session, "live-panel").hidden = false;
    element(session, "back-photo").disabled = false;
    element(session, "live-status").textContent = "Solicitando acceso a la cámara…";
    hideZoom({ dialog });
    const camera = { owner: session, dialog, task: operation(), tracks: new Set(),
        cancelled: false, done: false, candidate: null };
    session.live = camera;
    // Includes library load and the permission prompt, not only successful start.
    camera.timeout = setTimeout(() => liveResult(camera, "timeout"), 45000);
    try {
        const context = await loadDecoder(camera.task, dialog.querySelector(".barcode-camera-feed"), true);
        Object.assign(camera, context);
        if (!running(camera)) return "cancelled";
        const { decoder, target } = context;
        await camera.task.wait(new Promise((resolve, reject) => decoder.init({
            inputStream: {
                type: "LiveStream", target, size: 1280, constraints: cameraConstraints(),
                area: { top: "36%", right: "4%", left: "4%", bottom: "36%" }
            },
            locate: true, frequency: 10, numOfWorkers: 0,
            locator: { patchSize: "medium", halfSample: false },
            decoder: { readers: [...READERS], multiple: false },
            canvas: { createOverlay: false }
        }, error => {
            if (!running(camera)) {
                clearMedia(target);
                try { decoder.stop()?.catch?.(() => {}); } catch { /* Late permission result. */ }
                reject(aborted());
            } else if (error) reject(error);
            else resolve();
        })));
        if (!running(camera)) return "cancelled";
        captureTracks(camera);
        camera.track = decoder.CameraAccess?.getActiveTrack?.() ?? [...camera.tracks][0];
        camera.processed = result => {
            if (running(camera) && result?.codeResult && confirmedCode(result) === null) camera.candidate = null;
        };
        camera.detected = result => {
            if (!running(camera)) return;
            const code = confirmedCode(result);
            if (code === null) { camera.candidate = null; return; }
            const now = performance.now();
            if (camera.candidate?.code === code && now - camera.candidate.time <= CONFIRMATION_WINDOW_MS) {
                liveResult(camera, "detected", code);
            } else {
                camera.candidate = { code, time: now };
            }
        };
        decoder.onProcessed(camera.processed);
        decoder.onDetected(camera.detected);
        adaptView(camera);
        session.transitioning = false;
        element(session, "live-status").textContent = "Buscando código…";
        decoder.start();
        if (running(camera)) void configureCamera(camera);
        return "started";
    } catch (error) {
        if (camera.cancelled || !alive(session)) return "cancelled";
        const reason = classify(error);
        liveResult(camera, reason);
        return reason;
    }
}

export async function volverAFoto(dialog) {
    const session = active;
    if (!session || session.dialog !== dialog || !alive(session) || session.analysing || session.mode !== "live") return;
    stopCamera(session.live);
    session.live = null;
    session.transitioning = false;
    photoState(session, "idle", "Puedes tomar una foto o elegir una imagen.");
    element(session, "capture")?.focus?.();
}

function boundedSize(width, height, limit) {
    if (!(width > 0 && height > 0)) throw new Error("Invalid image dimensions");
    const scale = Math.min(1, limit / Math.max(width, height));
    return { width: Math.max(1, Math.round(width * scale)), height: Math.max(1, Math.round(height * scale)) };
}

// Read a bounded JPEG header, never a full 12/24/48 MP file into an ArrayBuffer.
// Neutralize its EXIF orientation in a sliced Blob and apply it exactly once in
// Canvas. This also makes the Image fallback deterministic on older Safari.
async function photoMetadata(task, file) {
    const bytes = new DataView(await task.wait(file.slice(0, 128 * 1024).arrayBuffer()));
    let orientation = 1, orientationOffset = null, little = false, width, height;
    if (bytes.byteLength >= 4 && bytes.getUint16(0) === 0xffd8) {
        let offset = 2;
        while (offset + 4 <= bytes.byteLength && bytes.getUint8(offset) === 0xff) {
            const marker = bytes.getUint8(offset + 1);
            if (marker === 0xda || marker === 0xd9) break;
            const size = bytes.getUint16(offset + 2);
            if (size < 2 || offset + size + 2 > bytes.byteLength) break;
            if ([0xc0, 0xc1, 0xc2, 0xc3, 0xc5, 0xc6, 0xc7, 0xc9, 0xca, 0xcb, 0xcd, 0xce, 0xcf].includes(marker) && size >= 7) {
                height = bytes.getUint16(offset + 5);
                width = bytes.getUint16(offset + 7);
            }
            if (marker === 0xe1 && size >= 16 && bytes.getUint32(offset + 4) === 0x45786966 && bytes.getUint16(offset + 8) === 0) {
                const tiff = offset + 10, end = offset + size + 2;
                little = bytes.getUint16(tiff) === 0x4949;
                if ((little || bytes.getUint16(tiff) === 0x4d4d) && bytes.getUint16(tiff + 2, little) === 42) {
                    const directory = tiff + bytes.getUint32(tiff + 4, little);
                    if (directory >= tiff + 8 && directory + 2 <= end) {
                        const count = bytes.getUint16(directory, little);
                        for (let i = 0; i < count; i++) {
                            const entry = directory + 2 + i * 12;
                            if (entry + 12 > end) break;
                            if (bytes.getUint16(entry, little) === 0x0112 && bytes.getUint16(entry + 2, little) === 3
                                && bytes.getUint32(entry + 4, little) === 1) {
                                const value = bytes.getUint16(entry + 8, little);
                                if (value >= 1 && value <= 8) { orientation = value; orientationOffset = entry + 8; }
                                break;
                            }
                        }
                    }
                }
            }
            offset += size + 2;
        }
    }
    const blob = orientationOffset === null ? file : new Blob([
        file.slice(0, orientationOffset), new Uint8Array(little ? [1, 0] : [0, 1]), file.slice(orientationOffset + 2)
    ], { type: file.type || "image/jpeg" });
    return { blob, orientation, width, height };
}

function ownCanvas(task, width, height) {
    const canvas = document.createElement("canvas");
    canvas.width = width;
    canvas.height = height;
    const release = task.own(() => { canvas.width = canvas.height = 0; });
    const context = canvas.getContext("2d");
    if (!context) throw new Error("Canvas unavailable");
    return { canvas, context, release };
}

function loadImage(task, blob) {
    if (typeof URL.createObjectURL !== "function" || typeof URL.revokeObjectURL !== "function")
        return Promise.reject(new Error("Local image loading unavailable"));
    const image = new Image();
    const url = URL.createObjectURL(blob);
    const release = task.own(() => {
        image.onload = image.onerror = null;
        image.removeAttribute("src");
        URL.revokeObjectURL(url);
    });
    return task.wait(new Promise((resolve, reject) => {
        image.onload = () => {
            image.onload = image.onerror = null;
            resolve({ image, width: image.naturalWidth, height: image.naturalHeight, release });
        };
        image.onerror = () => reject(new Error("Photo image could not be loaded"));
        image.src = url;
    }));
}

async function photoSource(task, metadata) {
    if (typeof createImageBitmap === "function") {
        try {
            const options = { imageOrientation: "from-image", resizeQuality: "high" };
            if (metadata.width && metadata.height) {
                const size = boundedSize(metadata.width, metadata.height, PHOTO_SOURCE_SIZE);
                options.resizeWidth = size.width;
                options.resizeHeight = size.height;
            }
            const pending = createImageBitmap(metadata.blob, options).then(image => {
                const release = task.own(() => image.close());
                return { image, width: image.width, height: image.height, release };
            });
            return await task.wait(pending);
        } catch (error) {
            if (task.disposed || error?.name === "AbortError") throw error;
            // Unsupported image format/options: use the local Image path.
        }
    }
    return loadImage(task, metadata.blob);
}

async function preparePhoto(task, file) {
    const metadata = await photoMetadata(task, file);
    const source = await photoSource(task, metadata);
    try {
        const size = boundedSize(source.width, source.height, PHOTO_SOURCE_SIZE);
        const swapped = metadata.orientation >= 5;
        const master = ownCanvas(task, swapped ? size.height : size.width, swapped ? size.width : size.height);
        const w = size.width, h = size.height;
        const transforms = [
            [1, 0, 0, 1, 0, 0], [-1, 0, 0, 1, w, 0], [-1, 0, 0, -1, w, h], [1, 0, 0, -1, 0, h],
            [0, 1, 1, 0, 0, 0], [0, 1, -1, 0, h, 0], [0, -1, -1, 0, h, w], [0, -1, 1, 0, 0, w]
        ];
        master.context.setTransform(...transforms[metadata.orientation - 1]);
        master.context.drawImage(source.image, 0, 0, w, h);
        return master;
    } finally { source.release(); }
}

async function decodePhoto(task, file) {
    const { decoder, target } = await loadDecoder(task);
    const master = await preparePhoto(task, file);
    // Whole image, broad center strip, closer center strip. Crops retain more
    // useful pixels per barcode without upscaling or speculative rotations.
    const candidates = [
        { width: 1, height: 1, limit: 1600 },
        { width: 0.9, height: 0.5, limit: PHOTO_DECODE_SIZE },
        { width: 0.65, height: 0.3, limit: PHOTO_DECODE_SIZE }
    ];
    for (const crop of candidates) {
        const sw = Math.max(1, Math.round(master.canvas.width * crop.width));
        const sh = Math.max(1, Math.round(master.canvas.height * crop.height));
        const size = boundedSize(sw, sh, crop.limit);
        const candidate = ownCanvas(task, size.width, size.height);
        try {
            candidate.context.drawImage(master.canvas, (master.canvas.width - sw) / 2,
                (master.canvas.height - sh) / 2, sw, sh, 0, 0, size.width, size.height);
            // PNG has no EXIF: Quagga cannot apply a second orientation.
            // Data URL stays in this discarded document; there is no HTTP/XHR.
            let src = candidate.canvas.toDataURL("image/png");
            const result = await task.wait(decoder.decodeSingle({
                src, inputStream: { target, size: 0 }, locate: true, numOfWorkers: 0,
                locator: { patchSize: "medium", halfSample: false },
                decoder: { readers: [...READERS], multiple: false },
                canvas: { createOverlay: false }
            }));
            src = null;
            clearMedia(target);
            const code = confirmedCode(result);
            if (code !== null) return code;
        } finally { candidate.release(); }
        // Give Safari an opportunity to paint/cancel between bounded attempts.
        await task.wait(new Promise(resolve => {
            const timer = setTimeout(resolve, 0);
            task.own(() => clearTimeout(timer));
        }));
    }
    return null;
}

async function analysePhoto(session, file) {
    if (!alive(session) || session.mode !== "photo" || session.analysing || session.transitioning || !file) return;
    session.analysing = true;
    photoState(session, "analyzing", "Analizando código…");
    const task = operation();
    session.photoOperation = task;
    const timeout = setTimeout(() => task.cancel(new Error("Photo analysis timed out")), 30000);
    task.own(() => clearTimeout(timeout));
    session.photoPromise = (async () => {
        try {
            const code = await decodePhoto(task, file);
            if (!alive(session)) return;
            if (code === null) photoState(session, "not-found", "No encontramos un código de barras en la foto.");
            else {
                photoState(session, "success", "Revisa el código antes de usarlo.", code);
                element(session, "use")?.focus?.();
            }
        } catch (error) {
            if (alive(session)) {
                // Do not log the File, data URL, decoded value or exception message.
                console.warn("Barcode photo analysis failed", error?.name ?? "Error");
                photoState(session, "technical-error", "No pudimos analizar la foto. Inténtalo de nuevo.");
            }
        } finally {
            task.dispose();
            if (session.photoOperation === task) session.photoOperation = null;
            session.analysing = false;
            file = null;
        }
    })();
    await session.photoPromise;
    session.photoPromise = null;
}

function release(session) {
    if (session.releasePromise) return session.releasePromise;
    session.cancelled = true;
    session.photoOperation?.cancel();
    stopCamera(session.live);
    session.pendingCode = null;
    element(session, "code").textContent = "";
    element(session, "result").hidden = true;
    element(session, "file").value = "";
    session.dialog.removeEventListener("cancel", session.onCancel);
    session.dialog.removeEventListener("close", session.onClose);
    document.removeEventListener("visibilitychange", session.onVisibilityChange);
    window.removeEventListener("pagehide", session.onPageHide);
    session.observer?.disconnect();
    for (const [control, name, listener] of session.listeners) control.removeEventListener(name, listener);
    session.listeners.length = 0;
    session.releasePromise = (async () => {
        try { await session.photoPromise; } catch { /* Discarded analysis. */ }
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
    catch { /* Blazor may have disconnected during the analysis. */ }
}

export async function abrir(dialog, viewId, reference) {
    if (active) return "busy";
    if (typeof dialog.showModal !== "function") return "unsupported";
    const session = {
        dialog, viewId, reference, origin: document.activeElement,
        cancelled: false, done: false, mode: "photo", live: null,
        analysing: false, transitioning: false, photoOperation: null,
        photoPromise: null, releasePromise: null, pendingCode: null, listeners: []
    };
    active = session;
    const listen = (name, event, callback) => {
        const control = element(session, name);
        if (!control) return;
        control.addEventListener(event, callback);
        session.listeners.push([control, event, callback]);
    };
    listen("capture", "click", () => {
        if (!alive(session) || session.mode !== "photo" || session.analysing || session.transitioning) return;
        const input = element(session, "file");
        input.value = "";
        input.click(); // Preserve the native iOS picker user gesture, without await/interop.
    });
    listen("file", "change", () => {
        const input = element(session, "file");
        const file = input.files?.[0];
        input.value = ""; // Selecting the same file again must still fire change.
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
    // The native iOS photo picker can temporarily hide the page.
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
        });
        session.observer.observe(document.body, { childList: true, subtree: true });
    }
    try {
        hideZoom({ dialog });
        photoState(session, "idle", "Puedes tomar una foto o elegir una imagen.");
        element(session, "file").value = "";
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
