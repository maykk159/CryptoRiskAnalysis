import { chromium } from 'file:///C:/Users/enesi/AppData/Local/OpenAI/Codex/runtimes/cua_node/b474a88d5d105afa/bin/node_modules/playwright-core/index.mjs';
const browser = await chromium.launch({ executablePath: 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', headless:true });
const page = await browser.newPage();
await page.goto('edge://settings/appearance');
console.log((await page.locator('body').innerText()).slice(-15000));
console.log(await page.locator('select').evaluateAll(es=>es.map(e=>({id:e.id,aria:e.getAttribute('aria-label'),options:e.innerText}))));
await browser.close();