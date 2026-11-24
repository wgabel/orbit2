#!/usr/bin/env node
import { readFileSync, lstatSync, realpathSync } from 'node:fs';
import { resolve, relative, isAbsolute } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';
import { validateRouting, extractReport, validateReport } from './agent-compliance.mjs';

const root = realpathSync(fileURLToPath(new URL('..', import.meta.url)));
function fileExists(path) {
  try {
    const target = resolve(root, path);
    const rel = relative(root, realpathSync(target));
    return !isAbsolute(rel) && rel !== '..' && !rel.startsWith('../') &&
      !rel.startsWith('..\\') && lstatSync(target).isFile();
  } catch { return false; }
}
try {
  const config = JSON.parse(readFileSync(resolve(root, 'docs/agents/task-routing.json'), 'utf8'));
  const errors = validateRouting(config, fileExists);
  if (!errors.length && process.env.GITHUB_EVENT_NAME === 'pull_request') {
    const event = JSON.parse(readFileSync(process.env.GITHUB_EVENT_PATH, 'utf8'));
    const pr = event.pull_request;
    if (![pr?.base?.sha, pr?.head?.sha].every(sha => typeof sha === 'string' && /^[a-f0-9]{40}$/.test(sha))) {
      throw Error('Missing or invalid pull request commit identities.');
    }
    const changedPaths = execFileSync('git', ['diff', '--name-only', '-z', '--no-renames',
      pr.base.sha + '...' + pr.head.sha], { cwd: root, encoding: 'utf8', maxBuffer: 16 * 1024 * 1024 }).split('\0').filter(Boolean);
    errors.push(...validateReport(config, extractReport(pr.body), changedPaths));
  }
  if (errors.length) {
    for (const error of errors) console.error(error);
    process.exitCode = 1;
  } else console.log('Agent routing and applicable PR declarations passed structural checks.');
} catch (error) {
  console.error('Agent compliance check failed:', error.message);
  process.exitCode = 1;
}
