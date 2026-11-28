import test from 'node:test';
import assert from 'node:assert/strict';
import { requireVerification, verifyManifest } from '../../scripts/ci-verification.mjs';

const full = { prepare: 'success', playback: 'success', editor: 'success', runtimeRequired: 'true', editorRequired: 'true' };
const isolated = { ...full, editor: 'skipped', editorRequired: 'false' };
const docs = { ...full, playback: 'skipped', editor: 'skipped', runtimeRequired: 'false' };

test('aggregate gate accepts full, audited omission and documentation-only success', () => {
  for (const result of [full, isolated, docs]) assert.doesNotThrow(() => requireVerification(result));
});
test('aggregate gate rejects failed, skipped or cancelled required jobs and invalid outputs', () => {
  for (const key of ['prepare', 'playback', 'editor']) {
    for (const status of ['failure', 'cancelled', 'skipped', '']) {
      assert.throws(() => requireVerification({ ...full, [key]: status }));
    }
  }
  for (const key of ['runtimeRequired', 'editorRequired']) {
    for (const value of ['', undefined, 'unknown']) assert.throws(() => requireVerification({ ...full, [key]: value }));
  }
  assert.throws(() => requireVerification({ ...isolated, editor: 'failure' }));
  assert.throws(() => requireVerification({ ...docs, prepare: 'failure' }));
});
test('manifest binds published revision and CI build number', () => {
  const revision = 'a'.repeat(40);
  const manifest = { commit: revision, build: '123' };
  assert.doesNotThrow(() => verifyManifest(manifest, revision, '123'));
  assert.throws(() => verifyManifest(manifest, 'b'.repeat(40), '123'));
  assert.throws(() => verifyManifest(manifest, revision, '124'));
  assert.throws(() => verifyManifest(manifest, '', '123'));
});
