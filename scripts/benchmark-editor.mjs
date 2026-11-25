// Opt-in external editor benchmark. No production instrumentation or quality changes.
import assert from "node:assert/strict";
import { createHash } from "node:crypto";
import { readFile, writeFile } from "node:fs/promises";
import { resolve } from "node:path";
import { setTimeout as delay } from "node:timers/promises";
import { platform, release, arch, cpus } from "node:os";
import { summarize } from "./benchmark-metrics.mjs";
import { verifyFoldedTelemetry } from "./benchmark-folded-telemetry.mjs";

export async function runEditorBenchmark({ browser, baseUrl, directory, outputFile, profiles, headless }) {
    const repeats = Number(process.env.ORBIT_EDITOR_REPEATS ?? 3);
    const idleMs = Number(process.env.ORBIT_EDITOR_IDLE_MS ?? 1500);
    const mode = process.env.ORBIT_EDITOR_VIEWPORT ?? "desktop";
    const playback = process.env.ORBIT_EDITOR_PLAYBACK !== "0";
    const countRenders = process.env.ORBIT_EDITOR_RENDER_COUNTS === "1";
    const expansionMatrix = process.env.ORBIT_EDITOR_EXPANSION_MATRIX === "1";
    assert.ok(Number.isInteger(repeats) && repeats >= 1 && repeats <= 5);
    assert.ok(Number.isInteger(idleMs) && idleMs >= 500 && idleMs <= 10000);
    assert.ok(["desktop", "mobile"].includes(mode));
    const mobile = mode === "mobile";
    const viewport = mobile ? { width: 390, height: 844 } : { width: 1920, height: 1080 };
    const rows = [];
    for (const profile of profiles) {
        if (profile.composite) assert.ok(!countRenders && !expansionMatrix,
            "Composite fixture uses its own navigation matrix; run separately from flat expansion/render-count modes");
        const context = await browser.newContext({ viewport, isMobile: mobile, hasTouch: mobile,
            deviceScaleFactor: 1 });
        try {
            const page = await context.newPage();
            const cdp = await context.newCDPSession(page);
            await cdp.send("Performance.enable");
            const metrics = async () => Object.fromEntries((await cdp.send("Performance.getMetrics")).metrics
                .map(({ name, value }) => [name, value]));
            await page.goto(countRenders ? baseUrl + "/?orbit-render-counts=1" : baseUrl);
            const status = page.locator(".playback-toolbar").getByRole("status");
            const ready = () => status.filter({ hasText: /Ready/ }).waitFor({ timeout: 60000 });
            const play = page.getByRole("button", { name: "Play patch" });
            const more = page.getByRole("navigation", { name: "Workspace tools" }).getByRole("button", { name: "More", exact: true });
            const tools = page.getByRole("region", { name: "More tools" });
            await ready();
            await more.click();
            await tools.getByRole("button", { name: "Patches", exact: true }).click();
            const manager = page.getByRole("region", { name: "Patch manager" });
            const patchPath = resolve(directory, profile.patch);
            const fingerprint = createHash("sha256").update(await readFile(patchPath)).digest("hex");
            const started = performance.now();
            await manager.getByLabel("Import JSON").setInputFiles(patchPath);
            await manager.getByText("Imported patch.", { exact: true }).waitFor({ timeout: 120000 });
            await manager.getByRole("button", { name: "Close" }).click();
            // The existing workspace may close More along with Patch manager.
            if (await tools.isVisible()) await more.click();
            await ready();
            await page.waitForFunction(expected => document.querySelectorAll(".canvas-world [data-node-id]").length === expected,
                profile.nodes, { timeout: 120000 });
            await page.waitForFunction(expected => document.querySelectorAll(".canvas-world .connection-hit").length === expected,
                profile.connections, { timeout: 120000 });
            const importWallMs = performance.now() - started;
            await delay(1000);
            const geometry = () => page.locator(".canvas-viewport").evaluate(view => {
                const box = view.getBoundingClientRect();
                const nodes = [...view.querySelectorAll(".canvas-world [data-node-id]")];
                const intersects = element => {
                    const r = element.getBoundingClientRect();
                    return r.right > box.left && r.left < box.right && r.bottom > box.top && r.top < box.bottom;
                };
                const world = view.querySelector(".canvas-world");
                const matrix = new DOMMatrixReadOnly(getComputedStyle(world).transform);
                return { renderedNodes: nodes.length, intersectingNodes: nodes.filter(intersects).length,
                    renderedConnections: view.querySelectorAll(".connection-hit").length,
                    domElements: view.querySelectorAll("*").length, viewport: { width: box.width, height: box.height },
                    zoom: matrix.a, panX: matrix.e, panY: matrix.f };
            });
            const initialGeometry = await geometry();
            assert.ok(initialGeometry.zoom >= .249 && initialGeometry.zoom <= 4.001);
            const measurements = [];
            const renderCounts = () => page.locator(".canvas-world [data-node-render-count]").evaluateAll(nodes =>
                Object.fromEntries(nodes.map(node => [node.dataset.nodeId, Number(node.dataset.nodeRenderCount)])));
            if (countRenders) assert.equal(Object.keys(await renderCounts()).length, profile.nodes);
            async function measure(state, workload, action, expansion = null) {
                const beforeRenders = countRenders ? await renderCounts() : null;
                const before = await metrics();
                await page.evaluate(() => {
                    const gaps = [], longTasks = [];
                    let previous = null, raf;
                    const start = performance.now();
                    const tick = now => {
                        if (previous !== null && gaps.length < 10000) gaps.push(now - previous);
                        previous = now;
                        raf = requestAnimationFrame(tick);
                    };
                    raf = requestAnimationFrame(tick);
                    const supported = PerformanceObserver.supportedEntryTypes?.includes("longtask") ?? false;
                    const append = entries => {
                        for (const entry of entries) if (entry.startTime >= start && longTasks.length < 1000)
                            longTasks.push(entry.duration);
                    };
                    const observer = supported ? new PerformanceObserver(list => append(list.getEntries())) : null;
                    observer?.observe({ entryTypes: ["longtask"] });
                    globalThis.__orbitEditorBenchmarkStop = () => {
                        cancelAnimationFrame(raf);
                        if (observer) { append(observer.takeRecords()); observer.disconnect(); }
                        delete globalThis.__orbitEditorBenchmarkStop;
                        return { gaps, longTasks: supported ? longTasks : null, elapsedMs: performance.now() - start };
                    };
                });
                let sample, actionResult;
                try {
                    actionResult = await action();
                    await delay(150);
                } finally {
                    sample = await page.evaluate(() => globalThis.__orbitEditorBenchmarkStop());
                }
                const after = await metrics();
                const delta = name => Number.isFinite(before[name]) && Number.isFinite(after[name]) ?
                    after[name] - before[name] : null;
                let nodeRenders = null;
                if (beforeRenders) {
                    const afterRenders = await renderCounts();
                    assert.equal(Object.keys(afterRenders).length, profile.nodes);
                    const changes = Object.entries(afterRenders).map(([id, count]) => count - beforeRenders[id]);
                    assert.ok(changes.every(value => Number.isInteger(value) && value >= 0));
                    nodeRenders = { total: changes.reduce((a, b) => a + b, 0), affectedNodes: changes.filter(n => n > 0).length };
                    if (state === "playing" && workload === "idle")
                        assert.ok(nodeRenders.affectedNodes < profile.nodes,
                            "Playback telemetry must not invalidate every node frame");
                    if (state === "stopped" && workload === "parameter-edit")
                        assert.equal(nodeRenders.affectedNodes, 1, "Carrier frequency edit must only rerender the changed frame");
                    if (state === "stopped" && workload === "pan")
                        assert.equal(nodeRenders.total, 0, "Pure canvas pan must not rerender node frames");
                    if (state === "stopped" && workload === "node-drag")
                        assert.ok(nodeRenders.affectedNodes > 0 && nodeRenders.affectedNodes <= 2,
                            "Dragging must rerender only moved/previously selected frames");
                }
                measurements.push({ state, workload, expansion, nodeRenders, elapsedMs: sample.elapsedMs,
                    ...(workload === "parameter-edit" ? { parameterAccess: actionResult } : {}),
                    frameGapsMs: sample.gaps.length ? summarize(sample.gaps) : null,
                    observedFps: sample.gaps.length ? 1000 * sample.gaps.length / sample.gaps.reduce((a, b) => a + b, 0) : null,
                    longTasksMs: sample.longTasks,
                    cdp: Object.fromEntries(["LayoutCount", "RecalcStyleCount", "LayoutDuration",
                        "RecalcStyleDuration", "ScriptDuration", "TaskDuration", "JSHeapUsedSize"].map(name => [name, delta(name)])),
                    geometry: await geometry() });
            }
            const canvas = page.locator(".canvas-viewport");
            async function pan() {
                const r = await canvas.boundingBox();
                const x = r.x + r.width * .55, y = r.y + r.height * .55;
                await page.mouse.move(x, y);
                await page.mouse.down({ button: "middle" });
                await page.mouse.move(x + 40, y + 30, { steps: 12 });
                await page.mouse.move(x, y, { steps: 12 });
                await page.mouse.up({ button: "middle" });
            }
            async function zoom() {
                const r = await canvas.boundingBox();
                await page.mouse.move(r.x + r.width / 2, r.y + r.height / 2);
                await page.mouse.wheel(0, -100);
                await delay(200);
                await page.mouse.wheel(0, 100);
                await delay(200);
            }
            async function drag() {
                const point = await canvas.evaluate(view => {
                    const v = view.getBoundingClientRect();
                    for (const header of view.querySelectorAll(".node-header")) {
                        const label = header.querySelector(".node-display-name");
                        if (!label) continue;
                        const r = label.getBoundingClientRect();
                        const x = r.left + r.width / 2, y = r.top + r.height / 2;
                        if (x > v.left + 30 && x < v.right - 60 && y > v.top + 30 && y < v.bottom - 60 &&
                            header.contains(document.elementFromPoint(x, y))) return { x, y };
                    }
                    return null;
                });
                assert.ok(point, "No visible draggable node header");
                await page.mouse.move(point.x, point.y);
                await page.mouse.down();
                await page.mouse.move(point.x + 25, point.y + 20, { steps: 12 });
                await page.mouse.move(point.x, point.y, { steps: 12 });
                await page.mouse.up();
            }
            async function editParameter(state) {
                // Prefer a hit-testable carrier input. In dense layouts the original
                // expanded editor may be covered by another node, so allow scripted
                // focus on an in-viewport field without changing world geometry.
                const target = await canvas.evaluate(view => {
                    const v = view.getBoundingClientRect();
                    // Match only the original Frequency slider's own base input.
                    // The adjacent cycle-period editor is a different control.
                    const selector = '.parameter-slider:has(> .parameter-track > input[aria-label="Frequency"]) > .parameter-heading > input[type="number"][id^="parameter-"]';
                    let fallback = null;
                    for (const input of view.querySelectorAll(selector)) {
                        const value = Number(input.value);
                        if (!Number.isFinite(value) || value < 100 || input.disabled || input.readOnly) continue;
                        const r = input.getBoundingClientRect();
                        const left = Math.max(r.left, v.left, 0), right = Math.min(r.right, v.right, innerWidth);
                        const top = Math.max(r.top, v.top, 0), bottom = Math.min(r.bottom, v.bottom, innerHeight);
                        if (right <= left || bottom <= top || getComputedStyle(input).visibility !== "visible") continue;
                        const x = (r.left + r.right) / 2, y = (r.top + r.bottom) / 2;
                        if (x > left && x < right && y > top && y < bottom && document.elementFromPoint(x, y) === input)
                            return { id: input.id, access: "pointer-hit-testable" };
                        fallback ??= { id: input.id, access: "scripted-focus-in-viewport" };
                    }
                    return fallback;
                });
                assert.ok(target, "No in-viewport carrier frequency base editor (including occluded fields)");
                const { id, access } = target;
                const input = page.locator("#" + id);
                if (access === "scripted-focus-in-viewport") {
                    // Focus the real native field without scrolling, not a graph command
                    // or an offscreen edit. This measures editing cost, not pointer usability.
                    assert.ok(await input.evaluate(element => {
                        element.focus({ preventScroll: true });
                        return document.activeElement === element;
                    }), "Could not focus the in-viewport carrier frequency editor");
                }
                const original = Number(await input.inputValue());
                assert.ok(Number.isFinite(original) && original >= 100);
                async function commit(value) {
                    const current = state === "playing" ? Number((await status.innerText()).match(/\d+$/)?.[0]) : null;
                    if (state === "playing") assert.ok(Number.isInteger(current), "Expected active playback revision");
                    await input.fill(String(value));
                    // The original ParameterSlider commits on input, not Enter.
                    // Wait for the sibling range's model-backed value, not just the input draft.
                    await page.waitForFunction(({ id, value }) => {
                        const field = document.getElementById(id);
                        const slider = field?.closest(".parameter-slider");
                        const range = slider?.querySelector(':scope > .parameter-track > input[type="range"]');
                        const committed = range?.getAttribute("aria-valuetext");
                        return committed != null && Number.parseFloat(committed) === value &&
                            field.getAttribute("aria-invalid") === "false";
                    }, { id, value }, { timeout: 60000 });
                    if (state === "playing") {
                        await status.filter({ hasText: new RegExp("^Playing revision " + (current + 1) + "$") })
                            .waitFor({ timeout: 60000 });
                    } else {
                        await ready();
                    }
                    await input.blur();
                    assert.equal(Number(await input.inputValue()), value);
                }
                await commit(original + 1);
                await commit(original);
                return access;
            }
            async function exercise(state) {
                for (let i = 0; i < repeats; i++) {
                    await measure(state, "idle", () => delay(idleMs));
                    await measure(state, "pan", pan);
                    await measure(state, "wheel-zoom", zoom);
                    await measure(state, "node-drag", drag);
                    await measure(state, "parameter-edit", () => editParameter(state));
                }
            }
            // Use the real fold toggles, including for dense/offscreen leaves. Do not
            // edit the graph or measure the transition as steady-state performance.
            async function exerciseExpansionMatrix(state) {
                const eligible = await canvas.locator("article.node-compactable[data-node-id]")
                    .evaluateAll(nodes => nodes.map(node => node.dataset.nodeId));
                assert.ok(eligible.length >= 5, "Expansion matrix requires at least five eligible leaves");
                assert.equal(new Set(eligible).size, eligible.length);
                const revision = (await status.innerText()).trim();
                async function setExpanded(ids) {
                    const desired = new Set(ids);
                    for (const id of eligible) {
                        const toggle = canvas.locator(`article[data-node-id="${id}"] .node-expand-toggle`);
                        const expanded = String(desired.has(id));
                        if (await toggle.getAttribute("aria-expanded") !== expanded) {
                            await toggle.evaluate(button => button.click());
                            await canvas.locator(`article[data-node-id="${id}"] .node-expand-toggle[aria-expanded="${expanded}"]`)
                                .waitFor({ state: "attached", timeout: 60000 });
                        }
                    }
                    const actual = await canvas.locator('article.node-compactable[data-node-id]:has(.node-expand-toggle[aria-expanded="true"])')
                        .evaluateAll(nodes => nodes.map(node => node.dataset.nodeId));
                    assert.deepEqual([...actual].sort(), [...ids].sort());
                    await delay(1000);
                    assert.equal((await status.innerText()).trim(), revision, "Expansion must preserve playback state/revision");
                }
                const cases = [
                    ["all-expanded", eligible],
                    ["five-expanded", eligible.slice(0, 5)],
                    ["one-expanded", eligible.slice(0, 1)],
                    ["all-folded", []]
                ];
                for (const [name, ids] of cases) {
                    await setExpanded(ids);
                    const expansion = { name, eligibleLeaves: eligible.length, expandedNodeIds: [...ids].sort() };
                    for (let i = 0; i < repeats; i++) {
                        await measure(state, "idle", () => delay(idleMs), expansion);
                        await measure(state, "pan", pan, expansion);
                        await measure(state, "wheel-zoom", zoom, expansion);
                    }
                }
                await setExpanded(eligible);
            }
            async function exerciseCompositeMatrix(state) {
                const { wrapperId, name, rootNodeIds, childNodeIds, childConnections } = profile.composite;
                assert.equal(rootNodeIds.length, 3);
                assert.equal(childNodeIds.length, 102);
                assert.equal(new Set([...rootNodeIds, ...childNodeIds]).size, 105);
                const path = page.getByRole("navigation", { name: "Canvas path" });
                const revision = (await status.innerText()).trim();
                const rootButton = () => path.getByRole("button").first();
                async function checkCanvas(ids, connections) {
                    await page.waitForFunction(({ ids, connections }) => {
                        const world = document.querySelector(".canvas-world");
                        if (!world) return false;
                        const actual = [...world.querySelectorAll("[data-node-id]")].map(node => node.dataset.nodeId);
                        return actual.length === ids.length && new Set(actual).size === ids.length &&
                            ids.every(id => actual.includes(id)) &&
                            world.querySelectorAll(".connection-hit").length === connections;
                    }, { ids, connections }, { timeout: 120000 });
                    assert.equal((await status.innerText()).trim(), revision,
                        "Canvas navigation must preserve playback state/revision");
                }
                async function runCase(name, ids, connections) {
                    await checkCanvas(ids, connections);
                    await delay(1000);
                    const scope = { name, canvasNodeIds: [...ids].sort() };
                    if (state === "playing" && ids === childNodeIds) {
                        await page.waitForFunction(() => [...document.querySelectorAll(
                            '.canvas-world .parameter-slider output[aria-label="Live effective value"]')]
                            .some(output => Number.isFinite(Number.parseFloat(output.textContent.trim()))),
                            null, { timeout: 60000 });
                    }
                    for (let i = 0; i < repeats; i++) {
                        await measure(state, "idle", () => delay(idleMs), scope);
                        await measure(state, "pan", pan, scope);
                        await measure(state, "wheel-zoom", zoom, scope);
                        await checkCanvas(ids, connections);
                    }
                }
                await runCase("composite-closed", rootNodeIds, profile.connections);
                const open = canvas.locator(`article[data-node-id="${wrapperId}"]`)
                    .getByRole("button", { name: "Open " + name, exact: true });
                await open.evaluate(button => button.click());
                await path.locator('[aria-current="page"]').filter({ hasText: name }).waitFor({ timeout: 60000 });
                await runCase("composite-open", childNodeIds, childConnections);
                await rootButton().click();
                await runCase("composite-reclosed", rootNodeIds, profile.connections);
            }
            const runWorkloads = state => profile.composite ? exerciseCompositeMatrix(state) :
                expansionMatrix ? exerciseExpansionMatrix(state) : exercise(state);
            // Desktop pointer workloads on a narrow viewport are not physical
            // touch/pinch measurements; label that limitation in the report.
            await runWorkloads("stopped");
            let initialPlayWallMs = null, foldedTelemetry = null;
            if (playback) {
                const start = performance.now();
                await play.click();
                await status.filter({ hasText: /^Playing revision \d+$/ }).waitFor({ timeout: 120000 });
                initialPlayWallMs = performance.now() - start;
                await delay(1000);
                if (countRenders && !expansionMatrix) {
                    // Require actual finite effective readouts before checking render scope.
                    await page.waitForFunction(() => [...document.querySelectorAll('.canvas-world .parameter-slider output[aria-label="Live effective value"]')]
                        .some(output => Number.isFinite(Number.parseFloat(output.textContent.trim()))),
                        null, { timeout: 60000 });
                }
                await runWorkloads("playing");
                if (countRenders && !expansionMatrix) {
                    foldedTelemetry = await verifyFoldedTelemetry({ page, canvas, status, renderCounts, idleMs });
                    console.log("EDITOR FOLDED TELEMETRY " + JSON.stringify(foldedTelemetry));
                }
                await page.getByRole("button", { name: "Stop", exact: true }).click();
                await ready();
                if (countRenders) await page.waitForFunction(() => {
                    const outputs = [...document.querySelectorAll('.canvas-world .parameter-slider output[aria-label="Live effective value"]')];
                    return outputs.length > 0 && outputs.every(output => output.textContent.trim() === "—");
                }, null, { timeout: 60000 });
            }
            const finalGeometry = await geometry();
            assert.equal(finalGeometry.renderedNodes, profile.nodes);
            assert.equal(finalGeometry.renderedConnections, profile.connections);
            rows.push({ ...profile, fingerprint, importWallMs, initialPlayWallMs, initialGeometry, finalGeometry, measurements, foldedTelemetry });
            console.log(profile.id + ": " + initialGeometry.intersectingNodes + " viewport-intersecting nodes, " +
                measurements.length + " measured interaction windows");
            // Log compact results as well as preserving the detailed artifact.
            // CI hardware varies; these are observations, never timing gates.
            for (const m of measurements) {
                const fixed = value => Number.isFinite(value) ? value.toFixed(2) : "N/A";
                console.log(`EDITOR BENCH ${profile.id} ${mode} ${m.state} ${m.workload} ${m.expansion?.name ?? "default"} ` +
                    `fps=${fixed(m.observedFps)} p95ms=${fixed(m.frameGapsMs?.p95)} ` +
                    `scriptMs=${fixed(m.cdp.ScriptDuration === null ? null : m.cdp.ScriptDuration * 1000)} ` +
                    `layoutMs=${fixed(m.cdp.LayoutDuration === null ? null : m.cdp.LayoutDuration * 1000)} ` +
                    `styleMs=${fixed(m.cdp.RecalcStyleDuration === null ? null : m.cdp.RecalcStyleDuration * 1000)} ` +
                    `longTasks=${m.longTasksMs?.length ?? "N/A"} nodes=${m.geometry.intersectingNodes} dom=${m.geometry.domElements}` +
                    (m.nodeRenders ? ` frameRenders=${m.nodeRenders.total} affectedFrames=${m.nodeRenders.affectedNodes}` : "") +
                    (m.parameterAccess ? ` parameterAccess=${m.parameterAccess}` : ""));
            }
        } finally { await context.close(); }
    }
    await writeFile(outputFile, JSON.stringify({ schemaVersion: 1, fixtureVersion: 1,
        kind: "external-editor-benchmark", revision: process.env.ORBIT_REVISION ?? process.env.GITHUB_SHA ?? null,
        timestampUtc: new Date().toISOString(),
        harnessFingerprint: createHash("sha256").update(JSON.stringify(await Promise.all(
            ["benchmark-editor.mjs", "benchmark-live.mjs", "benchmark-metrics.mjs", "benchmark-folded-telemetry.mjs"]
                .map(async name => [name, createHash("sha256").update(await readFile(new URL(name, import.meta.url))).digest("hex")])))).digest("hex"),
        environment: { chrome: browser.version(), node: process.version, headless, muted: true,
            platform: platform(), release: release(), arch: arch(), cpuModel: cpus()[0]?.model ?? null, logicalCpuCount: cpus().length },
        settings: { repeats, idleMs, viewport, mode, playback, input: "scripted mouse; mobile is viewport emulation",
            monitorOpen: false, countRenders, expansionMatrix, warmupMs: 1000, cdpDurationUnit: "seconds" },
        limitations: ["rAF callback gaps are not GPU paint timings", "CDP durations are renderer counters, not component render counts",
            "No physical Android, touch, or audio underrun measurement", "Import wall time includes file selection and dialog dismissal",
            "Parameter edits may use scripted focus on an occluded in-viewport editor; access method is reported and does not establish pointer usability"],
        rows }, null, 2) + "\n");
}
