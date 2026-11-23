import test from 'node:test';
import assert from 'node:assert/strict';
import { readPinned, writePinned } from '../../src/Orbit.UI/wwwroot/js/nodePresentation.js';

function withStorage(action) {
    const original = globalThis.localStorage;
    const entries = new Map();
    globalThis.localStorage = {
        getItem(key) { return entries.get(key) ?? null; },
        setItem(key, value) { entries.set(key, value); }
    };
    try { action(entries); }
    finally { globalThis.localStorage = original; }
}

test('node pins persist only in the selected patch and graph', () => withStorage(() => {
    writePinned('patch-a', 'graph-one', ['node-1', 'node-2']);
    writePinned('patch-a', 'graph-two', ['node-3']);
    assert.deepEqual(readPinned('patch-a', 'graph-one'), ['node-1', 'node-2']);
    assert.deepEqual(readPinned('patch-a', 'graph-two'), ['node-3']);
    assert.deepEqual(readPinned('patch-b', 'graph-one'), []);
}));

test('bad or obsolete pin preference payloads do not break canvas initialization', () => withStorage(store => {
    store.set('orbit2.node-pins.v1.patch-a.graph-one', '{"bad":"type"}');
    assert.deepEqual(readPinned('patch-a', 'graph-one'), []);
    store.set('orbit2.node-pins.v1.patch-a.graph-one', '["a",1,null,"b"]');
    assert.deepEqual(readPinned('patch-a', 'graph-one'), ['a', 'b']);
}));

test('storage restrictions cause no graph mutation or unhandled error', () => {
    const original = globalThis.localStorage;
    const warning = console.warn;
    globalThis.localStorage = {
        getItem() { throw new Error('storage prohibited'); },
        setItem() { throw new Error('storage prohibited'); }
    };
    console.warn = () => {};
    try {
        assert.deepEqual(readPinned('p', 'g'), []);
        assert.doesNotThrow(() => writePinned('p', 'g', ['n']));
    } finally {
        globalThis.localStorage = original;
        console.warn = warning;
    }
});
