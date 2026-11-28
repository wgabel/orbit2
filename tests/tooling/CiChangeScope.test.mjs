import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, writeFileSync, renameSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { isDocumentationPath, needsRuntimeBuild, canDeploy, benchmarkMode, editorSuiteDecision } from '../../scripts/ci-change-scope.mjs';

function fixture(t) {
  const cwd = mkdtempSync(join(tmpdir(), 'orbit-ci-scope-'));
  t.after(() => rmSync(cwd, { recursive: true, force: true }));
  const git = (...args) => execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  git('init', '-q', '-b', 'main');
  git('config', 'user.email', 'test@example.invalid');
  git('config', 'user.name', 'CI fixture');
  function put(path, value) {
    const target = join(cwd, path);
    mkdirSync(dirname(target), { recursive: true });
    writeFileSync(target, value);
  }
  function commit() {
    git('add', '-A');
    git('commit', '-qm', 'fixture');
    return git('rev-parse', 'HEAD');
  }
  put('src/editor.cs', 'initial');
  put('docs/guide.md', 'initial');
  const base = commit();
  return { cwd, git, put, commit, base };
}
const push = (before, after, forced = false) => ({ before, after, forced });
const pr = (base, head) => ({ pull_request: { base: { sha: base }, head: { sha: head } } });

test('only known non-runtime documentation is excluded', () => {
  for (const path of ['AGENTS.md', 'docs/guide.md', 'docs/nested/a.md', '.github/copilot-instructions.md',
    '.github/pull_request_template.md', 'docs/agents/task-routing.json']) assert.equal(isDocumentationPath(path), true, path);
  for (const path of ['src/guide.md', 'docs/example.orbit2.json', 'docs/unknown.json', 'tests/audio/test.mjs',
    '.github/workflows/build-and-deploy.yml', 'scripts/build.mjs', 'Orbit.slnx', 'unknown.file'])
    assert.equal(isDocumentationPath(path), false, path);
});
test('documentation-only PR and push skip runtime; mixed and code changes build', t => {
  const { cwd, put, commit, base } = fixture(t);
  put('docs/guide.md', 'updated');
  put('AGENTS.md', 'instructions');
  const docs = commit();
  assert.equal(needsRuntimeBuild('pull_request', pr(base, docs), cwd), false);
  assert.equal(needsRuntimeBuild('push', push(base, docs), cwd), false);
  put('src/editor.cs', 'changed');
  const code = commit();
  assert.equal(needsRuntimeBuild('pull_request', pr(base, code), cwd), true);
  assert.equal(needsRuntimeBuild('push', push(docs, code), cwd), true);
  put('docs/guide.md', 'closeout');
  const closeout = commit();
  assert.equal(needsRuntimeBuild('pull_request', pr(base, closeout), cwd), true);
  assert.equal(needsRuntimeBuild('push', push(code, closeout), cwd), false);
});
test('PR uses merge base; base branch runtime edits are not PR changes', t => {
  const { cwd, git, put, commit, base } = fixture(t);
  git('checkout', '-qb', 'docs-branch');
  put('docs/guide.md', 'docs branch');
  const head = commit();
  git('checkout', '-q', 'main');
  put('src/editor.cs', 'new main');
  const main = commit();
  assert.equal(needsRuntimeBuild('pull_request', pr(main, head), cwd), false);
});
test('rename across runtime boundary, deletions and non-Markdown fixtures build', t => {
  const { cwd, put, commit, base } = fixture(t);
  renameSync(join(cwd, 'src/editor.cs'), join(cwd, 'docs/editor.md'));
  const renamed = commit();
  assert.equal(needsRuntimeBuild('push', push(base, renamed), cwd), true);
  put('docs/reference.orbit2.json', '{}');
  const fixtureSha = commit();
  assert.equal(needsRuntimeBuild('push', push(renamed, fixtureSha), cwd), true);
  rmSync(join(cwd, 'docs/reference.orbit2.json'));
  const deleted = commit();
  assert.equal(needsRuntimeBuild('push', push(fixtureSha, deleted), cwd), true);
});
test('manual, invalid or missing history, force push and empty changes build', t => {
  const { cwd, base } = fixture(t);
  assert.equal(needsRuntimeBuild('workflow_dispatch', {}, cwd), true);
  assert.equal(needsRuntimeBuild('push', push(base, base), cwd), true);
  assert.equal(needsRuntimeBuild('push', push(base, base, true), cwd), true);
  assert.equal(needsRuntimeBuild('push', push('0'.repeat(40), base), cwd), true);
  assert.equal(needsRuntimeBuild('pull_request', pr('1'.repeat(40), base), cwd), true);
  assert.equal(needsRuntimeBuild('pull_request', {}, cwd), true);
});
test('deployment permits intervening docs but rejects new runtime inputs and divergent main', t => {
  const { cwd, git, put, commit, base } = fixture(t);
  put('src/editor.cs', 'built');
  const built = commit();
  put('docs/guide.md', 'later docs');
  const docs = commit();
  assert.equal(canDeploy(built, built, cwd), true);
  assert.equal(canDeploy(built, docs, cwd), true);
  put('src/editor.cs', 'newer runtime');
  const code = commit();
  assert.equal(canDeploy(built, code, cwd), false);
  assert.equal(canDeploy(code, built, cwd), false);
  git('checkout', '-qb', 'diverged', base);
  put('docs/guide.md', 'other history');
  const divergent = commit();
  assert.equal(canDeploy(built, divergent, cwd), false);
  assert.throws(() => canDeploy(built, '0'.repeat(40), cwd));
});

