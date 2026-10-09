// Pruebas del controlador cliente, sin navegador ni dependencias adicionales.
const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const script = fs.readFileSync(path.resolve(__dirname, "../../src/ResellManager.Web/wwwroot/reconnect.js"), "utf8");

function fixture(reconnect = async () => true, publicCatalog = false, loading = false) {
    const classes = new Set(["components-reconnect-hide"]);
    const attributes = {};
    const focus = [];
    const listeners = {};
    const retry = { disabled: false, addEventListener: (name, callback) => listeners[`retry:${name}`] = callback };
    const reload = { addEventListener: (name, callback) => listeners[`reload:${name}`] = callback };
    const notice = { hidden: true, dataset: {} };
    const noticeMessage = { textContent: '' };
    const noticeRetry = { hidden: true, disabled: false, addEventListener: (name, callback) => listeners[`noticeRetry:${name}`] = callback };
    const noticeReload = { hidden: true, addEventListener: (name, callback) => listeners[`noticeReload:${name}`] = callback };
    const current = { innerText: "4" };
    const maximum = { innerText: "8" };
    let observer;
    let layoutObserver;
    const body = {};
    const documentListeners = {};
    const windowListeners = {};
    const blazorListeners = {};
    let layoutObserverOptions;
    let observerOptions;
    let observerCount = 0;
    let calls = 0;
    let reloads = 0;
    const modal = {
        open: false,
        dataset: {},
        classList: {
            contains: value => classes.has(value),
            remove: (...values) => values.forEach(value => classes.delete(value)),
            add: value => classes.add(value),
        },
        setAttribute: (name, value) => attributes[name] = value,
        showModal() { this.open = true; },
        close() { this.open = false; },
        querySelector: selector => ({ focus: () => focus.push(selector) }),
        addEventListener: (name, callback) => listeners[`modal:${name}`] = callback,
    };
    const context = {
        document: { body, readyState: loading ? "loading" : "complete", addEventListener: (name, callback) => documentListeners[name] = callback, querySelector: () => publicCatalog ? {} : null, getElementById: id => ({
            "components-reconnect-modal": modal,
            "store-reconnect": notice,
            "store-reconnect-message": noticeMessage,
            "store-reconnect-retry": noticeRetry,
            "store-reconnect-reload": noticeReload,
            "reconnect-retry": retry,
            "reconnect-reload": reload,
            "components-reconnect-current-attempt": current,
            "components-reconnect-max-retries": maximum,
        })[id] },
        window: {
            Blazor: loading ? undefined : { reconnect: () => { calls++; return reconnect(); }, addEventListener: (name, callback) => blazorListeners[name] = callback },
            addEventListener: (name, callback) => windowListeners[name] = callback,
            location: { reload: () => reloads++ },
        },
        MutationObserver: class {
            constructor(callback) { this.callback = callback; observerCount++; }
            observe(target, options) {
                if (target === modal) { observer = this.callback; observerOptions = options; }
                else { assert.equal(target, body); layoutObserver = this.callback; layoutObserverOptions = options; }
            }
        },
    };
    vm.runInNewContext(script, context);
    return {
        modal, retry, attributes, focus, current, maximum, notice, noticeMessage, noticeRetry, noticeReload,
        state: value => { classes.clear(); classes.add(`components-reconnect-${value}`); observer(); },
        hasState: value => classes.has(`components-reconnect-${value}`),
        clickNoticeRetry: () => listeners["noticeRetry:click"](),
        clickNoticeReload: () => listeners["noticeReload:click"](),
        clickRetry: () => listeners["retry:click"](),
        clickReload: () => listeners["reload:click"](),
        cancel: event => listeners["modal:cancel"](event),
        initializeAgain: () => vm.runInNewContext(script, context),
        layout: (value, notify = true) => { publicCatalog = value; if (notify) layoutObserver(); },
        pageShow: () => windowListeners.pageshow(),
        popState: () => windowListeners.popstate(),
        enhancedLoad: () => blazorListeners.enhancedload(),
        loadFramework: () => {
            context.window.Blazor = { reconnect: () => { calls++; return reconnect(); }, addEventListener: (name, callback) => blazorListeners[name] = callback };
            documentListeners.DOMContentLoaded();
        },
        get enhancedLoadRegistered() { return typeof blazorListeners.enhancedload === "function"; },
        get layoutObserverOptions() { return layoutObserverOptions; },
        get documentListeners() { return documentListeners; },
        get calls() { return calls; },
        get reloads() { return reloads; },
        get observerOptions() { return observerOptions; },
        get observerCount() { return observerCount; },
    };
}

