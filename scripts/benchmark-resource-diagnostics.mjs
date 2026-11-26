export async function openResourceDiagnostics(page) {
    // Reuse the exact loaded module instance (including the deployment version),
    // never import the unversioned alternative and accidentally sample another engine.
    const moduleUrl = await page.evaluate(() => {
        const urls = [...new Set(performance.getEntriesByType("resource")
            .map(entry => entry.name)
            .filter(name => /\/_content\/Orbit\.Infrastructure\/audio(?:-[0-9a-f]{40})?\/audio\.js$/.test(new URL(name).pathname)))];
        if (urls.length !== 1) throw new Error("Expected exactly one loaded audio entry module: " + urls.join(", "));
        return urls[0];
    });
    const setCollection = enabled => page.evaluate(async ({ url, enabled }) =>
        (await import(url)).setPerformanceCollection(enabled), { url: moduleUrl, enabled });
    const snapshot = () => page.evaluate(async url => {
        const r = (await import(url)).getPerformanceSnapshot()?.runtime;
        if (!r) return null;
        return { currentVoices: r.currentVoices, retiringVoices: r.retiringVoices,
            outputChannels: r.outputChannels, occupiedAuxSlots: r.occupiedAuxSlots,
            captureLeases: r.captureLeases, processorErrors: r.processorErrors };
    }, moduleUrl);
    return { setCollection, snapshot };
}
