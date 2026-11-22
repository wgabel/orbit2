import test from 'node:test';
import assert from 'node:assert/strict';
import { contextTarget, mountContextInput, HOLD_MS } from '../../src/Orbit.UI/wwwroot/js/canvasContext.js';

function fixture() {
    const listeners = new Map(), calls = [];
    const eventSource = { addEventListener(name, fn) { listeners.set(name, fn); }, removeEventListener(name) { listeners.delete(name); } };
    const viewport = { ...eventSource, ownerDocument: eventSource, contains: element => element !== 'outside', querySelector: () => null,
        getBoundingClientRect: () => ({ left: 10, top: 20 }) };
    const make = (kind, interactive = false) => ({ closest(selector) {
        if (selector === '.canvas-world') return {};
        if (selector === `[data-${kind}-id]`) return { dataset: { [`${kind}Id`]: 'target' } };
        if (interactive && selector.startsWith('button,')) return {};
        return null;
    } });
    const element = make('node');
    const emit = (name, values = {}) => {
        const event = { target: element, pointerType: 'touch', pointerId: 1, button: 0, detail: 1, clientX: 50, clientY: 60,
            preventDefault() { this.prevented = true; }, stopImmediatePropagation() { this.stopped = true; }, ...values };
        listeners.get(name)?.(event);
        return event;
    };
    const receiver = { async invokeMethodAsync(method, request) { calls.push({ method, request }); } };
    return { viewport, make, emit, receiver, calls, listeners };
}

test('context hits preserve interactive controls and distinguish cables, nodes and canvas', () => {
    const { viewport, make } = fixture();
    assert.deepEqual(contextTarget(viewport, make('node')), { kind: 'node', id: 'target' });
    assert.equal(contextTarget(viewport, make('node', true)), null);
    assert.deepEqual(contextTarget(viewport, make('connection', true)), { kind: 'connection', id: 'target' });
    assert.deepEqual(contextTarget(viewport, viewport), { kind: 'canvas', id: null });
    assert.equal(contextTarget(viewport, 'outside'), null);
});

test('long press opens once and consumes its release; drag, quick tap and second finger cancel', async () => {
    const orig = globalThis.matchMedia;
    globalThis.matchMedia = () => ({ matches: true });
    const { viewport, receiver, emit, calls, listeners } = fixture();
    const controller = mountContextInput(viewport, receiver);
    const wait = () => new Promise(resolve => setTimeout(resolve, HOLD_MS + 30));
    try {
        emit('pointerdown');
        assert.ok(emit('pointermove', { clientX: 53 }).stopped);
        await wait();
        assert.deepEqual(calls, [{ method: 'OpenContextAsync', request: { kind: 'node', id: 'target', x: 40, y: 40, touch: true, mobile: true } }]);
        assert.ok(emit('contextmenu').prevented);
        assert.ok(emit('pointerup').stopped);
        assert.ok(emit('click').prevented);
        emit('pointerdown'); emit('pointerup');
        emit('pointerdown'); emit('pointermove', { clientX: 70 }); emit('pointerup');
        emit('pointerdown'); emit('pointerdown', { pointerId: 2 }); emit('pointerup', { pointerId: 2 }); emit('pointerup');
        await wait();
        assert.equal(calls.length, 1);
        assert.ok(emit('contextmenu', { pointerType: 'mouse', button: 2 }).prevented);
        assert.equal(calls.length, 2);
        assert.equal(calls[1].request.touch, false);
    } finally {
        controller.dispose();
        globalThis.matchMedia = orig;
    }
    assert.equal(listeners.size, 0);
});
