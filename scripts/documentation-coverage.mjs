// Shared documentation inventory, link graph and coverage analysis.
// Only repository-owned Markdown and routing metadata are discovery vertices.
import { execFileSync } from 'node:child_process';
import { readFileSync, lstatSync } from 'node:fs';
import { resolve, posix } from 'node:path';

const ROOT = 'AGENTS.md';
const ROUTING = 'docs/agents/task-routing.json';
const markdown = path => /\.md$/i.test(path);
const nonempty = value => typeof value === 'string' && value.trim().length > 0;
const uniqueStrings = value => Array.isArray(value) && value.every(nonempty) && new Set(value).size === value.length;

function prose(source) {
  let fence = null;
  return source.replace(/<!--[\s\S]*?-->/g, '').split(/\r?\n/).map(line => {
    const match = line.match(/^\s*([~]{3,}|\x60{3,})(.*)$/);
    if (fence) {
      if (match && match[1][0] === fence[0] && match[1].length >= fence.length && !match[2].trim()) fence = null;
      return '';
    }
    if (match) { fence = match[1]; return ''; }
    return line;
  }).join('\n');
}

function withoutInlineCode(source) {
  return source.replace(/(\x60+)([^\n]*?)\1/g, match => ' '.repeat(match.length));
}

function labelKey(label) { return label.trim().replace(/\s+/g, ' ').toLowerCase(); }

// Inline links/images, reference links, local HTML links and same-repo autolinks.
// Ignore code and comments; unused reference definitions are not discovery edges.
export function markdownLinks(source) {
  let text = withoutInlineCode(prose(source));
  const links = [];
  const definitions = new Map();
  text = text.replace(/^[ \t]{0,3}\[([^\]\n]+)\]:[ \t]*(?:<([^>\n]+)>|(\S+))(?:[ \t]+[^\n]*)?$/gm,
    (_, label, angle, plain) => { definitions.set(labelKey(label), angle ?? plain); return ''; });
  text = text.replace(/!?\[(?:\\.|[^\]\\\n])*\]\(\s*(?:<([^>\n]+)>|((?:\\.|[^()\s]|\([^()\n]*\))+))(?:[ \t]+(?:"[^"]*"|'[^']*'|\([^)]*\)))?\s*\)/g,
    (_, angle, plain) => { links.push(angle ?? plain); return ''; });
  text = text.replace(/!?\[([^\]\n]+)\]\[([^\]\n]*)\]/g, (_, label, ref) => {
    const target = definitions.get(labelKey(ref || label));
    if (target) links.push(target);
    return '';
  });
  for (const match of text.matchAll(/!?\[([^\]\n]+)\]/g)) {
    const target = definitions.get(labelKey(match[1]));
    if (target) links.push(target);
  }
  for (const match of text.matchAll(/<(?:a|img)\b[^>]*?\b(?:href|src)\s*=\s*["']([^"']+)["'][^>]*>/gi)) links.push(match[1]);
  for (const match of text.matchAll(/<(https?:\/\/[^>\s]+)>/g)) links.push(match[1]);
  for (const match of text.matchAll(/https:\/\/github\.com\/wgabel\/orbit2\/(?:blob|tree)\/main\/[^\s<>"'\x60)\]]+/g)) links.push(match[0].replace(/[.,;]+$/, ''));
  return [...new Set(links)];
}

function headingText(text) {
  return text.replace(/!\[([^\]]*)\]\([^)]+\)/g, '$1')
    .replace(/\[([^\]]+)\](?:\([^)]+\)|\[[^\]]*\])/g, '$1')
    .replace(/<[^>]*>/g, '')
    .replace(/&(?:amp|lt|gt|quot|apos|nbsp);/g, x => ({'&amp;':'&','&lt;':'<','&gt;':'>','&quot;':'"','&apos;':"'",'&nbsp;':' '})[x])
    .replace(/&#(x[0-9a-f]+|\d+);/gi, (_, n) => {
      const code = n[0].toLowerCase() === 'x' ? parseInt(n.slice(1), 16) : Number(n);
      return code <= 0x10ffff ? String.fromCodePoint(code) : '';
    })
    .replace(/(^|[\s])_{1,3}([^_]+)_{1,3}(?=$|[\s.,:;!?])/g, '$1$2')
    .replace(/[*\x60~]/g, '').replace(/\\(.)/g, '$1').trim();
}

