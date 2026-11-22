// Local editor preferences only; never mutate a patch document.
const key = "orbit2.node-library.v1";
export function read() {
    try {
        const value = JSON.parse(localStorage.getItem(key) ?? "null");
        if (!value || !Array.isArray(value.favorites) || !Array.isArray(value.recent)) return null;
        return {
            favorites: value.favorites.filter(value => typeof value === "string").slice(0, 128),
            recent: value.recent.filter(value => typeof value === "string").slice(0, 6)
        };
    } catch { return null; }
}
export function write(value) {
    try { localStorage.setItem(key, JSON.stringify(value)); }
    catch { /* Storage may be unavailable in private browsing. */ }
}
