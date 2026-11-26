#!/usr/bin/env node
// The same tracked-document audit used by agent compliance; no dependencies.
import { readFileSync, realpathSync } from 'node:fs';
import { resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { auditRepositoryDocumentation, formatCoverageSummary } from './documentation-coverage.mjs';

const root = realpathSync(fileURLToPath(new URL('..', import.meta.url)));
try {
  const config = JSON.parse(readFileSync(resolve(root, 'docs/agents/task-routing.json'), 'utf8'));
  const audit = auditRepositoryDocumentation(root, config);
  if (process.argv.includes('--report')) console.log(JSON.stringify(audit.report, null, 2));
  for (const item of audit.report) {
    if (!item.file.includes('/') && item.file !== 'AGENTS.md') audit.errors.push('Root Markdown forbidden except AGENTS.md: ' + item.file);
  }
  for (const warning of audit.warnings) console.warn(warning);
  for (const error of audit.errors) console.error(error);
  console.log(formatCoverageSummary(audit));
  if (audit.errors.length) process.exitCode = 1;
} catch (error) {
  console.error('Documentation audit failed:', error.message);
  process.exitCode = 1;
}