test("las clases oficiales abren/cierran el diálogo, actualizan accesibilidad y conservan contadores", () => {
    const ui = fixture();
    assert.equal(ui.modal.open, false);
    assert.equal(ui.attributes["aria-busy"], "false");
    assert.deepEqual(Array.from(ui.observerOptions.attributeFilter), ["class"]);
    for (const state of ["show", "failed", "rejected"]) {
        ui.state(state);
        assert.equal(ui.modal.open, true);
        assert.equal(ui.attributes["aria-busy"], String(state === "show"));
        assert.equal(ui.attributes["aria-labelledby"], `reconnect-title-${state}`);
        assert.equal(ui.attributes["aria-describedby"], `reconnect-description-${state}`);
        assert.equal(ui.focus.at(-1), `.reconnect-state-${state}`);
    }
    ui.state("hide");
    assert.equal(ui.modal.open, false);
    assert.equal(ui.current.innerText, "4");
    assert.equal(ui.maximum.innerText, "8");
    assert.equal(ui.calls, 0);
    assert.equal(ui.reloads, 0);
});

test("Reintentar usa Blazor.reconnect y cierra tras éxito sin recargar", async () => {
    const ui = fixture(async () => true);
    ui.state("failed");
    await ui.clickRetry();
    assert.equal(ui.calls, 1);
    assert.equal(ui.hasState("hide"), true);
    assert.equal(ui.modal.open, false);
    assert.equal(ui.reloads, 0);
});

test("rechazo del circuito muestra rejected sin recarga automática", async () => {
    const ui = fixture(async () => false);
    ui.state("failed");
    await ui.clickRetry();
    assert.equal(ui.hasState("rejected"), true);
    assert.equal(ui.modal.open, true);
    assert.equal(ui.reloads, 0);
});

test("error de red vuelve a failed y permite otro reintento", async () => {
    const ui = fixture(async () => { throw new Error("network"); });
    ui.state("failed");
    await ui.clickRetry();
    assert.equal(ui.hasState("failed"), true);
    assert.equal(ui.retry.disabled, false);
    assert.equal(ui.modal.dataset.manualRetry, undefined);
    await ui.clickRetry();
    assert.equal(ui.calls, 2);
    assert.equal(ui.reloads, 0);
});

test("doble clic no duplica la reconexión ni inventa contadores para el intento manual", async () => {
    let complete;
    const ui = fixture(() => new Promise(resolve => { complete = resolve; }));
    ui.state("failed");
    const first = ui.clickRetry();
    assert.equal(ui.retry.disabled, true);
    assert.equal(ui.hasState("show"), true);
    assert.equal(ui.modal.dataset.manualRetry, "true");
    await ui.clickRetry();
    assert.equal(ui.calls, 1);
    assert.equal(ui.current.innerText, "4");
    assert.equal(ui.maximum.innerText, "8");
    complete(true);
    await first;
    assert.equal(ui.retry.disabled, false);
    assert.equal(ui.modal.dataset.manualRetry, undefined);
});

test("solo el botón Recargar página ejecuta location.reload", () => {
    const ui = fixture();
    ui.state("rejected");
    assert.equal(ui.reloads, 0);
    ui.clickReload();
    assert.equal(ui.reloads, 1);
    assert.equal(ui.calls, 0);
});

test("Escape no permite descartar la protección de desconexión", () => {
    const ui = fixture();
    ui.state("show");
    let prevented = false;
    ui.cancel({ preventDefault: () => prevented = true });
    assert.equal(prevented, true);
    assert.equal(ui.modal.open, true);
});

