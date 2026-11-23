// DOM-only adapter. C# owns gesture state, graph edits and world geometry.
export function viewportBounds(viewport) {
    const rect = viewport.getBoundingClientRect();
    return { left: rect.left + viewport.clientLeft, top: rect.top + viewport.clientTop, width: viewport.clientWidth, height: viewport.clientHeight };
}

export function capturePointer(viewport, pointerId) {
    viewport.focus({ preventScroll: true });
    try {
        viewport.setPointerCapture(pointerId);
    } catch (error) {
        // A quick click may release before the asynchronous interop arrives.
        if (!(error instanceof DOMException)) throw error;
    }
}

export function canvasHasFocus(viewport) {
    return document.activeElement === viewport;
}

export function prefersReducedMotion() {
    return window.matchMedia("(prefers-reduced-motion: reduce)").matches;
}

// Noninteractive node surfaces remain useful for navigation at high zoom,
// while native inputs, labels, buttons and plot interactions keep ownership.
export function isNodeNavigationSurface(viewport, x, y) {
    const element = document.elementFromPoint(x, y);
    return !!element && viewport.contains(element) && !!element.closest("[data-node-id]") &&
        !element.closest("button, input, select, textarea, label, a, [contenteditable], [role='button'], canvas, .node-header");
}

function warnNoteDraft(event) {
    event.preventDefault();
    event.returnValue = true;
}

export function setAnnotationDraftWarning(active) {
    if (active) window.addEventListener("beforeunload", warnNoteDraft);
    else window.removeEventListener("beforeunload", warnNoteDraft);
}

export function measureCanvas(viewport, zoom) {
    const bounds = viewportBounds(viewport);
    const nodes = [];
    const ports = [];
    for (const frame of viewport.querySelectorAll("[data-node-id]")) {
        const rect = frame.getBoundingClientRect();
        const nodeId = frame.dataset.nodeId;
        nodes.push({ nodeId, width: rect.width / zoom, height: rect.height / zoom });
        for (const marker of frame.querySelectorAll("[data-port-id]")) {
            const port = marker.getBoundingClientRect();
            ports.push({
                nodeId, portId: marker.dataset.portId,
                x: (port.left + port.width / 2 - rect.left) / zoom,
                y: (port.top + port.height / 2 - rect.top) / zoom
            });
        }
    }
    // Measure the part of the canvas actually on screen as well as its full
    // size (the latter is still used for Fit). Coordinates are canvas-local.
    const view = window.visualViewport;
    const left = view?.offsetLeft ?? 0;
    const top = view?.offsetTop ?? 0;
    const visibleScreenBounds = {
        left: Math.max(0, left - bounds.left),
        top: Math.max(0, top - bounds.top),
        right: Math.min(bounds.width, left + (view?.width ?? window.innerWidth) - bounds.left),
        bottom: Math.min(bounds.height, top + (view?.height ?? window.innerHeight) - bounds.top)
    };
    return { width: bounds.width, height: bounds.height, nodes, ports, visibleScreenBounds };
}

export function findInputPortAt(viewport, clientX, clientY) {
    const input = document.elementFromPoint(clientX, clientY)?.closest(".port-input");
    if (!input || !viewport.contains(input)) return null;
    const frame = input.closest("[data-node-id]");
    const marker = input.querySelector("[data-port-id]");
    return frame && marker ? { nodeId: frame.dataset.nodeId, portId: marker.dataset.portId } : null;
}

