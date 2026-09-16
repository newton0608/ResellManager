// Pruebas del controlador cliente, sin navegador ni dependencias adicionales.
const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const script = fs.readFileSync(path.resolve(__dirname, "../../src/ResellManager.Web/wwwroot/reconnect.js"), "utf8");

function fixture(reconnect = async () => true) {
    const classes = new Set(["components-reconnect-hide"]);
    const attributes = {};
    const focus = [];
    const listeners = {};
    const retry = { disabled: false, addEventListener: (name, callback) => listeners[`retry:${name}`] = callback };
    const reload = { addEventListener: (name, callback) => listeners[`reload:${name}`] = callback };
    const current = { innerText: "4" };
    const maximum = { innerText: "8" };
    let observer;
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
        document: { getElementById: id => ({
            "components-reconnect-modal": modal,
            "reconnect-retry": retry,
            "reconnect-reload": reload,
            "components-reconnect-current-attempt": current,
            "components-reconnect-max-retries": maximum,
        })[id] },
        window: {
            Blazor: { reconnect: () => { calls++; return reconnect(); } },
            location: { reload: () => reloads++ },
        },
        MutationObserver: class {
            constructor(callback) { observer = callback; observerCount++; }
            observe(target, options) { assert.equal(target, modal); observerOptions = options; }
        },
    };
    vm.runInNewContext(script, context);
    return {
        modal, retry, attributes, focus, current, maximum,
        state: value => { classes.clear(); classes.add(`components-reconnect-${value}`); observer(); },
        hasState: value => classes.has(`components-reconnect-${value}`),
        clickRetry: () => listeners["retry:click"](),
        clickReload: () => listeners["reload:click"](),
        cancel: event => listeners["modal:cancel"](event),
        initializeAgain: () => vm.runInNewContext(script, context),
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
    assert.equal(ui.observerCount, 1);
    ui.state("failed");
    await ui.clickRetry();
    assert.equal(ui.calls, 1);
});