test("la navegación mejorada no instala otro observador sobre el modal permanente", async () => {
    const ui = fixture();
    ui.initializeAgain();
    assert.equal(ui.observerCount, 2);
    ui.state("failed");
    await ui.clickRetry();
    assert.equal(ui.calls, 1);
});

test("layout público usa aviso sin modal/foco y desaparece solo al reconectar", () => {
    const ui = fixture(async () => true, true);
    ui.state("show");
    assert.equal(ui.modal.open, false);
    assert.equal(ui.notice.hidden, false);
    assert.equal(ui.noticeMessage.textContent, "Reconectando…");
    assert.equal(ui.focus.length, 0);
    ui.state("hide");
    assert.equal(ui.notice.hidden, true);
});

test("fallo/rechazo público conserva acciones nativas sin circuito", async () => {
    const ui = fixture(async () => false, true);
    ui.state("failed");
    assert.equal(ui.noticeRetry.hidden, false);
    await ui.clickNoticeRetry();
    assert.equal(ui.hasState("rejected"), true);
    assert.equal(ui.notice.hidden, false);
    assert.equal(ui.noticeReload.hidden, false);
    assert.equal(ui.modal.open, false);
    ui.clickNoticeReload();
    assert.equal(ui.reloads, 1);
});

test("el cambio de marca de layout actualiza la presentación aunque Blazor conserve el estado", () => {
    const ui = fixture();
    ui.state("show");
    assert.equal(ui.modal.open, true);
    ui.layout(true);
    assert.equal(ui.modal.open, false);
    assert.equal(ui.notice.hidden, false);
    ui.layout(false);
    assert.equal(ui.modal.open, true);
    assert.equal(ui.notice.hidden, true);
    assert.equal(ui.layoutObserverOptions.subtree, true);
    assert.equal(ui.layoutObserverOptions.childList, true);
    assert.deepEqual(Array.from(ui.layoutObserverOptions.attributeFilter), ["data-public-catalog"]);
});

test("enhancedload se suscribe a Blazor y reevalúa el layout permanente", () => {
    const ui = fixture();
    assert.equal(ui.enhancedLoadRegistered, true);
    assert.equal(ui.documentListeners.enhancedload, undefined);
    ui.state("failed");
    ui.layout(true, false);
    ui.enhancedLoad();
    assert.equal(ui.modal.open, false);
    assert.equal(ui.noticeRetry.hidden, false);
});

test("el script anterior al framework registra enhancedload tras DOMContentLoaded", () => {
    const ui = fixture(async () => true, true, true);
    assert.equal(ui.enhancedLoadRegistered, false);
    ui.loadFramework();
    assert.equal(ui.enhancedLoadRegistered, true);
    ui.state("show");
    ui.enhancedLoad();
    assert.equal(ui.modal.open, false);
    assert.equal(ui.notice.hidden, false);
});

test("pageshow y popstate reevalúan el layout al restaurar atrás/adelante o bfcache", () => {
    const ui = fixture();
    ui.state("rejected");
    ui.layout(true, false);
    ui.pageShow();
    assert.equal(ui.modal.open, false);
    assert.equal(ui.noticeReload.hidden, false);
    ui.layout(false, false);
    ui.popState();
    assert.equal(ui.modal.open, true);
    assert.equal(ui.notice.hidden, true);
});

test("mutaciones de contenido no vuelven a enfocar el modal de administración", () => {
    const ui = fixture();
    ui.state("show");
    const focused = ui.focus.length;
    ui.layout(false);
    ui.layout(false);
    assert.equal(ui.focus.length, focused);
});

test("fallo de reintento público mantiene aviso y permite recuperar sin recargar", async () => {
    const ui = fixture(async () => { throw new Error("offline"); }, true);
    ui.state("failed");
    await ui.clickNoticeRetry();
    assert.equal(ui.hasState("failed"), true);
    assert.equal(ui.notice.hidden, false);
    assert.equal(ui.noticeRetry.hidden, false);
    assert.equal(ui.noticeRetry.disabled, false);
    assert.equal(ui.modal.open, false);
    assert.equal(ui.focus.length, 0);
    assert.equal(ui.reloads, 0);
});
