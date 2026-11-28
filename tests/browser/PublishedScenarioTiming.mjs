import assert from 'node:assert/strict';
import { performance } from 'node:perf_hooks';

// Sequential wall-clock markers only: no extra browser calls, waits or test actions.
export function createPublishedScenarioTimer({ now = () => performance.now(), log = console.log } = {}) {
    const seen = new Set();
    const rows = [];
    let current = null, started, first, finished = false;

    function close(status, end) {
        if (current === null) return;
        const row = { scenario: current, status, wallMs: Math.round(end - started) };
        rows.push(row);
        log('PUBLISHED SCENARIO WALL ' + JSON.stringify(row));
    }

    return {
        enter(name) {
            assert.ok(!finished && typeof name === 'string' && name && !seen.has(name), 'Invalid or repeated scenario marker');
            const boundary = now();
            close('passed', boundary);
            seen.add(name);
            current = name;
            started = boundary;
            first ??= boundary;
        },
        finish(status) {
            if (finished) return;
            assert.ok(current !== null && ['passed', 'failed'].includes(status), 'Invalid scenario completion');
            const end = now();
            close(status, end);
            finished = true;
            log('PUBLISHED SCENARIO TOTAL ' + JSON.stringify({ status, scenarios: rows.length, wallMs: Math.round(end - first) }));
        }
    };
}