export function markdownAnchors(source) {
  const text = prose(source);
  const anchors = new Set();
  const used = new Set();
  for (const match of text.matchAll(/<a\b[^>]*?\b(?:id|name)\s*=\s*["']([^"']+)["']/gi)) anchors.add(match[1]);
  const lines = text.split('\n');
  for (let i = 0; i < lines.length; i++) {
    const atx = lines[i].match(/^\s{0,3}#{1,6}[ \t]+(.+?)(?:[ \t]+#+)?[ \t]*$/);
    const setext = !atx && lines[i].trim() && !/^\s{0,3}(?:[-*+]\s|>|\||#{1,6}\s)/.test(lines[i]) &&
      /^\s{0,3}(?:=+|-+)\s*$/.test(lines[i+1] ?? '');
    if (!atx && !setext) continue;
    const base = headingText(atx ? atx[1] : lines[i]).toLowerCase()
      .replace(/[^\p{L}\p{N}\p{M}_\- \t]/gu, '').replace(/[ \t]/g, '-');
    let slug = base;
    for (let suffix = 1; used.has(slug); suffix++) slug = base + '-' + suffix;
    used.add(slug);
    anchors.add(slug);
    if (setext) i++;
  }
  return anchors;
}

function resolveHref(source, href, inventory, directories) {
  let local = href.trim();
  const sameRepo = local.match(/^https:\/\/github\.com\/wgabel\/orbit2\/(?:blob|tree)\/main\/(.+)$/);
  if (sameRepo) local = '/' + sameRepo[1];
  else if (/^(?:[a-z][a-z0-9+.-]*:|\/\/)/i.test(local)) return null;
  const hash = local.indexOf('#');
  const fragment = hash < 0 ? '' : local.slice(hash + 1).split('?')[0];
  const rawPath = (hash < 0 ? local : local.slice(0, hash)).split('?')[0];
  let name, anchor;
  try { name = decodeURIComponent(rawPath); anchor = decodeURIComponent(fragment); }
  catch { return { error: 'invalid URL escaping' }; }
  if (name.includes('\\') || name.includes('\0')) return { error: 'invalid local path' };
  let target = name ? posix.normalize(name.startsWith('/') ? name.slice(1) : posix.join(posix.dirname(source), name)) : source;
  if (target === '..' || target.startsWith('../') || target.startsWith('/')) return { error: 'outside repository' };
  target = target.replace(/\/$/, '');
  if (!inventory.has(target) && directories.has(target)) {
    const index = posix.join(target, 'README.md');
    if (inventory.has(index)) target = index;
    else return { target: null };
  }
  if (!inventory.has(target)) return { error: 'target not found' };
  return { target, anchor };
}

function breadthFirst(graph, origin) {
  const parent = new Map([[origin, null]]);
  const queue = [origin];
  for (let i = 0; i < queue.length; i++) {
    for (const target of graph.get(queue[i]) ?? []) {
      if (!parent.has(target)) { parent.set(target, queue[i]); queue.push(target); }
    }
  }
  return parent;
}

function pathTo(parent, target) {
  if (!parent.has(target)) return null;
  const path = [];
  for (let next = target; next !== null; next = parent.get(next)) path.push(next);
  return path.reverse();
}

function reverseDiscovery(graph, root) {
  const reverse = new Map([...graph.keys()].map(path => [path, new Set()]));
  for (const [source, targets] of graph) for (const target of targets) {
    if (!reverse.has(target)) reverse.set(target, new Set());
    reverse.get(target).add(source);
  }
  // Each target receives an actual shortest discovery chain from the entry point.
  return target => {
    const backwards = breadthFirst(reverse, target);
    const chain = pathTo(backwards, root);
    return chain?.reverse() ?? null;
  };
}

export function analyzeDocumentation({ documents, files, config }) {
  const inventory = new Set(files);
  const directories = new Set(['.']);
  for (const file of inventory) {
    for (let dir = posix.dirname(file); dir !== '.'; dir = posix.dirname(dir)) directories.add(dir);
  }
  const errors = [];
  const warnings = [];
  const paths = [...inventory].filter(markdown).sort();
  const graph = new Map([...paths, ROUTING].map(path => [path, new Set()]));
  const anchors = new Map();
  for (const file of paths) {
    if (!file.includes('/') && file !== ROOT) errors.push('Root Markdown forbidden except AGENTS.md: ' + file);
    if (!documents.has(file)) { errors.push('Unreadable tracked Markdown: ' + file); continue; }
    anchors.set(file, markdownAnchors(documents.get(file)));
  }
  for (const [file, source] of documents) {
    if (!graph.has(file)) continue;
    for (const href of markdownLinks(source)) {
      const link = resolveHref(file, href, inventory, directories);
      if (!link) continue;
      if (link.error) { errors.push(file + ': ' + href + ' — ' + link.error); continue; }
      if (link.target && graph.has(link.target)) graph.get(file).add(link.target);
      if (link.target && link.anchor && markdown(link.target) && !anchors.get(link.target)?.has(link.anchor)) {
        errors.push(file + ': ' + href + ' — anchor not found');
      }
    }
  }
  const mandatory = new Set(config.alwaysRequired ?? []);
  const recommended = new Set();
  const reference = new Set();
  const routes = new Map();
  for (const route of config.routes ?? []) {
    routes.set(route.id, route);
    for (const path of route.required ?? []) mandatory.add(path);
    for (const path of route.recommended ?? []) recommended.add(path);
    for (const path of route.reference ?? []) reference.add(path);
  }
  const registered = new Set([...mandatory, ...recommended, ...reference]);
  for (const path of registered) if (!inventory.has(path)) errors.push('Routing target not tracked: ' + path);
  const coverage = config.coverage;
  if (coverage?.schemaVersion !== 1 || !Number.isInteger(coverage.maxActiveDepth) ||
      coverage.maxActiveDepth < 1 || !Array.isArray(coverage.exceptions) || !Array.isArray(coverage.scenarios) ||
      !coverage.scenarios.length) {
    errors.push('Missing or invalid documentation coverage policy.');
  }
  const exceptions = new Map();
  for (const item of Array.isArray(coverage?.exceptions) ? coverage.exceptions : []) {
    if (!item || !nonempty(item.path) || !nonempty(item.reason) ||
        !['archive','redirect','alternate-entry','template'].includes(item.kind) ||
        !paths.includes(item.path) || exceptions.has(item.path) ||
        (item.kind === 'archive' && !item.path.startsWith('docs/archive/')) ||
        (item.kind === 'alternate-entry' && item.path !== '.github/copilot-instructions.md') ||
        (item.kind === 'template' && item.path !== '.github/pull_request_template.md') ||
        (item.kind === 'redirect' && !item.path.startsWith('docs/')) ||
        (['redirect','alternate-entry'].includes(item.kind) && !nonempty(item.target))) {
      errors.push('Invalid or stale coverage classification: ' + JSON.stringify(item));
      continue;
    }
    if (registered.has(item.path)) errors.push('Routed document must not be exempt: ' + item.path);
    exceptions.set(item.path, item);
  }
  const humanPaths = breadthFirst(graph, ROOT);
  if (!inventory.has(ROOT)) errors.push('Missing agent entry point: ' + ROOT);
  if (!humanPaths.has(ROUTING)) errors.push('Routing configuration is not linked from ' + ROOT);
  // Also traverse actual routing declarations, but do not let them mask broken human navigation.
  for (const path of registered) if (graph.has(path)) graph.get(ROUTING).add(path);
  const routedPaths = breadthFirst(graph, ROOT);
  const reversePath = reverseDiscovery(graph, ROOT);
  const report = [];
  for (const file of paths) {
    const exemption = exceptions.get(file);
    const priority = mandatory.has(file) ? 'required' : recommended.has(file) ? 'recommended' : reference.has(file) ? 'reference' : exemption?.kind ?? 'unregistered';
    const navigation = pathTo(humanPaths, file);
    const routePath = pathTo(routedPaths, file);
    const reverse = reversePath(file);
    report.push({ file, priority, navigation, routePath, reversePath: reverse, reason: exemption?.reason ?? null });
    if (!exemption && !registered.has(file)) errors.push('No explicit routing owner: ' + file);
    if (exemption && ['redirect','alternate-entry'].includes(exemption.kind)) {
      if (!humanPaths.has(exemption.target) || !graph.get(file)?.has(exemption.target) ||
          (exemption.kind === 'alternate-entry' && exemption.target !== ROOT) || exceptions.has(exemption.target)) {
        errors.push('Invalid discovery redirect: ' + file + ' -> ' + exemption.target);
      }
      continue;
    }
    if (!navigation) errors.push('No Markdown navigation path from AGENTS.md: ' + file);
    if (!routePath || !reverse) errors.push('No forward/reverse discovery path: ' + file);
    if (!exemption && navigation && navigation.length - 1 > coverage?.maxActiveDepth) {
      warnings.push('Deep navigation (' + (navigation.length - 1) + ' links): ' + file);
    }
  }
  const tasks = new Set();
  for (const scenario of Array.isArray(coverage?.scenarios) ? coverage.scenarios : []) {
    if (!scenario || !nonempty(scenario.task) || tasks.has(scenario.task) ||
        !uniqueStrings(scenario.routes) || !scenario.routes.length ||
        !uniqueStrings(scenario.required) || !scenario.required.length) {
      errors.push('Invalid or duplicate routing scenario: ' + JSON.stringify(scenario)); continue;
    }
    tasks.add(scenario.task);
    const needed = new Set(config.alwaysRequired);
    for (const id of scenario.routes) {
      const route = routes.get(id);
      if (!route) errors.push('Unknown scenario route: ' + scenario.task + ' / ' + id);
      for (const path of route?.required ?? []) needed.add(path);
    }
    for (const path of scenario.required) {
      if (!inventory.has(path) || !needed.has(path)) errors.push('Scenario lacks mandatory document: ' + scenario.task + ' / ' + path);
    }
  }
  return { errors: [...new Set(errors)].sort(), warnings, report, scenarioCount: tasks.size };
}

export function auditRepositoryDocumentation(root, config) {
  const files = [...new Set(execFileSync('git', ['ls-files', '-z', '--cached'], {
    cwd: root, encoding: 'utf8', maxBuffer: 16 * 1024 * 1024
  }).split('\0').filter(Boolean))];
  const documents = new Map();
  for (const path of files.filter(markdown)) {
    const absolute = resolve(root, path);
    if (lstatSync(absolute).isFile()) documents.set(path, readFileSync(absolute, 'utf8'));
  }
  const audit = analyzeDocumentation({ documents, files, config });
  for (const entry of [ROOT, ROUTING, 'docs/README.md', 'docs/roadmap/overview.md',
    'docs/agents/contributor-instructions.md', '.github/copilot-instructions.md']) {
    if (!files.includes(entry)) audit.errors.push('Missing canonical documentation entry: ' + entry);
  }
  return audit;
}

export function formatCoverageSummary(audit) {
  const counts = {};
  for (const item of audit.report) counts[item.priority] = (counts[item.priority] ?? 0) + 1;
  return 'Documentation coverage: ' + audit.report.length + ' Markdown files; ' +
    Object.entries(counts).map(([key, n]) => key + '=' + n).join(', ') +
    '; ' + audit.scenarioCount + ' routing scenarios.';
}
