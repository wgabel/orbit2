#!/usr/bin/env node
// Validate local Markdown file targets without downloading dependencies.
// Validate local paths and selected migrated Markdown anchors; other historical
// heading syntax requires manual review when changing titles.
import { readFileSync, readdirSync, existsSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
import process from 'node:process';

const root = resolve(import.meta.dirname, '..');
const ignore = new Set(['.git', 'node_modules', 'bin', 'obj', 'publish', 'dist']);
const markdown = [];
const visit = (dir) => {
  for (const item of readdirSync(dir, { withFileTypes: true })) {
    if (item.isDirectory()) { if (!ignore.has(item.name)) visit(resolve(dir, item.name)); }
    else if (item.isFile() && item.name.endsWith('.md')) markdown.push(resolve(dir, item.name));
  }
};
visit(root);
const rootMarkdown = readdirSync(root, { withFileTypes: true }).filter(item => item.isFile() && item.name.toLowerCase().endsWith('.md'));
if (rootMarkdown.length) {
  for (const file of rootMarkdown) console.error(`Root Markdown forbidden; move to docs/: ${file.name}`);
  process.exitCode = 1;
}

const checkAnchorsFor = new Set([
  'README.md', 'ORBIT2-ARCHITECTURE.md',
  'docs/architecture/overview.md', 'docs/architecture/task-routing.md',
  'docs/usage/README.md', 'docs/usage/task-index.md', 'docs/usage/feature-history.md',
  'docs/examples/README.md', 'docs/examples/task-index.md',
  'docs/examples/production-workflow.md', 'docs/examples/foundations/catalogue.md',
  'docs/getting-started/installation.md', 'docs/getting-started/development.md',
  'docs/getting-started/deployment.md',
  'docs/archive/legacy-readme.md',
  ...markdown.map(file => relative(root, file)).filter(path => /^docs\/archive\/legacy-(?!readme\.md$).+\.md$/.test(path)),
]);
const anchorCache = new Map();
function anchorsFor(file) {
  if (anchorCache.has(file)) return anchorCache.get(file);
  const content = readFileSync(file, 'utf8');
  const found = new Set();
  const repeats = new Map();
  // Preserve explicit HTML anchor IDs used by the original architecture.
  for (const match of content.matchAll(/<a\s+id=["']([^"']+)["']/g)) found.add(match[1]);
  for (const match of content.matchAll(/^#{1,6}\s+(.+?)(?:\s+#+)?\s*$/gm)) {
    const clean = match[1].replace(/<[^>]*>/g, '').replace(/\[([^\]]+)\]\([^)]+\)/g, '$1').replace(/[*_\x60~]/g, '');
    const slug = clean.toLowerCase().replace(/[^\p{L}\p{N}\p{M}_\- ]/gu, '').replace(/ /g, '-');
    const count = repeats.get(slug) || 0;
    found.add(count ? slug + '-' + count : slug);
    repeats.set(slug, count + 1);
  }
  anchorCache.set(file, found);
  return found;
}

const broken = [];
for (const file of markdown) {
  const source = readFileSync(file, 'utf8');
  // Markdown inline links and image links. External links are skipped;
  // fragment-only links resolve to the referring Markdown file.
  for (const match of source.matchAll(/!?\[[^\]]*\]\(<?([^\s)]+)>?(?:\s+["'][^"']*["'])?\)/g)) {
    const href = match[1];
    if (/^(?:[a-z][a-z0-9+.-]*:|\/\/)/i.test(href)) continue;
    let name = href.split('#')[0].split('?')[0];
    if (!name) name = '.';
    try { name = decodeURIComponent(name); } catch { broken.push([file, href, 'invalid URL escaping']); continue; }
    const dest = name === '.' ? file : resolve(dirname(file), name);
    const relToTarget = relative(root, dest);
    if (relToTarget === '..' || relToTarget.startsWith('../') || relToTarget.startsWith('..\\')) { broken.push([file, href, 'outside repository']); continue; }
    if (!existsSync(dest)) broken.push([file, href, 'target not found']);
    else {
      const fragment = href.includes('#') ? href.slice(href.indexOf('#') + 1).split('?')[0] : '';
      if (fragment && checkAnchorsFor.has(relative(root, dest)) && dest.endsWith('.md')) {
        let decoded = fragment;
        try { decoded = decodeURIComponent(fragment); }
        catch { broken.push([file, href, 'invalid anchor escaping']); continue; }
        if (!anchorsFor(dest).has(decoded)) broken.push([file, href, 'anchor not found']);
      }
    }
  }
}
if (broken.length) {
  for (const [file, href, issue] of broken) console.error(`${relative(root, file)}: ${href} — ${issue}`);
  console.error(`Found ${broken.length} invalid local documentation link(s).`);
  process.exitCode = 1;
} else {
  console.log(`Checked local link destinations and migrated anchors in ${markdown.length} Markdown files.`);
}
