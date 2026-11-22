// Context input only: the canvas owns selection, commands and graph state.
export const HOLD_MS = 450;
export const HOLD_SLOP = 8;

export function contextTarget(viewport, element) {
    if (!element || !viewport.contains(element)) return null;
    if (element === viewport || element.closest('.canvas-empty')) return { kind: 'canvas', id: null };
    const world = element.closest('.canvas-world');
    if (!world) return null;
    const cable = element.closest('[data-connection-id]');
    if (cable) return { kind: 'connection', id: cable.dataset.connectionId };
    const marker = element.closest('.annotation-marker');
    if (!marker && element.closest('button, input, select, textarea, label, a, [contenteditable], [role="button"], canvas, .node-parameters, .node-visualization-panel, .note-body')) return null;
    for (const kind of ['node', 'note', 'group']) {
        const target = element.closest(`[data-${kind}-id]`);
        if (target) {
            // A group's transparent body is not a context target.
            if (kind === 'group' && !marker && !element.closest('.visual-group-header')) return null;
            return { kind, id: target.dataset[`${kind}Id`] };
        }
    }
    return { kind: 'canvas', id: null };
}

export function mountContextInput(viewport, receiver) {
    const doc = viewport.ownerDocument;
    const active = new Set();
    let hold = null, swallowed = null, suppressClick = false;
    const cancel = () => {
        if (hold) clearTimeout(hold.timer);
        hold = null;
    };
    const open = (target, x, y, touch) => {
        const rect = viewport.getBoundingClientRect();
        receiver.invokeMethodAsync('OpenContextAsync', { ...target, x: x - rect.left, y: y - rect.top, touch,
            mobile: matchMedia('(max-width: 768px), (max-height: 480px) and (pointer: coarse)').matches }).catch(() => {});
    };
    const down = event => {
        suppressClick = false;
        if (viewport.querySelector('.canvas-context-overlay') && !viewport.contains(event.target))
            receiver.invokeMethodAsync('DismissContextAsync').catch(() => {});
        if (event.pointerType !== 'touch') { cancel(); return; }
        active.add(event.pointerId);
        cancel();
        if (active.size !== 1 || event.button !== 0 || viewport.querySelector('.canvas-context-overlay')) return;
        const target = contextTarget(viewport, event.target);
        if (!target) return;
        const origin = { id: event.pointerId, x: event.clientX, y: event.clientY };
        hold = { ...origin, timer: setTimeout(() => {
            hold = null;
            swallowed = origin.id;
            suppressClick = true;
            open(target, origin.x, origin.y, true);
        }, HOLD_MS) };
    };
    const move = event => {
        if (event.pointerId === swallowed) { event.stopImmediatePropagation(); return; }
        if (hold?.id === event.pointerId) {
            if (Math.hypot(event.clientX - hold.x, event.clientY - hold.y) > HOLD_SLOP) cancel();
            else event.stopImmediatePropagation(); // Ignore ordinary finger jitter before drag threshold.
        }
    };
    const end = event => {
        active.delete(event.pointerId);
        if (hold?.id === event.pointerId) cancel();
        if (event.pointerId === swallowed) {
            swallowed = null;
            event.preventDefault();
            event.stopImmediatePropagation();
        }
    };
    const click = event => {
        if (!suppressClick || event.detail === 0) return;
        suppressClick = false;
        event.preventDefault();
        event.stopImmediatePropagation();
    };
    const menu = event => {
        if (viewport.querySelector('.canvas-context-overlay')) { event.preventDefault(); return; }
        const target = contextTarget(viewport, event.target);
        if (!target) return;
        event.preventDefault();
        // Touch contextmenu is browser-dependent; the hold timer is authoritative.
        if (event.pointerType === 'touch' || active.size || suppressClick) return;
        open(target, event.clientX, event.clientY, false);
    };
    const key = event => {
        if (event.key !== 'ContextMenu' && !(event.shiftKey && event.key === 'F10')) return;
        const target = contextTarget(viewport, event.target);
        if (!target) return;
        event.preventDefault();
        event.stopImmediatePropagation();
        const rect = event.target.getBoundingClientRect();
        open(target, rect.left + rect.width / 2, rect.top + rect.height / 2, false);
    };
    const wheel = event => { if (event.target.closest('.canvas-context-overlay')) event.stopPropagation(); };
    const listeners = [[doc, 'pointerdown', down], [doc, 'pointermove', move], [doc, 'pointerup', end],
        [doc, 'pointercancel', end], [doc, 'click', click], [viewport, 'contextmenu', menu], [viewport, 'keydown', key], [viewport, 'wheel', wheel]];
    for (const [element, name, handler] of listeners) element.addEventListener(name, handler, true);
    return { dispose() { cancel(); for (const [element, name, handler] of listeners) element.removeEventListener(name, handler, true); } };
}

export function positionContextMenu(viewport, x, y) {
    const menu = viewport.querySelector('.canvas-context-menu');
    if (!menu) return;
    if (!menu.classList.contains('canvas-context-sheet')) {
        menu.style.left = `${Math.max(0, Math.min(x, viewport.clientWidth - menu.offsetWidth))}px`;
        menu.style.top = `${Math.max(0, Math.min(y, viewport.clientHeight - menu.offsetHeight))}px`;
    }
    menu.querySelector('button')?.focus({ preventScroll: true });
}
