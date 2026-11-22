// DOM-only mobile sheet adapter. The Blazor workspace owns actual panel state.
export function mountWorkspacePanels(workspace, receiver) {
    const mobile = matchMedia("(max-width: 768px), (max-height: 480px) and (pointer: coarse)");
    const marker = "orbitMobilePanel";
    const token = crypto.randomUUID?.() ?? String(Math.random());
    let active = null;
    let removing = false;
    let dismissing = false;
    let disposed = false;
    const ownsEntry = () => history.state?.[marker] === token;
    const locked = () => workspace.dataset.mobilePanelLocked === "true";
    const current = () => mobile.matches ? workspace.dataset.mobileActivePanel || null : null;
    function focusPanel(panel) {
        const sheet = workspace.querySelector(`[data-mobile-sheet="${panel}"]`);
        const focus = sheet?.querySelector("[data-panel-focus], input:not([disabled]), button:not([disabled])");
        focus?.focus({ preventScroll: true });
    }
    function restoreFocus(panel) {
        if (locked()) return; // Do not steal focus from a newly opened protected editor.
        const trigger = panel === "selection" ? "edit" : panel;
        workspace.querySelector(`[data-mobile-trigger="${trigger}"]`)?.focus({ preventScroll: true });
    }
    function pushEntry() {
        if (ownsEntry() || removing || dismissing || !current()) return;
        try { history.pushState({ ...history.state, [marker]: token }, ""); }
        catch { /* Escape and explicit controls still work if history is unavailable. */ }
    }
    function sync() {
        if (disposed) return;
        const next = current();
        if (next === active) return;
        const previous = active;
        active = next;
        if (next) {
            pushEntry();
            focusPanel(next);
        } else if (previous) {
            if (mobile.matches) restoreFocus(previous);
            if (ownsEntry() && !removing && !dismissing) {
                removing = true;
                history.back();
            }
        }
    }
    async function dismiss() {
        if (disposed || dismissing || !current()) return false;
        dismissing = true;
        try { return await receiver.invokeMethodAsync("DismissMobilePanelCoreAsync"); }
        catch { return false; }
        finally { dismissing = false; }
    }
    function onKey(event) {
        if (event.key !== "Escape" || !current() || locked() || event.defaultPrevented) return;
        event.preventDefault();
        event.stopPropagation();
        // An Escape close must remove the history entry after C# closes the sheet.
        void dismiss().then(() => {
            if (!current() && ownsEntry() && !removing) { removing = true; history.back(); }
        });
    }
    async function onBack() {
        if (disposed) return;
        if (removing) {
            removing = false;
            if (current()) pushEntry();
            return;
        }
        if (!current() || ownsEntry()) return;
        if (locked() || !await dismiss()) pushEntry();
    }
    const observer = new MutationObserver(sync);
    observer.observe(workspace, { attributes: true, attributeFilter: ["data-mobile-active-panel"] });
    mobile.addEventListener("change", sync);
    window.addEventListener("popstate", onBack);
    workspace.addEventListener("keydown", onKey, true);
    sync();
    return {
        dispose() {
            disposed = true;
            observer.disconnect();
            mobile.removeEventListener("change", sync);
            window.removeEventListener("popstate", onBack);
            workspace.removeEventListener("keydown", onKey, true);
        }
    };
}
