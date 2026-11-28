// Keep the runtime input exclusion list deliberately narrow.
// The same policy controls CI classification and deployment freshness.
import { execFileSync } from 'node:child_process';
import { appendFileSync, readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

const documentationFiles = new Set([
  'AGENTS.md', '.github/copilot-instructions.md', '.github/pull_request_template.md',
  'docs/agents/task-routing.json',
]);
export function isDocumentationPath(path) {
  return documentationFiles.has(path) || (path.startsWith('docs/') && path.endsWith('.md'));
}
function checkedSha(value) {
  if (typeof value !== 'string' || !/^[a-f0-9]{40}$/.test(value) || /^0+$/.test(value))
    throw new Error('Missing or invalid Git revision.');
  return value;
}
function git(cwd, args) {
  return execFileSync('git', args, { cwd, encoding: 'utf8', maxBuffer: 16 * 1024 * 1024,
    stdio: ['ignore', 'pipe', 'pipe'] });
}
function isAncestor(cwd, before, after) {
  try { git(cwd, ['merge-base', '--is-ancestor', checkedSha(before), checkedSha(after)]); return true; }
  catch (error) { if (error.status === 1) return false; throw error; }
}
function changedPaths(cwd, before, after, mergeBase = false) {
  const range = checkedSha(before) + (mergeBase ? '...' : '..') + checkedSha(after);
  // No rename detection: both old and new paths must be classified. NUL handles odd names.
  return git(cwd, ['diff', '--name-only', '-z', '--no-renames', range, '--'])
    .split('\0').filter(Boolean);
}
export function needsRuntimeBuild(eventName, event, cwd = process.cwd()) {
  try {
    let paths;
    if (eventName === 'pull_request') {
      paths = changedPaths(cwd, event.pull_request?.base?.sha, event.pull_request?.head?.sha, true);
    } else if (eventName === 'push' && !event.forced &&
      isAncestor(cwd, event.before, event.after)) {
      paths = changedPaths(cwd, event.before, event.after);
    } else {
      // Manual, unknown, forced or otherwise unusual events always build.
      return true;
    }
    return paths.length === 0 || paths.some(path => !isDocumentationPath(path));
  } catch {
    // Missing commits, shallow histories and failed comparisons must never skip tests.
    return true;
  }
}
// Benchmark selection is separate from runtime/deployment input classification.
// Only ordinary, recognized PR inputs may use the already verified regression modes.
const benchmarkHarnessPath = path => path.startsWith('scripts/benchmark-') ||
  path.startsWith('scripts/validate-editor-regression') || path.startsWith('scripts/validate-dsp-regression') ||
  path.startsWith('tests/benchmarks/') || path === 'scripts/ci-change-scope.mjs' ||
  path === 'tests/tooling/CiChangeScope.test.mjs' || path === '.github/workflows/build-and-deploy.yml';
const recognizedRuntimePath = path => path.startsWith('src/') || path.startsWith('tests/') ||
  path.startsWith('tools/agent-kit/');
// Conservative automatic diagnostic selection for editor hot paths and audio/DSP.
const performanceSensitivePath = path => [
  'src/Orbit.UI/Components/Canvas/', 'src/Orbit.Presentation/Canvas/',
  'src/Orbit.Audio/', 'src/Orbit.Infrastructure/wwwroot/audio/'
].some(prefix => path.startsWith(prefix));

export function benchmarkMode(eventName, event, cwd = process.cwd()) {
  if (eventName !== 'pull_request') return 'full';
  try {
    const paths = changedPaths(cwd, event.pull_request?.base?.sha, event.pull_request?.head?.sha, true);
    if (!paths.length) return 'full';
    if (paths.some(benchmarkHarnessPath)) return 'both';
    const labels = event.pull_request?.labels;
    if (!Array.isArray(labels) || labels.some(label => typeof label?.name !== 'string')) return 'full';
    if (labels.some(label => ['performance-diagnostics', 'full-verification'].includes(label.name))) return 'full';
    const runtimePaths = paths.filter(path => !isDocumentationPath(path));
    if (!runtimePaths.length || runtimePaths.some(path => !recognizedRuntimePath(path) || performanceSensitivePath(path)))
      return 'full';
    return 'regression';
  } catch {
    // Missing history or unusual inputs must never suppress diagnostics.
    return 'full';
  }
}

// The only initial editor-suite exemption is the independently executed offline
// Python package and its tests. Shared native contracts and the exporter are NOT exempt.
const isolatedOfflinePythonPath = path =>
  /^tools\/agent-kit\/(orbit2\/[A-Za-z_][A-Za-z0-9_]*\.py|tests\/(test_[A-Za-z0-9_]+|generate_native_fixture)\.py)$/.test(path);

export function editorSuiteDecision(eventName, event, cwd = process.cwd()) {
  // Any full/both benchmark mode, explicit checkpoint, non-PR or uncertain input
  // requires the editor suite. The label can only broaden coverage, never reduce it.
  if (benchmarkMode(eventName, event, cwd) !== 'regression')
    return { required: true, reason: 'full-or-parity-verification' };
  try {
    const paths = changedPaths(cwd, event.pull_request?.base?.sha, event.pull_request?.head?.sha, true);
    const runtimePaths = paths.filter(path => !isDocumentationPath(path));
    if (runtimePaths.length > 0 && runtimePaths.every(isolatedOfflinePythonPath))
      return { required: false, reason: 'isolated-offline-agent-kit-python' };
  } catch { /* Fail closed on any unavailable or malformed change data. */ }
  return { required: true, reason: 'editor-impact-not-excluded' };
}

export function canDeploy(built, latest, cwd = process.cwd()) {
  checkedSha(built); checkedSha(latest);
  if (!isAncestor(cwd, built, latest)) return false;
  return changedPaths(cwd, built, latest).every(isDocumentationPath);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [mode, built, latest] = process.argv.slice(2);
  if (mode === 'build') {
    let runtime = true, benchmark = 'full';
    let editor = { required: true, reason: 'unavailable-change-data' };
    try {
      const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
      runtime = needsRuntimeBuild(process.env.GITHUB_EVENT_NAME, event);
      benchmark = benchmarkMode(process.env.GITHUB_EVENT_NAME, event);
      editor = editorSuiteDecision(process.env.GITHUB_EVENT_NAME, event);
    } catch { /* Conservative full build on an unavailable event. */ }
    appendFileSync(process.env.GITHUB_OUTPUT, 'runtime=' + runtime + '\nbenchmark=' + benchmark +
      '\neditor=' + editor.required + '\neditor_reason=' + editor.reason + '\n');
    console.log(runtime ? 'Full runtime build required; benchmark mode: ' + benchmark :
      'Documentation-only change: skip runtime build.');
    if (runtime) console.log('Editor benchmark suite: ' + (editor.required ? 'required' : 'omitted') + ' (' + editor.reason + ').');
  } else if (mode === 'deploy') {
    // Unexpected Git errors fail the deploy job rather than publishing an unverified artifact.
    const deploy = canDeploy(built, latest);
    appendFileSync(process.env.GITHUB_OUTPUT, 'deploy=' + deploy + '\n');
    console.log(deploy ? 'Built runtime inputs match current main.' : 'Skipping stale Pages build.');
  } else {
    throw new Error('Expected build or deploy mode.');
  }
}