// Measure at drag start, after edge-pan renders and browser reflow, excluding
// clipped or covered markers. Visible bounds are client-space;
// the absolute overlay and transformed world share the *scrolled* origin.
// The C# gesture owns compatibility, snapping, the draft path and release.
export function measureTouchWire(viewport) {
    const bounds = viewportBounds(viewport);
    const visual = window.visualViewport;
    const left = visual?.offsetLeft ?? 0;
    const top = visual?.offsetTop ?? 0;
    const visible = {
        left: Math.max(bounds.left, left),
        top: Math.max(bounds.top, top),
        right: Math.min(bounds.left + bounds.width, left + (visual?.width ?? innerWidth)),
        bottom: Math.min(bounds.top + bounds.height, top + (visual?.height ?? innerHeight))
    };
    const ports = [];
    for (const button of viewport.querySelectorAll(".port-input")) {
        const marker = button.querySelector("[data-port-id]");
        const frame = button.closest("[data-node-id]");
        if (!marker || !frame) continue;
        const rect = marker.getBoundingClientRect();
        const x = rect.left + rect.width / 2, y = rect.top + rect.height / 2;
        if (!rect.width || !rect.height || x < visible.left || x > visible.right ||
            y < visible.top || y > visible.bottom ||
            document.elementFromPoint(x, y)?.closest(".port-input") !== button) continue;
        ports.push({ nodeId: frame.dataset.nodeId, portId: marker.dataset.portId, x, y });
    }
    return { left: bounds.left - (viewport.scrollLeft ?? 0),
        top: bounds.top - (viewport.scrollTop ?? 0), visible, ports };
}

// Observe geometry only for the lifetime of a wire drag. Coalesce browser
// layout notifications, rather than measuring every port on every pointer move.
export function watchTouchWireGeometry(viewport, receiver, generation) {
    let disposed = false, frame = null, previous = "";
    const visual = window.visualViewport;
    function schedule() {
        if (disposed || frame !== null) return;
        frame = requestAnimationFrame(() => {
            frame = null;
            if (disposed || !viewport.isConnected) return;
            const geometry = measureTouchWire(viewport);
            const key = JSON.stringify(geometry);
            if (key === previous) return;
            previous = key;
            receiver.invokeMethodAsync("TouchWireGeometryChanged", generation, geometry).catch(() => {});
        });
    }
    const resize = new ResizeObserver(schedule);
    resize.observe(viewport);
    // A node header or port group can move inputs even when its outer size
    // happens to remain the same. The graph revision handles node replacement.
    for (const element of viewport.querySelectorAll("[data-node-id], .node-header, .node-ports"))
        resize.observe(element);
    window.addEventListener("scroll", schedule, true);
    window.addEventListener("resize", schedule);
    visual?.addEventListener("scroll", schedule);
    visual?.addEventListener("resize", schedule);
    schedule();
    return {
        dispose() {
            disposed = true;
            if (frame !== null) cancelAnimationFrame(frame);
            resize.disconnect();
            window.removeEventListener("scroll", schedule, true);
            window.removeEventListener("resize", schedule);
            visual?.removeEventListener("scroll", schedule);
            visual?.removeEventListener("resize", schedule);
        }
    };
}

export function touchWireFeedback() {
    // Haptics are an enhancement, not a requirement (some browsers omit or reject them).
    try { navigator.vibrate?.(12); } catch { /* unsupported */ }
}

// Clamp the unscaled panel to the visible canvas. Prefer below the cable,
// switch above when needed, and keep the badge itself uncovered when possible.
export function connectionEditorPosition(anchor, bounds, panel, gap) {
    const clamp = (value, min, max) => Math.max(min, Math.min(value, max));
    const left = bounds.left + gap;
    const right = bounds.right - gap - panel.width;
    const top = bounds.top + gap;
    const bottom = bounds.bottom - gap - panel.height;
    const below = anchor.y + gap;
    const above = anchor.y - gap - panel.height;
    const y = below <= bottom ? below : above >= top ? above : below;
    return { x: clamp(anchor.x - panel.width / 2, left, right), y: clamp(y, top, bottom) };
}

