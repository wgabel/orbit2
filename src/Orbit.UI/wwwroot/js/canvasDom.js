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
    return { width: bounds.width, height: bounds.height, nodes, ports };
}

export function findInputPortAt(viewport, clientX, clientY) {
    const input = document.elementFromPoint(clientX, clientY)?.closest(".port-input");
    if (!input || !viewport.contains(input)) return null;
    const frame = input.closest("[data-node-id]");
    const marker = input.querySelector("[data-port-id]");
    return frame && marker ? { nodeId: frame.dataset.nodeId, portId: marker.dataset.portId } : null;
}
