const { test } = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");
const source = fs.readFileSync(path.resolve(__dirname, "../../src/ResellManager.Web/wwwroot/operation-review.js"), "utf8").replaceAll("export function", "function");

function fixture() {
    let restores = 0, focuses = 0, edits = 0, opens = 0;
    const listeners = {};
    const dialog = {
        open: false, dataset: { ocupado: "false" },
        addEventListener: (event, callback) => listeners[event] = callback,
        removeEventListener: event => delete listeners[event],
        showModal() { this.open = true; opens++; },
        close() { this.open = false; },
        querySelector: () => ({ focus: () => focuses++ })
    };
    const context = vm.createContext({ document: { activeElement: { isConnected: true, focus: () => restores++ } } });
    vm.runInContext(source, context);
    const reference = { invokeMethodAsync: name => { assert.equal(name, "CancelarDesdeTeclado"); edits++; } };
    return { context, dialog, reference, listeners, counts: () => ({ restores, focuses, edits, opens }) };
}

test("modal nativo se abre una sola vez y prioriza Editar", () => {
    const f = fixture();
    f.context.abrir(f.dialog, f.reference);
    f.context.abrir(f.dialog, f.reference);
    assert.equal(f.dialog.open, true);
    assert.equal(f.counts().opens, 1);
    assert.equal(f.counts().focuses, 1);
});
test("Escape cancela solo si no está ocupado", () => {
    const f = fixture(); f.context.abrir(f.dialog, f.reference);
    let prevented = 0;
    f.dialog.dataset.ocupado = "true";
    f.listeners.cancel({ preventDefault: () => prevented++ });
    assert.equal(f.counts().edits, 0);
    f.dialog.dataset.ocupado = "false";
    f.listeners.cancel({ preventDefault: () => prevented++ });
    assert.equal(f.counts().edits, 1);
    assert.equal(prevented, 2);
});
test("cerrar libera listeners y devuelve foco al control de origen", () => {
    const f = fixture(); f.context.abrir(f.dialog, f.reference);
    f.context.cerrar(f.dialog); f.context.cerrar(f.dialog);
    assert.equal(f.dialog.open, false);
    assert.equal(f.counts().restores, 1);
    assert.equal(f.listeners.cancel, undefined);
});