// C# supplies the screen-space anchor. Observe only DOM layout and anchor
// changes, not playback frames; keep the same editor instance while typing.
export function mountConnectionEditor(editor) {
    const viewport = editor.closest(".canvas-viewport");
    const visual = window.visualViewport;
    let positioned = false;
    function position() {
        if (!editor.isConnected) return;
        const canvas = viewportBounds(viewport);
        const screenLeft = visual?.offsetLeft ?? 0;
        const screenTop = visual?.offsetTop ?? 0;
        const bounds = {
            left: Math.max(0, screenLeft - canvas.left),
            top: Math.max(0, screenTop - canvas.top),
            right: Math.min(canvas.width, screenLeft + (visual?.width ?? innerWidth) - canvas.left),
            bottom: Math.min(canvas.height, screenTop + (visual?.height ?? innerHeight) - canvas.top)
        };
        const gap = parseFloat(getComputedStyle(editor).marginTop);
        const width = bounds.right - bounds.left - 2 * gap;
        const height = bounds.bottom - bounds.top - 2 * gap;
        if (width <= 0 || height <= 0) { editor.removeAttribute("data-positioned"); return; }
        editor.style.maxWidth = `${width}px`;
        editor.style.maxHeight = `${height}px`;
        const anchor = { x: Number(editor.dataset.anchorX), y: Number(editor.dataset.anchorY) };
        const point = connectionEditorPosition(anchor, bounds, { width: editor.offsetWidth, height: editor.offsetHeight }, gap);
        // CSS margins are part of absolute positioning, so subtract the inset.
        editor.style.left = `${point.x - gap}px`;
        editor.style.top = `${point.y - gap}px`;
        editor.setAttribute("data-positioned", "");
        if (!positioned) {
            positioned = true;
            editor.querySelector("input")?.focus({ preventScroll: true });
        }
    }
    const resize = new ResizeObserver(position);
    resize.observe(viewport);
    resize.observe(editor);
    const attributes = new MutationObserver(position);
    attributes.observe(editor, { attributes: true, attributeFilter: ["data-anchor-x", "data-anchor-y"] });
    window.addEventListener("resize", position);
    window.addEventListener("scroll", position, true);
    visual?.addEventListener("resize", position);
    visual?.addEventListener("scroll", position);
    position();
    return {
        dispose() {
            resize.disconnect();
            attributes.disconnect();
            window.removeEventListener("resize", position);
            window.removeEventListener("scroll", position, true);
            visual?.removeEventListener("resize", position);
            visual?.removeEventListener("scroll", position);
        }
    };
}


// One unscaled connector hint overlay per canvas. Delegation avoids listeners,
// DOM overlays and Blazor rerenders on every individual port or pointer move.
export function portHelpPosition(anchor, bounds, size, gap) {
    const clamp = (value, min, max) => Math.max(min, Math.min(value, max));
    const left = clamp(anchor.left, gap, Math.max(gap, bounds.width - size.width - gap));
    const below = anchor.bottom + gap;
    const above = anchor.top - size.height - gap;
    const top = below + size.height <= bounds.height - gap
        ? below : clamp(above, gap, Math.max(gap, bounds.height - size.height - gap));
    return { left, top };
}

export function mountPortHints(viewport) {
    const tooltip = viewport.ownerDocument.createElement("div");
    tooltip.className = "port-help-tooltip";
    tooltip.setAttribute("role", "tooltip");
    const heading = viewport.ownerDocument.createElement("strong");
    const description = viewport.ownerDocument.createElement("span");
    tooltip.append(heading, description);
    tooltip.hidden = true;
    viewport.append(tooltip);
    let active = null, originalTitle = null;
    const hide = () => {
        if (active && originalTitle !== null) active.setAttribute("title", originalTitle);
        active = null;
        originalTitle = null;
        tooltip.hidden = true;
    };
    const show = button => {
        if (button === active || !button?.isConnected || !button.dataset.portHelp ||
            viewport.querySelector(".node-help-overlay, .canvas-context-overlay")) return;
        hide();
        active = button;
        originalTitle = button.getAttribute("title");
        if (originalTitle !== null) button.removeAttribute("title"); // prevent duplicate native tooltip
        heading.textContent = button.dataset.portTitle ?? "Connector";
        description.textContent = button.dataset.portHelp; // text only: no parsed HTML
        tooltip.hidden = false;
        const bounds = viewportBounds(viewport);
        const rect = button.getBoundingClientRect();
        const gap = parseFloat(getComputedStyle(tooltip).paddingTop) || 0;
        const point = portHelpPosition({
            left: rect.left - bounds.left,
            top: rect.top - bounds.top,
            bottom: rect.bottom - bounds.top
        }, bounds, { width: tooltip.offsetWidth, height: tooltip.offsetHeight }, gap);
        tooltip.style.left = `${point.left}px`;
        tooltip.style.top = `${point.top}px`;
    };
    const buttonFor = element => element?.closest?.(".port-connector[data-port-help]") ?? null;
    const over = event => {
        if (event.pointerType !== "touch") show(buttonFor(event.target));
    };
    const out = event => {
        if (active && active.contains(event.target) &&
            (!event.relatedTarget || !active.contains(event.relatedTarget))) hide();
    };
    const focus = event => {
        const button = buttonFor(event.target);
        if (button?.matches(":focus-visible")) show(button);
    };
    const blur = event => { if (active?.contains(event.target)) hide(); };
    const events = [
        ["pointerover", over], ["pointerout", out], ["focusin", focus], ["focusout", blur],
        ["pointerdown", hide], ["wheel", hide]
    ];
    for (const [name, handler] of events) viewport.addEventListener(name, handler, true);
    window.addEventListener("resize", hide);
    window.addEventListener("scroll", hide, true);
    return {
        dispose() {
            hide();
            tooltip.remove();
            for (const [name, handler] of events) viewport.removeEventListener(name, handler, true);
            window.removeEventListener("resize", hide);
            window.removeEventListener("scroll", hide, true);
        }
    };
}