const labeledPr = (base, head, labels = []) => ({ pull_request: { base: { sha: base }, head: { sha: head },
  labels: labels.map(name => ({ name })) } });
test('ordinary PRs use verified regression suites, while main/manual and explicit diagnostics use full', t => {
  const { cwd, put, commit, base } = fixture(t);
  put('src/editor.cs', 'changed');
  put('docs/guide.md', 'updated');
  const head = commit();
  assert.equal(benchmarkMode('pull_request', labeledPr(base, head), cwd), 'regression');
  assert.equal(benchmarkMode('pull_request', labeledPr(base, head, ['performance-diagnostics']), cwd), 'full');
  assert.equal(benchmarkMode('push', push(base, head), cwd), 'full');
  assert.equal(benchmarkMode('workflow_dispatch', {}, cwd), 'full');
});
test('benchmark harness changes require parity even for mixed runtime changes', t => {
  const { cwd, put, commit, base } = fixture(t);
  put('scripts/benchmark-editor.mjs', 'harness');
  put('src/Orbit.Audio/Engine.cs', 'changed');
  const head = commit();
  assert.equal(benchmarkMode('pull_request', labeledPr(base, head), cwd), 'both');
  assert.equal(benchmarkMode('pull_request', labeledPr(base, head, ['performance-diagnostics']), cwd), 'both');
});
test('performance-sensitive, unknown, missing and malformed inputs fail closed to full diagnostics', t => {
  const { cwd, put, commit, base } = fixture(t);
  put('src/Orbit.Presentation/Canvas/Graph.cs', 'changed');
  const perf = commit();
  assert.equal(benchmarkMode('pull_request', labeledPr(base, perf), cwd), 'full');
  put('unknown.file', 'changed');
  const unknown = commit();
  assert.equal(benchmarkMode('pull_request', labeledPr(perf, unknown), cwd), 'full');
  assert.equal(benchmarkMode('pull_request', labeledPr(base, base), cwd), 'full');
  assert.equal(benchmarkMode('pull_request', labeledPr('1'.repeat(40), unknown), cwd), 'full');
  assert.equal(benchmarkMode('pull_request', pr(base, perf), cwd), 'full');
  assert.equal(benchmarkMode('unknown', {}, cwd), 'full');
});

test('isolated offline Python changes omit only the editor suite; checkpoint and diagnostics labels restore it', t => {
  const { cwd, put, commit, base } = fixture(t);
  put('tools/agent-kit/orbit2/patch.py', 'updated');
  put('tools/agent-kit/tests/test_builder.py', 'test');
  put('docs/guide.md', 'updated');
  const head = commit();
  const event = labeledPr(base, head);
  assert.equal(needsRuntimeBuild('pull_request', event, cwd), true);
  assert.equal(benchmarkMode('pull_request', event, cwd), 'regression');
  assert.deepEqual(editorSuiteDecision('pull_request', event, cwd),
    { required: false, reason: 'isolated-offline-agent-kit-python' });
  for (const label of ['full-verification', 'performance-diagnostics']) {
    const checkpoint = labeledPr(base, head, [label]);
    assert.equal(benchmarkMode('pull_request', checkpoint, cwd), 'full');
    assert.equal(editorSuiteDecision('pull_request', checkpoint, cwd).required, true);
  }
  assert.equal(editorSuiteDecision('push', push(base, head), cwd).required, true);
  assert.equal(editorSuiteDecision('workflow_dispatch', {}, cwd).required, true);
});
test('mixed, shared, unknown and harness changes never omit editor coverage', t => {
  const { cwd, git, put, commit, base } = fixture(t);
  put('tools/agent-kit/orbit2/patch.py', 'updated');
  const offline = commit();
  for (const path of ['src/Orbit.Domain/Graph.cs', 'src/Orbit.Audio/Compilation/FaustGraphCompiler.cs',
    'src/Orbit.UI/Components/Canvas/Editor.razor', 'tools/agent-kit/exporter/Orbit.AgentKit.Exporter.csproj',
    'tools/agent-kit/unknown.json', 'tools/agent-kit/orbit2/unknown.js', 'tests/browser/PublishedPlaybackSmoke.mjs',
    'scripts/benchmark-editor.mjs', '.github/workflows/build-and-deploy.yml', 'unknown.file']) {
    git('checkout', '-qB', 'mixed-scope', offline);
    put(path, 'changed');
    const head = commit();
    assert.equal(editorSuiteDecision('pull_request', labeledPr(base, head), cwd).required, true, path);
  }
  assert.equal(editorSuiteDecision('pull_request', labeledPr(base, offline), cwd).required, false);
});
test('missing history, malformed labels, empty diffs and rename across exemption boundary require editor suite', t => {
  const { cwd, put, commit, base } = fixture(t);
  put('tools/agent-kit/orbit2/patch.py', 'updated');
  const offline = commit();
  assert.equal(editorSuiteDecision('pull_request', pr(base, offline), cwd).required, true);
  assert.equal(editorSuiteDecision('pull_request', labeledPr('1'.repeat(40), offline), cwd).required, true);
  assert.equal(editorSuiteDecision('pull_request', labeledPr(base, base), cwd).required, true);
  assert.equal(editorSuiteDecision('unknown', {}, cwd).required, true);
  renameSync(join(cwd, 'src/editor.cs'), join(cwd, 'tools/agent-kit/orbit2/editor.py'));
  const renamed = commit();
  assert.equal(editorSuiteDecision('pull_request', labeledPr(offline, renamed), cwd).required, true);
});
