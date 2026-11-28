import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

export function verifyManifest(manifest, revision, runNumber) {
  assert.match(revision, /^[a-f0-9]{40}$/);
  assert.match(runNumber, /^[1-9]\d*$/);
  assert.equal(manifest.commit, revision, 'Published revision mismatch');
  assert.equal(String(manifest.build), runNumber, 'Published build number mismatch');
}

export function requireVerification({ prepare, playback, editor, runtimeRequired, editorRequired }) {
  assert.equal(prepare, 'success', 'Preparation failed or did not run');
  assert.ok(['true', 'false'].includes(runtimeRequired), 'Missing or invalid runtime decision');
  assert.ok(['true', 'false'].includes(editorRequired), 'Missing or invalid editor decision');
  assert.equal(playback, runtimeRequired === 'true' ? 'success' : 'skipped', 'Published playback verification did not match required coverage');
  assert.equal(editor, runtimeRequired === 'true' && editorRequired === 'true' ? 'success' : 'skipped',
    'Editor verification did not match required coverage');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const [mode, file] = process.argv.slice(2);
  if (mode === 'manifest') {
    verifyManifest(JSON.parse(readFileSync(file, 'utf8')), process.env.GITHUB_SHA, process.env.GITHUB_RUN_NUMBER);
  } else if (mode === 'gate') {
    requireVerification({ prepare: process.env.PREPARE_RESULT, playback: process.env.PLAYBACK_RESULT,
      editor: process.env.EDITOR_RESULT, runtimeRequired: process.env.RUNTIME_REQUIRED, editorRequired: process.env.EDITOR_REQUIRED });
    console.log('All applicable verification jobs succeeded; intentional omissions confirmed.');
  } else throw new Error('Expected manifest or gate mode');
}
