import { chromium } from 'file:///C:/Users/enesi/AppData/Local/OpenAI/Codex/runtimes/cua_node/b474a88d5d105afa/bin/node_modules/playwright-core/index.mjs';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const out = new URL('./', import.meta.url).pathname.replace(/^\/(?=[A-Z]:)/, '');
const fixture = JSON.parse((await fs.readFile(out + 'bitcoin-response.json', 'utf8')).replace(/^\uFEFF/, ''));
const browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless: true });
const results = [];
const errors = [];
let page;
const setup = async (width = 360, height = 800) => {
  const context = await browser.newContext({ viewport: { width, height }, deviceScaleFactor: 1 });
  page = await context.newPage();
  page.on('pageerror', error => errors.push(error.message));
  return context;
};
const shot = async name => {
  await page.screenshot({ path: out + name + '.png', fullPage: true, animations: 'disabled' });
  const layout = await page.evaluate(() => ({ width: innerWidth, scrollWidth: document.documentElement.scrollWidth }));
  assert.ok(layout.scrollWidth <= layout.width, name + ' horizontal overflow');
  results.push({ name, ...layout, noHorizontalOverflow: true });
};
const success = route => route.fulfill({ json: fixture });
try {
  let context = await setup();
  let resolve;
  const gate = new Promise(r => { resolve = r; });
  await page.route('**/api/RiskAnalysis/**', async route => { await gate; await success(route); });
  await page.goto('http://localhost:5173');
  await page.getByLabel('Loading current price').waitFor();
  await shot('loading-360');
  resolve();
  await page.getByRole('heading', { name: 'Advanced Metrics' }).waitFor();
  await context.close();

  context = await setup();
  await page.route('**/api/RiskAnalysis/**', route => route.fulfill({ status: 429, json: { succeeded: false, message: 'Rate limited' } }));
  await page.goto('http://localhost:5173');
  await page.getByRole('alert').waitFor();
  await shot('initial-429-360');
  await context.close();

  context = await setup(1366,768);
  let failing = false;
  await page.route('**/api/RiskAnalysis/**', route => failing ? route.fulfill({ status: 429, json: { succeeded: false, message: 'Rate limited' } }) : success(route));
  await page.goto('http://localhost:5173');
  await page.getByRole('heading', { name: 'Advanced Metrics' }).waitFor();
  const fetched = await page.locator('time').getAttribute('datetime');
  const beforeHeight = await page.locator('body').evaluate(e => e.scrollHeight);
  failing = true;
  await page.getByRole('button', { name: 'Refresh', exact: true }).click();
  await page.getByText(/Showing the last successfully loaded data/).waitFor();
  assert.equal(await page.locator('time').getAttribute('datetime'), fetched);
  await shot('refresh-error-1366');
  results.push({ name: 'failed-refresh-retains-timestamp', passed: true, beforeHeight });
  await page.setViewportSize({ width: 360, height: 800 });
  await shot('refresh-error-360');
  await context.setOffline(true);
  await page.getByRole('button', { name: '7 Days' }).click();
  await page.getByText('Connection paused', { exact: false }).first().waitFor();
  await shot('offline-360');
  await context.close();

  context = await setup(390,844);
  await page.route('**/api/RiskAnalysis/**', route => route.fulfill({ json: { ...fixture, data: { ...fixture.data, priceHistory: [] } } }));
  await page.goto('http://localhost:5173');
  await page.getByText('No price history is available for this period.').waitFor();
  await shot('empty-chart-390');
  await context.close();

  context = await setup(1440,900);
  await page.emulateMedia({ reducedMotion: 'reduce' });
  await page.route('**/api/RiskAnalysis/**', success);
  await page.goto('http://localhost:5173');
  await page.getByRole('heading', { name: 'Advanced Metrics' }).waitFor();
  const chart = page.getByRole('img', { name: /30-day price chart/ });
  await chart.focus();
  for (let i = 0; i < 35; i++) await page.keyboard.press('ArrowLeft');
  await shot('keyboard-first-1440');
  for (let i = 0; i < 35; i++) await page.keyboard.press('ArrowRight');
  await shot('keyboard-last-reduced-motion-1440');
  assert.ok(await page.evaluate(() => document.getAnimations().every(a => a.playState !== 'running')));
  await page.getByLabel('About VaR (95%)').click();
  await shot('metric-help-1440');
  await page.setViewportSize({ width: 360, height: 800 });
  await page.getByRole('button', { name: 'Risk breakdown' }).click();
  await shot('risk-expanded-360');
  await page.getByRole('combobox').click();
  await page.getByRole('combobox').fill('not-found');
  await shot('no-assets-360');
  await page.keyboard.press('Escape');
  await page.getByRole('combobox').focus();
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('ArrowDown');
  await page.keyboard.press('Enter');
  assert.equal(await page.getByRole('combobox').inputValue(), 'Ethereum (ETH)');
  await page.keyboard.press('Tab');
  assert.ok(await page.getByRole('button', { name: '7 Days' }).evaluate(e => e === document.activeElement));
  results.push({ name: 'keyboard-selection-and-tab', passed: true });
  await context.close();

  context = await setup(1440,900);
  await page.route('**/api/RiskAnalysis/**', success);
  await page.goto('http://localhost:5173');
  await page.getByRole('heading', { name: 'Advanced Metrics' }).waitFor();
  // Chromium's CSS zoom implements 200% layout scaling with text and control reflow.
  await page.evaluate(() => { document.documentElement.style.zoom = '2'; });
  await shot('zoom-200-1440');
  await context.close();

  const palette = { canvas:'#0b1018', panel:'#121a26', raised:'#192436', ink:'#edf2fa', secondary:'#acb9cd', muted:'#8b9bb3', accent:'#9ab1ff', positive:'#57d3a5', warning:'#f2c66d', negative:'#f1808b', control:'#607493', selection:'#23345a' };
  const lum = hex => {
    const rgb = hex.match(/[a-f\d]{2}/gi).map(x => parseInt(x,16)/255).map(x => x <= .04045 ? x/12.92 : ((x+.055)/1.055)**2.4);
    return rgb[0]*.2126 + rgb[1]*.7152 + rgb[2]*.0722;
  };
  const ratio = (a,b) => (Math.max(lum(palette[a]),lum(palette[b]))+.05)/(Math.min(lum(palette[a]),lum(palette[b]))+.05);
  results.push({ name: 'contrast', ratios: Object.fromEntries(['ink','secondary','muted','accent','positive','warning','negative'].flatMap(fg => ['canvas','panel','raised'].map(bg => [fg+'/'+bg, +ratio(fg,bg).toFixed(2)]))), controlOnPanel:ratio('control','panel'), controlOnRaised:ratio('control','raised'), accentOnSelection:ratio('accent','selection') });
} finally {
  await fs.writeFile(out + 'states-results.json', JSON.stringify({ results, errors }, null, 2));
  await browser.close();
}