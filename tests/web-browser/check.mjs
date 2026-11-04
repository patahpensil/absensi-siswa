// Membuka web uji di Chromium nyata dan memastikan setiap pemeriksaan lulus.
// Hasil ditulis sebagai anotasi GitHub (::notice/::error) agar terbaca tanpa
// membuka log penuh.
import { chromium } from 'playwright';

const baseUrl = process.argv[2] ?? 'http://127.0.0.1:8080/';
const browser = await chromium.launch();
const page = await browser.newPage();
const consoleErrors = [];
page.on('console', (message) => {
  if (message.type() === 'error') consoleErrors.push(message.text());
});
page.on('pageerror', (error) => consoleErrors.push(String(error)));

await page.goto(baseUrl);
await page.waitForFunction(
  () => document.querySelector('#status')?.textContent?.startsWith('Selesai'),
  null,
  { timeout: 120_000 },
);

const items = await page.$$eval('#results li', (nodes) =>
  nodes.map((node) => ({ passed: node.dataset.passed === 'ya', text: node.textContent.trim() })),
);
await browser.close();

let failed = 0;
for (const item of items) {
  console.log(`${item.passed ? '::notice::' : '::error::'}${item.text}`);
  if (!item.passed) failed++;
}
for (const text of consoleErrors) console.log(`::warning::Konsol browser: ${text}`);
console.log(`Ringkasan: ${items.length - failed} dari ${items.length} pemeriksaan lulus.`);
process.exit(failed === 0 && items.length > 0 ? 0 : 1);
