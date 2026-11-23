// Browser capture is opt-in through the expanded dock; musical mapping/validation belongs to C#.
export function mountPerformanceKeyboard(root, receiver, codes) {
    const recognized = new Set([...codes, 'KeyZ', 'KeyX']);
    const held = new Set(), pointers = new Map();
    let disposed = false, pending = 0, epoch = 0, chain = Promise.resolve();
    const active = () => !disposed && root.isConnected && root.dataset.armed === 'true' && !document.hidden;
    const excluded = target => target instanceof Element && target.closest('input, textarea, select, [contenteditable]:not([contenteditable="false"]), [role="textbox"], [role="slider"]') ||
        document.querySelector('[aria-modal="true"], [role="dialog"], .workspace-library-panel, .rack-editor, .connection-amount-editor') ||
        root.closest('.graph-workspace')?.dataset.mobileActivePanel;
    const send = (method, ...args) => {
        if (pending >= 64) { release(); return; }
        pending++;
        const current = epoch;
        chain = chain.then(() => active() && current === epoch ? receiver.invokeMethodAsync(method, ...args) : undefined)
            .catch(error => { if (!disposed) console.error('Performance input failed', error); }).finally(() => pending--);
    };
    const release = (notify = true) => {
        held.clear();
        for (const [id, { piano }] of pointers) {
            try { if (piano.hasPointerCapture(id)) piano.releasePointerCapture(id); } catch {}
        }
        pointers.clear();
        epoch++;
        // Clear/reset from C# already releases notes. Browser blur/cancel must
        // notify C#, even if the dock was hidden before a queued input ran.
        if (notify) chain = chain.then(() => !disposed && receiver.invokeMethodAsync('ReleasePhysicalKeys')).catch(() => {});
    };
    const down = event => {
        const stepKey = root.dataset.stepEntry === 'true' && ['Space', 'ArrowLeft', 'ArrowRight'].includes(event.code);
        // When armed, the performance piano owns its note keys even if the canvas is focused.
        // When collapsed, active() is false and canvas shortcuts remain available.
        if (!active() || excluded(event.target) || event.ctrlKey || event.altKey || event.metaKey || event.shiftKey ||
            (!recognized.has(event.code) && !stepKey)) return;
        event.preventDefault();
        event.stopPropagation();
        if (event.repeat || held.has(event.code)) return;
        held.add(event.code);
        send('PhysicalKey', event.code, true);
    };
    const up = event => {
        if (!held.delete(event.code)) return;
        send('PhysicalKey', event.code, false);
        if (active() && !excluded(event.target)) { event.preventDefault(); event.stopPropagation(); }
    };
    const pianoKey = target => {
        const key = target instanceof Element ? target.closest('button.performance-key[data-midi-note]') : null;
        return key && !key.disabled && root.contains(key) ? key : null;
    };
    const pointerDown = event => {
        if (!active() || event.button !== 0 || pointers.has(event.pointerId)) return;
        const key = pianoKey(event.target);
        if (!key) return;
        const piano = key.closest('.performance-piano');
        const note = Number(key.dataset.midiNote);
        pointers.set(event.pointerId, { piano, note });
        // Pointer capture keeps releases reliable outside the piano. Hit-test
        // the actual screen position, including the overlapping black keys.
        try { piano.setPointerCapture(event.pointerId); } catch {}
        event.preventDefault();
        send('PianoPointer', event.pointerId, note);
    };
    const pointerMove = event => {
        const pointer = pointers.get(event.pointerId);
        if (!pointer || !active() || root.dataset.stepEntry === 'true') return;
        const key = pianoKey(document.elementFromPoint(event.clientX, event.clientY));
        const note = key && pointer.piano.contains(key) ? Number(key.dataset.midiNote) : null;
        if (note === pointer.note) return;
        pointer.note = note;
        send('PianoPointer', event.pointerId, note);
    };
    const pointerEnd = event => {
        if (!pointers.delete(event.pointerId)) return;
        send('PianoPointer', event.pointerId, null);
    };
    const onBlur = () => release();
    const onVisibility = () => { if (document.hidden) release(); };
    window.addEventListener('keydown', down, true);
    window.addEventListener('keyup', up, true);
    window.addEventListener('blur', onBlur);
    document.addEventListener('visibilitychange', onVisibility);
    root.addEventListener('pointerdown', pointerDown, true);
    window.addEventListener('pointermove', pointerMove, true);
    window.addEventListener('pointerup', pointerEnd, true);
    window.addEventListener('pointercancel', pointerEnd, true);
    root.addEventListener('lostpointercapture', pointerEnd, true);
    return { clear: () => release(false), focus: () => root.focus({ preventScroll: true }), dispose() {
        disposed = true; release(false);
        window.removeEventListener('keydown', down, true);
        window.removeEventListener('keyup', up, true);
        window.removeEventListener('blur', onBlur);
        document.removeEventListener('visibilitychange', onVisibility);
        root.removeEventListener('pointerdown', pointerDown, true);
        window.removeEventListener('pointermove', pointerMove, true);
        window.removeEventListener('pointerup', pointerEnd, true);
        window.removeEventListener('pointercancel', pointerEnd, true);
        root.removeEventListener('lostpointercapture', pointerEnd, true);
    } };
}
