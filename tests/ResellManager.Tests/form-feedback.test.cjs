const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(path.resolve(__dirname, "../../src/ResellManager.Web/wwwroot/form-feedback.js"), "utf8");

function fixture(reduced = false) {
    const listeners = {}, frames = [], errors = [];
    let mutation, modal = null;
    const document = {
        body: {}, addEventListener: (name, callback) => listeners[name] = callback,
        querySelector: () => modal, querySelectorAll: () => errors
    };
    vm.runInNewContext(source, { document, window: { innerHeight: 844, matchMedia: () => ({ matches: reduced }) },
        requestAnimationFrame: fn => frames.push(fn),
        MutationObserver: class { constructor(fn) { mutation = fn; } observe() {} }
    });
    function flush() { while (frames.length) frames.shift()(); }
    function error(text = "No se pudo guardar", top = 1000) {
        const item = { textContent: text, visible: true, top, scrolls: [], focuses: 0, attrs: {},
            getClientRects() { return this.visible ? [1] : []; },
            getBoundingClientRect() { return { top: this.top, bottom: this.top + 40 }; },
            scrollIntoView(options) { this.scrolls.push(options); },
            focus(options) { assert.equal(options.preventScroll, true); this.focuses++; },
            setAttribute(name, value) { this.attrs[name] = value; }
        };
        errors.push(item); return item;
    }
    return { listeners, flush, error, mutate: () => { mutation(); flush(); }, modal: value => modal = value };
}
test("selecciona el valor numérico solo en el primer foco y permite editar después", () => {
    const f = fixture();
    const input = { value: "0.00", selections: 0, matches: () => true, select() { this.selections++; } };
    f.listeners.focusin({ target: input });
    let prevented = false;
    f.listeners.mouseup({ target: input, preventDefault: () => prevented = true });
    assert.equal(prevented, true);
    input.value = "123";
    f.listeners.focusout();
    f.listeners.focusin({ target: input });
    prevented = false;
    f.listeners.mouseup({ target: input, preventDefault: () => prevented = true });
    assert.equal(input.selections, 1);
    assert.equal(prevented, false);
});
test("no selecciona un campo vacío ni controles ajenos", () => {
    const f = fixture();
    const input = { value: "", matches: () => true, select() { assert.fail("No seleccionar"); } };
    f.listeners.focusin({ target: input });
    f.listeners.focusin({ target: { value: "texto", matches: () => false } });
});
test("error nuevo fuera del viewport desplaza y recibe foco una sola vez", () => {
    const f = fixture(), error = f.error();
    f.flush(); f.mutate(); f.mutate();
    assert.equal(error.scrolls.length, 1);
    assert.equal(error.scrolls[0].behavior, "smooth");
    assert.equal(error.focuses, 1);
    assert.equal(error.attrs.tabindex, "-1");
    error.textContent = "Otro error"; f.mutate();
    assert.equal(error.scrolls.length, 2);
});
test("error ya visible no desplaza ni roba foco", () => {
    const f = fixture(), error = f.error("Error", 200);
    f.flush(); f.mutate();
    assert.equal(error.scrolls.length, 0);
    assert.equal(error.focuses, 0);
});
test("respeta movimiento reducido y permite un nuevo intento tras limpiar error", () => {
    const f = fixture(true), error = f.error();
    f.flush();
    assert.equal(error.scrolls[0].behavior, "instant");
    error.textContent = ""; f.mutate();
    error.textContent = "No se pudo guardar"; f.mutate();
    assert.equal(error.scrolls.length, 2);
});
test("ignora errores ocultos y prioriza el diálogo abierto", () => {
    const f = fixture(), background = f.error(), modalError = f.error("Error del diálogo");
    f.modal({ contains: item => item === modalError });
    f.flush();
    assert.equal(background.scrolls.length, 0);
    assert.equal(modalError.scrolls.length, 1);
    f.modal(null); background.visible = false; f.mutate();
    assert.equal(background.scrolls.length, 0);
});
