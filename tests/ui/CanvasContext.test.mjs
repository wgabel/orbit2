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
    const slider = make('connection', true);
    const closest = slider.closest.bind(slider);
    slider.closest = selector => selector === '.connection-amount-slider' ? slider : closest(selector);
    assert.equal(contextTarget(viewport, slider), null);
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
        assert.deepEqual(calls.slice(1).map(call => call.method), ['BeginUniversalPinchAsync', 'EndUniversalPinchAsync', 'EndUniversalPinchAsync']);
        assert.ok(emit('contextmenu', { pointerType: 'mouse', button: 2 }).prevented);
        // A fresh mouse down permits a new right-click after the pinch.
        emit('pointerdown', { pointerType: 'mouse', button: 2 });
        assert.ok(emit('contextmenu', { pointerType: 'mouse', button: 2 }).prevented);
        assert.equal(calls.length, 5);
        assert.equal(calls.at(-1).request.touch, false);
    } finally {
        controller.dispose();
        globalThis.matchMedia = orig;
    }
    assert.equal(listeners.size, 0);
});

test('two fingers starting on interactive controls take over, cancel the range and suppress native edits', () => {
    const { viewport, receiver, emit, calls, make } = fixture();
    const savedPointer = globalThis.PointerEvent;
    globalThis.PointerEvent = class { constructor(type, options) { Object.assign(this, options, { type }); } };
    const range = make('node', true);
    range.value = '0.4';
    const closest = range.closest.bind(range);
    range.closest = selector => selector === 'input[type="range"]' ? range : closest(selector);
    let canceled = 0;
    range.dispatchEvent = event => { assert.equal(event.type, 'pointercancel'); canceled++; };
    const controller = mountContextInput(viewport, receiver);
    try {
        assert.equal(emit('pointerdown', { target: range }).stopped, undefined);
        range.value = '0.8';
        const takeover = emit('pointerdown', { pointerId: 2, target: make('node', true), clientX: 100 });
        assert.ok(takeover.stopped);
        assert.equal(calls[0].method, 'BeginUniversalPinchAsync');
        assert.equal(calls[0].request.slider, true);
        assert.equal(range.value, '0.4');
        assert.equal(canceled, 1);
        range.value = '0.9';
        assert.ok(emit('input', { target: range }).stopped);
        assert.equal(range.value, '0.4');
        emit('pointermove', { pointerId: 1, clientX: 60 });
        emit('pointerup', { pointerId: 2 });
        emit('pointermove', { pointerId: 1, clientX: 70 });
        emit('pointerup');
        assert.deepEqual(calls.map(call => call.method), ['BeginUniversalPinchAsync', 'MoveUniversalPinchAsync', 'EndUniversalPinchAsync', 'MoveUniversalPinchAsync', 'EndUniversalPinchAsync']);
        assert.ok(emit('click').stopped);
        assert.equal(emit('pointerdown', { target: range }).stopped, undefined);
        assert.equal(emit('input', { target: range }).stopped, undefined);
        emit('pointerup');
    } finally { controller.dispose(); globalThis.PointerEvent = savedPointer; }
});

test('connection exact input is not captured as a mouse range rail', () => {
    const { viewport, receiver, emit, make } = fixture();
    const input = make('node', true);
    let captures = 0;
    input.setPointerCapture = () => captures++;
    const closest = input.closest.bind(input);
    input.closest = selector => selector === '.connection-amount-slider input' ? input : closest(selector);
    const controller = mountContextInput(viewport, receiver);
    try {
        emit('pointerdown', { target: input, pointerType: 'mouse', isPrimary: true });
        assert.equal(captures, 0);
    } finally { controller.dispose(); }
});
