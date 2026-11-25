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
export function canDeploy(built, latest, cwd = process.cwd()) {
  checkedSha(built); checkedSha(latest);
  if (!isAncestor(cwd, built, latest)) return false;
  return changedPaths(cwd, built, latest).every(isDocumentationPath);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [mode, built, latest] = process.argv.slice(2);
  if (mode === 'build') {
    let runtime = true;
    try {
      const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
      runtime = needsRuntimeBuild(process.env.GITHUB_EVENT_NAME, event);
    } catch { /* Conservative full build on an unavailable event. */ }
    appendFileSync(process.env.GITHUB_OUTPUT, 'runtime=' + runtime + '\n');
    console.log(runtime ? 'Full runtime build required.' : 'Documentation-only change: skip runtime build.');
  } else if (mode === 'deploy') {
    // Unexpected Git errors fail the deploy job rather than publishing an unverified artifact.
    const deploy = canDeploy(built, latest);
    appendFileSync(process.env.GITHUB_OUTPUT, 'deploy=' + deploy + '\n');
    console.log(deploy ? 'Built runtime inputs match current main.' : 'Skipping stale Pages build.');
  } else {
    throw new Error('Expected build or deploy mode.');
  }
}
