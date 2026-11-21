// Browser capture is opt-in; all musical mapping/validation belongs to C#.
export function mountPerformanceKeyboard(root, receiver, codes) {
    const recognized = new Set([...codes, 'KeyZ', 'KeyX']);
    const held = new Set();
    let disposed = false, pending = 0, epoch = 0, chain = Promise.resolve();
    const active = () => !disposed && root.isConnected && root.dataset.armed === 'true' && !document.hidden;
    const excluded = target => target instanceof Element && target.closest('input, textarea, select, [contenteditable]:not([contenteditable="false"]), [role="textbox"], [role="slider"]') ||
        document.querySelector('[aria-modal="true"], [role="dialog"], .workspace-library-panel, .rack-editor, .connection-amount-editor');
    const send = (code, down) => {
        if (pending >= 64) return;
        pending++;
        const current = epoch;
        chain = chain.then(() => active() && current === epoch ? receiver.invokeMethodAsync('PhysicalKey', code, down) : undefined)
            .catch(error => { if (!disposed) console.error('Performance input failed', error); }).finally(() => pending--);
    };
    const release = () => {
        held.clear();
        epoch++;
        // Release is deliberately never a pitch-reset or a new note.
        chain = chain.then(() => !disposed && receiver.invokeMethodAsync('ReleasePhysicalKeys')).catch(() => {});
    };
    const down = event => {
        const stepKey = root.dataset.stepEntry === 'true' && ['Space', 'ArrowLeft', 'ArrowRight'].includes(event.code);
        if (!active() || excluded(event.target) || event.ctrlKey || event.altKey || event.metaKey || event.shiftKey ||
            (!recognized.has(event.code) && !stepKey)) return;
        event.preventDefault();
        event.stopPropagation();
        if (event.repeat || held.has(event.code)) return;
        held.add(event.code);
        send(event.code, true);
    };
    const up = event => {
        if (!held.delete(event.code)) return;
        send(event.code, false);
        if (active() && !excluded(event.target)) { event.preventDefault(); event.stopPropagation(); }
    };
    const onVisibility = () => { if (document.hidden) release(); };
    window.addEventListener('keydown', down, true);
    window.addEventListener('keyup', up, true);
    window.addEventListener('blur', release);
    document.addEventListener('visibilitychange', onVisibility);
    return { clear: release, focus: () => root.focus(), dispose() {
        disposed = true; held.clear();
        window.removeEventListener('keydown', down, true); window.removeEventListener('keyup', up, true);
        window.removeEventListener('blur', release); document.removeEventListener('visibilitychange', onVisibility);
    } };
}
