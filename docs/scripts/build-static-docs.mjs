import { mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const rootDir = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const docsDir = path.join(rootDir, 'docs');
const distDir = path.join(rootDir, 'dist');
const siteTitle = 'Nona Docs';

await rm(distDir, { force: true, recursive: true });
await mkdir(path.join(distDir, 'assets'), { recursive: true });

const markdownFiles = (await readdir(docsDir))
  .filter((file) => file.endsWith('.md'))
  .sort((first, second) => first.localeCompare(second));

const pages = [];

for (const file of markdownFiles) {
  const markdown = await readFile(path.join(docsDir, file), 'utf8');
  const title = getDocumentTitle(markdown, file);
  const slug = path.basename(file, '.md');

  pages.push({
    file,
    markdown,
    output: `${slug}.html`,
    slug,
    title
  });
}

await writeFile(path.join(distDir, 'assets', 'styles.css'), getStyles());

for (const page of pages) {
  await writeFile(
    path.join(distDir, page.output),
    renderLayout({
      body: renderMarkdown(page.markdown),
      currentSlug: page.slug,
      pages,
      title: page.title
    })
  );
}

await writeFile(
  path.join(distDir, 'index.html'),
  renderLayout({
    body: renderIndex(pages),
    currentSlug: 'index',
    pages,
    title: siteTitle
  })
);

function getDocumentTitle(markdown, fallbackFileName) {
  const heading = markdown.match(/^#\s+(.+)$/m);

  if (heading) {
    return heading[1].trim();
  }

  return path.basename(fallbackFileName, '.md')
    .split(/[-_]+/)
    .map((part) => part.charAt(0).toUpperCase() + part.slice(1))
    .join(' ');
}

function renderLayout(options) {
  const navLinks = [
    `<a href="index.html"${options.currentSlug === 'index' ? ' aria-current="page"' : ''}>Home</a>`,
    ...options.pages.map((page) => {
      const current = page.slug === options.currentSlug ? ' aria-current="page"' : '';

      return `<a href="${escapeAttribute(page.output)}"${current}>${escapeHtml(page.title)}</a>`;
    })
  ].join('\n');

  return `<!doctype html>
<html lang="en">
<head>
  <meta charset="utf-8">
  <meta name="viewport" content="width=device-width, initial-scale=1">
  <title>${escapeHtml(options.title)} | ${escapeHtml(siteTitle)}</title>
  <link rel="stylesheet" href="assets/styles.css">
</head>
<body>
  <header class="site-header">
    <a class="site-title" href="index.html">${escapeHtml(siteTitle)}</a>
    <nav aria-label="Documentation">
      ${navLinks}
    </nav>
  </header>
  <main class="page">
    ${options.body}
  </main>
</body>
</html>
`;
}

function renderIndex(pages) {
  const links = pages.map((page) => {
    return `<li><a href="${escapeAttribute(page.output)}">${escapeHtml(page.title)}</a></li>`;
  }).join('\n');

  return `<h1>${escapeHtml(siteTitle)}</h1>
<p>Static documentation generated from the Markdown files in this repository.</p>
<ul>
${links}
</ul>`;
}

function renderMarkdown(markdown) {
  const lines = markdown.replace(/\r\n/g, '\n').split('\n');
  const blocks = [];
  let paragraph = [];
  let listItems = [];
  let orderedListItems = [];
  let inCodeBlock = false;
  let codeFence = [];
  let codeLanguage = '';

  function flushParagraph() {
    if (paragraph.length === 0) {
      return;
    }

    blocks.push(`<p>${renderInline(paragraph.join(' '))}</p>`);
    paragraph = [];
  }

  function flushList() {
    if (listItems.length > 0) {
      blocks.push(`<ul>\n${listItems.map((item) => `<li>${renderInline(item)}</li>`).join('\n')}\n</ul>`);
      listItems = [];
    }

    if (orderedListItems.length > 0) {
      blocks.push(`<ol>\n${orderedListItems.map((item) => `<li>${renderInline(item)}</li>`).join('\n')}\n</ol>`);
      orderedListItems = [];
    }
  }

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];

    if (inCodeBlock) {
      if (line.startsWith('```')) {
        blocks.push(`<pre><code${codeLanguage ? ` class="language-${escapeAttribute(codeLanguage)}"` : ''}>${escapeHtml(codeFence.join('\n'))}</code></pre>`);
        codeFence = [];
        codeLanguage = '';
        inCodeBlock = false;
      } else {
        codeFence.push(line);
      }
      continue;
    }

    if (line.startsWith('```')) {
      flushParagraph();
      flushList();
      inCodeBlock = true;
      codeLanguage = line.slice(3).trim();
      continue;
    }

    if (line.trim() === '') {
      flushParagraph();
      flushList();
      continue;
    }

    if (isTableStart(lines, index)) {
      flushParagraph();
      flushList();
      const { html, nextIndex } = renderTable(lines, index);
      blocks.push(html);
      index = nextIndex;
      continue;
    }

    const heading = line.match(/^(#{1,6})\s+(.+)$/);
    if (heading) {
      flushParagraph();
      flushList();
      const level = heading[1].length;
      blocks.push(`<h${level}>${renderInline(heading[2].trim())}</h${level}>`);
      continue;
    }

    const listItem = line.match(/^\s*[-*]\s+(.+)$/);
    if (listItem) {
      flushParagraph();
      orderedListItems = [];
      listItems.push(listItem[1].trim());
      continue;
    }

    const orderedListItem = line.match(/^\s*\d+\.\s+(.+)$/);
    if (orderedListItem) {
      flushParagraph();
      listItems = [];
      orderedListItems.push(orderedListItem[1].trim());
      continue;
    }

    paragraph.push(line.trim());
  }

  flushParagraph();
  flushList();

  if (inCodeBlock) {
    blocks.push(`<pre><code${codeLanguage ? ` class="language-${escapeAttribute(codeLanguage)}"` : ''}>${escapeHtml(codeFence.join('\n'))}</code></pre>`);
  }

  return blocks.join('\n');
}

function isTableStart(lines, index) {
  return lines[index]?.includes('|') === true
    && /^\s*\|?\s*:?-{3,}:?\s*(\|\s*:?-{3,}:?\s*)+\|?\s*$/.test(lines[index + 1] ?? '');
}

function renderTable(lines, startIndex) {
  const headers = splitTableRow(lines[startIndex]);
  const rows = [];
  let index = startIndex + 2;

  while (index < lines.length && lines[index].includes('|') && lines[index].trim() !== '') {
    rows.push(splitTableRow(lines[index]));
    index += 1;
  }

  const headerHtml = headers.map((header) => `<th>${renderInline(header)}</th>`).join('');
  const bodyHtml = rows.map((row) => {
    return `<tr>${row.map((cell) => `<td>${renderInline(cell)}</td>`).join('')}</tr>`;
  }).join('\n');

  return {
    html: `<table>
<thead><tr>${headerHtml}</tr></thead>
<tbody>
${bodyHtml}
</tbody>
</table>`,
    nextIndex: index - 1
  };
}

function splitTableRow(row) {
  return row
    .trim()
    .replace(/^\|/, '')
    .replace(/\|$/, '')
    .split('|')
    .map((cell) => cell.trim());
}

function renderInline(value) {
  return escapeHtml(value)
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/\[([^\]]+)]\(([^)]+)\)/g, (_match, label, url) => {
      return `<a href="${escapeAttribute(url)}">${label}</a>`;
    });
}

