#!/usr/bin/env node
// Validate local Markdown file targets without downloading dependencies.
// Anchors are intentionally not checked: GitHub's heading algorithm, HTML anchors,
// and duplicate generated slugs need a separate semantic review when headings move.
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
const broken = [];
for (const file of markdown) {
  const source = readFileSync(file, 'utf8');
  // Markdown inline links and image links. Exclude external and fragment-only URLs.
  for (const match of source.matchAll(/!?\[[^\]]*\]\(<?([^\s)]+)>?(?:\s+["'][^"']*["'])?\)/g)) {
    const href = match[1];
    if (/^(?:[a-z][a-z0-9+.-]*:|\/\/|#)/i.test(href)) continue;
    let name = href.split('#')[0].split('?')[0];
    try { name = decodeURIComponent(name); } catch { broken.push([file, href, 'invalid URL escaping']); continue; }
    const dest = resolve(dirname(file), name);
    const relToTarget = relative(root, dest);
    if (relToTarget === '..' || relToTarget.startsWith('../') || relToTarget.startsWith('..\\')) { broken.push([file, href, 'outside repository']); continue; }
    if (!existsSync(dest)) broken.push([file, href, 'target not found']);
  }
}
if (broken.length) {
  for (const [file, href, issue] of broken) console.error(`${relative(root, file)}: ${href} — ${issue}`);
  console.error(`Found ${broken.length} invalid local documentation link(s).`);
  process.exitCode = 1;
} else {
  console.log(`Checked local link destinations in ${markdown.length} Markdown files.`);
}