// Position the node popup next to its ? trigger in unscaled CSS pixels.
// Narrow/coarse viewports use the CSS bottom sheet instead.
export function positionNodeHelp(viewport, nodeId) {
    const panel = viewport.querySelector(".node-help-panel");
    const trigger = [...viewport.querySelectorAll(".node-help-trigger")]
        .find(button => button.dataset.helpNodeId === nodeId);
    if (!panel || !trigger) return;
    if (matchMedia("(max-width: 768px), (max-height: 480px) and (pointer: coarse)").matches) return;
    const bounds = viewportBounds(viewport);
    const rect = trigger.getBoundingClientRect();
    const gap = parseFloat(getComputedStyle(panel).paddingTop) || 0;
    const width = panel.offsetWidth, height = panel.offsetHeight;
    const rightSide = rect.right - bounds.left + gap;
    const leftSide = rect.left - bounds.left - width - gap;
    const left = rightSide + width < bounds.width - gap ? rightSide
        : leftSide >= gap ? leftSide : Math.max(gap, bounds.width - width - gap);
    const top = Math.max(gap, Math.min(rect.top - bounds.top, bounds.height - height - gap));
    panel.style.right = "auto";
    panel.style.left = `${left}px`;
    panel.style.top = `${top}px`;
}

export function focusNodeHelpTrigger(viewport, nodeId) {
    [...viewport.querySelectorAll(".node-help-trigger")]
        .find(button => button.dataset.helpNodeId === nodeId)?.focus({ preventScroll: true });
}

// Modal help is an ordinary local DOM overlay. Trap Tab, leaving Escape/Close
// and all graph commands under their established C# event ownership.
export function trapNodeHelpFocus(viewport, receiver) {
    const panel = viewport.querySelector(".node-help-panel");
    if (!panel) return { dispose() {} };
    const mobile = matchMedia("(max-width: 768px), (max-height: 480px) and (pointer: coarse)");
    const marker = "orbitNodeHelp";
    const token = crypto.randomUUID?.() ?? String(Math.random());
    const ownsEntry = () => history.state?.[marker] === token;
    let backClosing = false;
    if (mobile.matches) {
        try { history.pushState({ ...history.state, [marker]: token }, ""); }
        catch { /* Close/Escape remain available if history is restricted. */ }
    }

    function onKey(event) {
        if (event.key !== "Tab") return;
        const controls = [...panel.querySelectorAll("button:not([disabled]), a[href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex='-1'])")];
        if (!controls.length) return;
        const first = controls[0], last = controls.at(-1);
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus({ preventScroll: true });
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus({ preventScroll: true });
        }
    }
    function onBack() {
        if (!mobile.matches || ownsEntry()) return;
        backClosing = true;
        void receiver.invokeMethodAsync("DismissNodeHelpFromBackAsync").catch(() => {});
    }
    panel.addEventListener("keydown", onKey, true);
    window.addEventListener("popstate", onBack);
    return {
        dispose() {
            panel.removeEventListener("keydown", onKey, true);
            window.removeEventListener("popstate", onBack);
            // Explicit Close/Escape consumes our transient entry, not the preceding page.
            if (!backClosing && ownsEntry()) history.back();
        }
    };
}