function escapeHtml(value) {
  return String(value)
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;');
}

function escapeAttribute(value) {
  return escapeHtml(value).replaceAll("'", '&#39;');
}

function getStyles() {
  return `:root {
  color-scheme: light;
  font-family: Inter, ui-sans-serif, system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
  line-height: 1.6;
  color: #172033;
  background: #f5f7fb;
}

* {
  box-sizing: border-box;
}

body {
  margin: 0;
}

a {
  color: #2454d6;
}

.site-header {
  position: sticky;
  top: 0;
  z-index: 1;
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  gap: 16px;
  padding: 14px max(24px, calc((100vw - 960px) / 2));
  border-bottom: 1px solid #d9e0ef;
  background: rgba(255, 255, 255, .94);
  backdrop-filter: blur(10px);
}

.site-title {
  color: #172033;
  font-size: 16px;
  font-weight: 750;
  text-decoration: none;
}

nav {
  display: flex;
  flex-wrap: wrap;
  gap: 6px;
}

nav a {
  padding: 6px 10px;
  border-radius: 6px;
  color: #4b5a75;
  font-size: 14px;
  font-weight: 650;
  text-decoration: none;
}

nav a[aria-current="page"],
nav a:hover {
  background: #e8eefc;
  color: #173ea7;
}

.page {
  width: min(960px, calc(100vw - 32px));
  margin: 32px auto;
  padding: 32px;
  border: 1px solid #dde4f0;
  border-radius: 8px;
  background: #fff;
}

h1,
h2,
h3,
h4,
h5,
h6 {
  margin: 1.4em 0 .55em;
  line-height: 1.25;
  color: #121a2b;
}

h1 {
  margin-top: 0;
  font-size: clamp(2rem, 3vw, 3rem);
}

h2 {
  padding-top: .35em;
  border-top: 1px solid #e4e9f3;
}

p,
ul,
ol,
table,
pre {
  margin: 0 0 1.05rem;
}

code {
  padding: .1em .3em;
  border-radius: 4px;
  background: #eef2fa;
  font-size: .92em;
}

pre {
  overflow: auto;
  padding: 16px;
  border-radius: 8px;
  background: #101827;
  color: #e8eefc;
}

pre code {
  padding: 0;
  background: transparent;
  color: inherit;
}

table {
  width: 100%;
  border-collapse: collapse;
  overflow: hidden;
}

th,
td {
  padding: 10px 12px;
  border: 1px solid #dde4f0;
  text-align: left;
  vertical-align: top;
}

th {
  background: #f2f5fb;
}

@media (max-width: 700px) {
  .site-header {
    padding: 12px 16px;
  }

  .page {
    width: 100%;
    margin: 0;
    padding: 22px 16px;
    border-right: 0;
    border-left: 0;
    border-radius: 0;
  }
}
`;
}
