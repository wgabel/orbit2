// DOM-only responsive panel adapter. Blazor owns the shared panel state.
export function mountWorkspacePanels(workspace, receiver) {
    const mobile = matchMedia("(max-width: 768px), (max-height: 480px) and (pointer: coarse)");
    const marker = "orbitMobilePanel";
    const token = crypto.randomUUID?.() ?? String(Math.random());
    let active = null;
    let removing = false;
    let dismissing = false;
    let outsideDismiss = false;
    let disposed = false;
    const ownsEntry = () => history.state?.[marker] === token;
    const locked = () => workspace.dataset.mobilePanelLocked === "true";
    const current = () => workspace.dataset.mobileActivePanel || null;
    const triggerFor = panel => workspace.querySelector(`[data-mobile-trigger="${panel === "selection" ? "edit" : panel}"]`);
    function positionPanel(panel) {
        if (!panel || mobile.matches) return;
        const trigger = triggerFor(panel)?.getBoundingClientRect?.();
        const bounds = workspace.getBoundingClientRect?.();
        if (trigger && bounds) workspace.style.setProperty("--workspace-panel-left", `${trigger.left - bounds.left}px`);
    }
    function focusPanel(panel) {
        const sheet = workspace.querySelector(`[data-mobile-sheet="${panel}"]`);
        const focus = sheet?.querySelector("[data-panel-focus], input:not([disabled]), button:not([disabled])");
        focus?.focus({ preventScroll: true });
    }
    function restoreFocus(panel) {
        if (locked()) return; // Do not steal focus from a newly opened protected editor.
        const trigger = panel === "selection" ? "edit" : panel;
        triggerFor(trigger)?.focus({ preventScroll: true });
    }
    function pushEntry() {
        if (!mobile.matches || ownsEntry() || removing || dismissing || !current()) return;
        try { history.pushState({ ...history.state, [marker]: token }, ""); }
        catch { /* Escape and explicit controls still work if history is unavailable. */ }
    }
    function sync() {
        if (disposed) return;
        const next = current();
        positionPanel(next);
        if (next === active) {
            if (next && mobile.matches) pushEntry();
            else if (!mobile.matches && ownsEntry() && !removing) { removing = true; history.back(); }
            return;
        }
        const previous = active;
        active = next;
        if (next) {
            pushEntry();
            focusPanel(next);
        } else if (previous) {
            if (!outsideDismiss) restoreFocus(previous);
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
        if (!mobile.matches || !current() || ownsEntry()) return;
        if (locked() || !await dismiss()) pushEntry();
    }
    function onPointerDown(event) {
        if (mobile.matches || !current() || locked() || event.target.closest?.("[data-mobile-trigger], [data-mobile-sheet]")) return;
        outsideDismiss = true;
        void dismiss().finally(() => { outsideDismiss = false; });
    }
    const observer = new MutationObserver(sync);
    observer.observe(workspace, { attributes: true, attributeFilter: ["data-mobile-active-panel"] });
    mobile.addEventListener("change", sync);
    const reposition = () => positionPanel(current());
    window.addEventListener("resize", reposition);
    window.addEventListener("popstate", onBack);
    workspace.addEventListener("keydown", onKey, true);
    workspace.addEventListener("pointerdown", onPointerDown, true);
    sync();
    return {
        dispose() {
            disposed = true;
            observer.disconnect();
            mobile.removeEventListener("change", sync);
            window.removeEventListener("resize", reposition);
            window.removeEventListener("popstate", onBack);
            workspace.removeEventListener("keydown", onKey, true);
            workspace.removeEventListener("pointerdown", onPointerDown, true);
        }
    };
}
