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
