import { chromium } from 'file:///C:/Users/enesi/AppData/Local/OpenAI/Codex/runtimes/cua_node/b474a88d5d105afa/bin/node_modules/playwright-core/index.mjs';
import fs from 'node:fs/promises';
const out = new URL('./', import.meta.url).pathname.replace(/^\/(?=[A-Z]:)/, '');
const browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true });
const results = [];
const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, deviceScaleFactor: 1 });
const errors = [];
page.on('pageerror', e => errors.push(e.message));
page.on('console', e => { if (e.type() === 'error' && !e.text().includes('Failed to load resource')) errors.push(e.text()); });
const snapshot = async name => {
  await page.screenshot({ path: out + name + '.png', fullPage: true, animations: 'disabled' });
  const layout = await page.evaluate(() => ({ width: innerWidth, scrollWidth: document.documentElement.scrollWidth }));
  results.push({ name, ...layout, noHorizontalOverflow: layout.scrollWidth <= layout.width });
};
try {
  await page.goto('http://localhost:5173');
  await page.getByRole('heading', { name: 'Advanced Metrics' }).waitFor({ timeout: 25000 });
  await page.evaluate(() => document.fonts.ready);
  for (const [width,height] of [[1440,900],[1366,768],[768,1024],[390,844],[360,800]]) {
    await page.setViewportSize({ width, height });
    await page.getByRole('heading', { name: 'Advanced Metrics' }).waitFor();
    await snapshot('bitcoin-' + width);
  }
  await page.getByRole('combobox').click();
  await page.getByRole('combobox').fill('shib');
  await snapshot('search-360');
  await page.getByRole('option', { name: /Shiba/ }).click();
  await page.getByRole('heading', { name: 'Advanced Metrics' }).waitFor({ timeout: 25000 });
  await snapshot('shib-360');
  await page.getByRole('combobox').click();
  await page.getByRole('combobox').fill('internet');
  await page.getByRole('option', { name: /Internet Computer/ }).click();
  await page.getByRole('heading', { name: 'Advanced Metrics' }).waitFor({ timeout: 25000 });
  await snapshot('long-name-360');
} finally {
  await fs.writeFile(out + 'browser-results.json', JSON.stringify({ results, errors }, null, 2));
  await browser.close();
}