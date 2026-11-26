import test from 'node:test';
import assert from 'node:assert/strict';
import { analyzeDocumentation, markdownAnchors, markdownLinks } from '../../scripts/documentation-coverage.mjs';

const ROOT = 'AGENTS.md', ROUTING = 'docs/agents/task-routing.json';
function fixture() {
  const documents = new Map([
    [ROOT, '[Index](docs/README.md) · [Routes](docs/agents/task-routing.json)'],
    ['docs/README.md', '# Index\n[Owner](owner.md#topic)\n[Archive](archive/old.md)'],
    ['docs/owner.md', '# Topic\n[Back](README.md)'],
    ['docs/archive/old.md', '# Historical'],
    ['docs/redirect.md', '[Owner](owner.md)'],
    ['.github/copilot-instructions.md', '[Start](../AGENTS.md)'],
    ['.github/pull_request_template.md', '# Template'],
  ]);
  documents.set('docs/README.md', documents.get('docs/README.md') + '\n[Template](../.github/pull_request_template.md)');
  const config = { alwaysRequired: [ROOT, ROUTING, 'docs/README.md'],
    routes: [{ id: 'owner', required: ['docs/owner.md'], recommended: [], reference: [] }],
    coverage: { schemaVersion: 1, maxActiveDepth: 4, exceptions: [
      { path: 'docs/archive/old.md', kind: 'archive', reason: 'Historical' },
      { path: 'docs/redirect.md', kind: 'redirect', target: 'docs/owner.md', reason: 'Compatibility' },
      { path: '.github/copilot-instructions.md', kind: 'alternate-entry', target: ROOT, reason: 'Native entry' },
      { path: '.github/pull_request_template.md', kind: 'template', reason: 'Authoring template' }
    ], scenarios: [{ task: 'Do owner work', routes: ['owner'], required: ['docs/owner.md'] }] }
  };
  return { documents, files: [...documents.keys(), ROUTING], config };
}
const check = f => analyzeDocumentation(f);
const hasError = (audit, fragment) => assert.ok(audit.errors.some(error => error.includes(fragment)), JSON.stringify(audit.errors));

test('tracked files have classified forward and reverse paths; explicit alternate entries may point inward', () => {
  const audit = check(fixture());
  assert.deepEqual(audit.errors, []);
  const owner = audit.report.find(x => x.file === 'docs/owner.md');
  assert.deepEqual(owner.navigation, [ROOT, 'docs/README.md', 'docs/owner.md']);
  assert.ok(owner.reversePath[0] === ROOT && owner.reversePath.at(-1) === owner.file);
  assert.equal(audit.scenarioCount, 1);
  assert.equal(audit.report.find(x => x.file === 'docs/redirect.md').priority, 'redirect');
});

test('disconnected cycle and route-only documents do not pass human navigation', () => {
  const f = fixture();
  f.documents.set('docs/README.md', '[Archive](archive/old.md)\n[Template](../.github/pull_request_template.md)');
  f.documents.set('docs/owner.md', '# Topic\n[Loop](loop.md)');
  f.documents.set('docs/loop.md', '[Owner](owner.md)');
  f.files.push('docs/loop.md');
  f.config.routes[0].reference.push('docs/loop.md');
  const audit = check(f);
  hasError(audit, 'No Markdown navigation path from AGENTS.md: docs/owner.md');
  hasError(audit, 'No Markdown navigation path from AGENTS.md: docs/loop.md');
});

test('linked but unregistered files fail and route priority cannot hide a mandatory scenario gap', () => {
  const f = fixture();
  f.documents.set('docs/extra.md', '# New document');
  f.files.push('docs/extra.md');
  f.documents.set('docs/README.md', f.documents.get('docs/README.md') + '\n[New](extra.md)');
  f.config.routes[0].required = [];
  f.config.routes[0].recommended = ['docs/owner.md'];
  const audit = check(f);
  hasError(audit, 'No explicit routing owner: docs/extra.md');
  hasError(audit, 'Scenario lacks mandatory document: Do owner work / docs/owner.md');
});

test('route JSON must be linked from root, tracked path and anchor checks are case-sensitive', () => {
  const f = fixture();
  f.documents.set(ROOT, '[Index](docs/README.md)');
  f.documents.set('docs/README.md', f.documents.get('docs/README.md') + '\n[Broken](Owner.md) [Bad anchor](owner.md#wrong) [Escape](../../bad.md)');
  const audit = check(f);
  hasError(audit, 'Routing configuration is not linked');
  hasError(audit, 'Owner.md — target not found');
  hasError(audit, 'owner.md#wrong — anchor not found');
  hasError(audit, '../../bad.md — outside repository');
});

test('explicit classifications cannot hide new live docs, stale files or redirects with no real target link', () => {
  const f = fixture();
  f.config.coverage.exceptions.push({ path: 'docs/owner.md', kind: 'archive', reason: 'invalid' });
  f.config.coverage.exceptions.push({ path: 'docs/missing.md', kind: 'archive', reason: 'stale' });
  f.documents.set('docs/redirect.md', '# No redirect link');
  const audit = check(f);
  hasError(audit, 'Invalid or stale coverage classification');
  hasError(audit, 'Invalid discovery redirect');
});

test('markdown extraction supports titles, references, HTML, code exclusion and duplicate heading anchors', () => {
  const text = '# Topic\n# Topic\n# A **bold** title\n\nSetext title\n---\n<a id="custom"></a>\n[Owner](owner.md#topic "Title")\n[Ref][id]\n[id]: other.md#custom\n[unused]: hidden.md\n<a href="local.md">Local</a>\n[x](<space name.md>)\n[x](nested(foo).md)\n~~~md\n# Not a heading\n[example](missing.md)\n~~~\n<!-- [comment](bad.md) -->\n' + String.fromCharCode(96) + '[inline](bad.md)' + String.fromCharCode(96);
  assert.deepEqual([...markdownLinks(text)].sort(), ['local.md','nested(foo).md','other.md#custom','owner.md#topic','space name.md'].sort());
  const anchors = markdownAnchors(text);
  for (const anchor of ['topic','topic-1','a-bold-title','setext-title','custom']) assert.ok(anchors.has(anchor), anchor);
  assert.ok(!anchors.has('not-a-heading'));
});

test('URL-escaped names, same-repository links, non-Markdown assets and external links are handled', () => {
  const f = fixture();
  f.documents.set('docs/owner.md', '# Topic\n[Ref](https://github.com/wgabel/orbit2/blob/main/docs/README.md#index)\n[Other](a%20b.md#żółć)\n[Asset](../data.bin)\n[External](https://example.com/404)');
  f.documents.set('docs/a b.md', '# Żółć');
  f.files.push('docs/a b.md','data.bin');
  f.config.routes[0].reference.push('docs/a b.md');
  assert.deepEqual(check(f).errors, []);
});
test('unread tracked Markdown fails closed', () => {
  const f = fixture();
  f.files.push('docs/unread.md');
  hasError(check(f), 'Unreadable tracked Markdown: docs/unread.md');
});
