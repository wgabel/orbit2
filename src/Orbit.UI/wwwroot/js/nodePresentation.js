// Presentation-only preferences: pinned nodes are keyed by patch and graph.
// No graph, patch, or audio state is stored here.
const prefix = "orbit2.node-pins.v1.";

export function readPinned(patchId, graphId) {
    try {
        const raw = localStorage.getItem(prefix + patchId + "." + graphId);
        const ids = raw ? JSON.parse(raw) : [];
        return Array.isArray(ids) ? ids.filter(id => typeof id === "string") : [];
    } catch (error) {
        console.warn("Node pin preferences could not be loaded.", error);
        return [];
    }
}

export function writePinned(patchId, graphId, ids) {
    try {
        localStorage.setItem(prefix + patchId + "." + graphId, JSON.stringify(ids));
    } catch (error) {
        console.warn("Node pin preferences could not be saved.", error);
    }
}
