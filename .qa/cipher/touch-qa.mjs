import { chromium } from 'file:///C:/Users/enesi/AppData/Local/OpenAI/Codex/runtimes/cua_node/b474a88d5d105afa/bin/node_modules/playwright-core/index.mjs';
import fs from 'node:fs/promises';
import assert from 'node:assert/strict';
const out = new URL('./', import.meta.url).pathname.replace(/^\/(?=[A-Z]:)/, '');
const fixture = JSON.parse((await fs.readFile(out + 'bitcoin-response.json','utf8')).replace(/^\uFEFF/,''));
const browser = await chromium.launch({executablePath:'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',headless:true});
const checks=[];
try {
  const context=await browser.newContext({viewport:{width:360,height:800},hasTouch:true,isMobile:true});
  const page=await context.newPage();
  await page.route('**/api/RiskAnalysis/**',r=>r.fulfill({json:fixture}));
  await page.goto('http://localhost:5173');
  const chart=page.getByRole('img',{name:/30-day price chart/});
  await chart.scrollIntoViewIfNeeded();
  let box=await chart.boundingBox();
  await page.touchscreen.tap(box.x+90,box.y+150);
  await page.screenshot({path:out+'touch-first-360.png',fullPage:true,animations:'disabled'});
  await page.touchscreen.tap(box.x+box.width-20,box.y+150);
  await page.screenshot({path:out+'touch-last-360.png',fullPage:true,animations:'disabled'});
  assert.equal(await chart.evaluate(e=>getComputedStyle(e).touchAction),'pan-y');
  const before=await page.evaluate(()=>scrollY);
  const cdp=await context.newCDPSession(page);
  await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[{x:200,y:box.y+200}]});
  for(let i=1;i<=5;i++){
    await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[{x:200,y:box.y+200-i*25}]});
    await page.evaluate(()=>new Promise(requestAnimationFrame));
  }
  await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[]});
  const after=await page.evaluate(()=>scrollY);
  assert.ok(after>before);
  checks.push({name:'touch-inspection-and-vertical-scroll',before,after,passed:true});
  await context.close();
  const zoomContext=await browser.newContext({viewport:{width:720,height:450},deviceScaleFactor:2});
  const zoomPage=await zoomContext.newPage();
  await zoomPage.route('**/api/RiskAnalysis/**',r=>r.fulfill({json:fixture}));
  await zoomPage.goto('http://localhost:5173');
  await zoomPage.getByRole('heading',{name:'Advanced Metrics'}).waitFor();
  await zoomPage.screenshot({path:out+'zoom-200-reflow-1440.png',fullPage:true,animations:'disabled'});
  assert.ok(await zoomPage.evaluate(()=>document.documentElement.scrollWidth<=innerWidth));
  checks.push({name:'200-percent-equivalent-reflow',cssViewport:720,deviceScaleFactor:2,passed:true,limitation:'Browser menu zoom itself is not automated; this checks equivalent reflow plus separate CSS zoom.'});
  await zoomContext.close();
} finally {
  await fs.writeFile(out+'touch-results.json',JSON.stringify(checks,null,2));
  await browser.close();
}