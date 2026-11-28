import test from 'node:test';
import assert from 'node:assert/strict';
import { execFileSync } from 'node:child_process';
import { mkdtempSync, mkdirSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';
import { editorSuiteDecision, needsRuntimeBuild, benchmarkMode } from '../../scripts/ci-change-scope.mjs';

function fixture(t) {
  const cwd = mkdtempSync(join(tmpdir(), 'orbit-ci-impact-'));
  t.after(() => rmSync(cwd, { recursive: true, force: true }));
  const git = (...args) => execFileSync('git', args, { cwd, encoding: 'utf8', stdio: ['ignore', 'pipe', 'pipe'] }).trim();
  git('init', '-q', '-b', 'main');
  git('config', 'user.email', 'test@example.invalid');
  git('config', 'user.name', 'CI fixture');
  function put(path) {
    const target = join(cwd, path);
    mkdirSync(dirname(target), { recursive: true });
    writeFileSync(target, 'fixture');
  }
  function commit() {
    git('add', '-A');
    git('commit', '-qm', 'fixture');
    return git('rev-parse', 'HEAD');
  }
  put('README.md');
  const base = commit();
  return { cwd, git, put, commit, base };
}
const pr = (base, head, labels = []) => ({ pull_request: { base: { sha: base }, head: { sha: head },
  labels: labels.map(name => ({ name })) } });

test('audited independent source and test scopes omit only editor benchmarks', t => {
  const { cwd, git, put, commit, base } = fixture(t);
  const cases = [
    [['tools/agent-kit/exporter/Program.cs', 'tools/agent-kit/orbit2/patch.py',
      'tests/Orbit.Presentation.Tests/AgentKitPatchContractTests.cs'], 'isolated-offline-agent-kit-native'],
    [['tests/Orbit.Domain.Tests/MacroDependencyProjectionTests.cs',
      'tests/Orbit.Presentation.Tests/CanvasStateServiceTests.cs'], 'isolated-native-unit-tests'],
    [['tests/compiler/Resonator.test.mjs'], 'isolated-compiler-tests']
  ];
  for (const [paths, reason] of cases) {
    git('checkout', '-qB', 'case', base);
    for (const path of paths) put(path);
    put('docs/guide.md');
    const head = commit(), event = pr(base, head);
    assert.equal(needsRuntimeBuild('pull_request', event, cwd), true);
    assert.equal(benchmarkMode('pull_request', event, cwd), 'regression');
    assert.deepEqual(editorSuiteDecision('pull_request', event, cwd), { required: false, reason });
    for (const label of ['full-verification', 'performance-diagnostics'])
      assert.equal(editorSuiteDecision('pull_request', pr(base, head, [label]), cwd).required, true);
    assert.equal(editorSuiteDecision('push', { before: base, after: head }, cwd).required, true);
  }
});

test('each shared runtime, fixture, project, browser or cross-family change restores full editor coverage', t => {
  const { cwd, git, put, commit, base } = fixture(t);
  put('tests/compiler/Resonator.test.mjs');
  const isolated = commit();
  for (const path of ['src/Orbit.Audio/Compilation/FaustDampedMode.cs',
    'src/Orbit.Domain/Graph.cs', 'src/Orbit.Application/PatchSession.cs',
    'src/Orbit.Presentation/Canvas/CanvasStateService.cs', 'src/Orbit.UI/Components/Canvas/Editor.razor',
    'tests/Orbit.Audio.Probe/EditorBenchmarkFixtures.cs', 'tests/Orbit.Audio.Probe/Program.cs',
    'tests/Orbit.Domain.Tests/Orbit.Domain.Tests.csproj', 'tests/compiler/helper.mjs',
    'tests/browser/PublishedPlaybackSmoke.mjs', 'tests/benchmarks/EditorBenchmarkSuite.test.mjs',
    'tools/agent-kit/exporter/Program.cs', 'Directory.Build.props', 'unknown.file']) {
    git('checkout', '-qB', 'case', isolated);
    put(path);
    const head = commit();
    assert.equal(editorSuiteDecision('pull_request', pr(base, head), cwd).required, true, path);
  }
});

test('unreviewed exporter and test metadata paths do not acquire an exemption', t => {
  const { cwd, git, put, commit, base } = fixture(t);
  for (const path of ['tools/agent-kit/exporter/Unknown.cs', 'tools/agent-kit/exporter/Orbit.AgentKit.Exporter.csproj',
    'tests/Orbit.Domain.Tests/Helper.cs', 'tests/Orbit.Presentation.Tests/nested/NewTests.cs',
    'tests/compiler/helpers/Example.test.mjs', 'tests/audio/AudioEngine.test.mjs']) {
    git('checkout', '-qB', 'case', base);
    put(path);
    const head = commit();
    assert.equal(editorSuiteDecision('pull_request', pr(base, head), cwd).required, true, path);
  }
});
