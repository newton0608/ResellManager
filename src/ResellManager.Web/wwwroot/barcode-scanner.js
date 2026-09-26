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

function closeDialog(session) {
    if (session.dialog.open) session.dialog.close();
    if (session.origin?.isConnected) session.origin.focus();
}

function releaseTracks(session) {
    session.dialog.querySelectorAll("video").forEach(video => {
        video.srcObject?.getTracks?.().forEach(track => track.stop());
        video.srcObject = null;
    });
}

function release(session) {
    if (session.stopPromise) return session.stopPromise;
    session.cancelled = true;
    clearTimeout(session.timeout);
    session.dialog.removeEventListener("cancel", session.onCancel);
    document.removeEventListener("visibilitychange", session.onVisibilityChange);
    window.removeEventListener("pagehide", session.onPageHide);
    session.observer?.disconnect();
    session.stopPromise = (async () => {
        try { await session.startPromise; } catch { /* Error de inicio ya se comunica arriba. */ }
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
        dialog,
        reference,
        origin: document.activeElement,
        cancelled: false,
        done: false,
        scanner: null,
        startPromise: null,
        stopPromise: null,
        timeout: null
    };
    session.onCancel = event => {
        event.preventDefault();
        void reference.invokeMethodAsync("CancelarDesdeTeclado").catch(() => {});
    };
    session.onVisibilityChange = () => {
        if (document.hidden) void finish(session, "background", null);
    };
    session.onPageHide = () => { void finish(session, "background", null); };
    active = session;
    dialog.addEventListener("cancel", session.onCancel);
    document.addEventListener("visibilitychange", session.onVisibilityChange);
    window.addEventListener("pagehide", session.onPageHide);
    if (typeof MutationObserver !== "undefined") {
        session.observer = new MutationObserver(() => {
            if (!dialog.isConnected) {
                session.cancelled = true;
                void release(session);
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
            formatsToSupport: [
                formats.EAN_13, formats.EAN_8, formats.UPC_A,
                formats.UPC_E, formats.CODE_128
            ],
            // ZXing funciona en Safari/iOS sin depender de BarcodeDetector.
            useBarCodeDetectorIfSupported: false
        });
        session.startPromise = session.scanner.start(
            { facingMode: "environment" },
            { fps: 10, disableFlip: true },
            text => { if (text) void finish(session, "detected", text); },
            () => { /* Sin coincidencia en este fotograma; continúa buscando. */ }
        );
        await session.startPromise;
        if (session.done) return "started";
        if (session.cancelled) {
            await release(session);
            return "cancelled";
        }
        session.timeout = setTimeout(() => void finish(session, "timeout", null), 45000);
        return "started";
    } catch (error) {
        const reason = classify(error);
        await release(session);
        closeDialog(session);
        return reason;
    }
}

export function cerrar(dialog) {
    const session = active;
    if (!session || session.dialog !== dialog) return;
    session.cancelled = true;
    closeDialog(session);
    void release(session);
}
