// Menguji web uji di Chromium nyata: pemeriksaan teknis (?diagnostik) lalu
// alur pakai sebenarnya (login, menu per peran, daftar data, data bertahan
// setelah dimuat ulang). Hasil ditulis sebagai anotasi GitHub.
import { chromium } from 'playwright';

const baseUrl = (process.argv[2] ?? 'http://127.0.0.1:8080/').replace(/\/?$/, '/');
const browser = await chromium.launch();
const context = await browser.newContext();
const page = await context.newPage();
const consoleErrors = [];
page.on('console', (m) => { if (m.type() === 'error') consoleErrors.push(m.text()); });
page.on('pageerror', (e) => consoleErrors.push(String(e)));

let failed = 0;
const lapor = (lulus, teks) => {
  console.log(`${lulus ? '::notice::LULUS' : '::error::GAGAL'} — ${teks}`);
  if (!lulus) failed++;
};
const langkah = async (nama, fn) => {
  try { await fn(); lapor(true, nama); }
  catch (e) {
    lapor(false, `${nama}: ${String(e.message).split('\n')[0]}`);
    const isi = await page.evaluate(() => document.body.innerText.slice(0, 300)).catch(() => '(halaman tidak terbaca)');
    console.log(`::warning::Isi halaman saat gagal: ${isi.replace(/\s+/g, ' ')}`);
    for (const t of consoleErrors.splice(0)) console.log(`::warning::Konsol: ${t.slice(0, 400)}`);
  }
};
const tegas = (kondisi, pesan) => { if (!kondisi) throw new Error(pesan); };
const masuk = async (u, s) => {
  await page.fill('#username', u);
  await page.fill('#sandi', s);
  await page.click('#tombol-masuk');
};
const baris = (sel) => page.$$eval(`${sel} tbody tr`, (n) => n.length);

await langkah('Pemeriksaan teknis (?diagnostik): semua lulus', async () => {
  await page.goto(`${baseUrl}?diagnostik`);
  await page.waitForFunction(() => document.querySelector('#status')?.textContent?.startsWith('Selesai'), null, { timeout: 120_000 });
  const hasil = await page.$$eval('#results li', (n) => n.map((x) => x.dataset.passed === 'ya'));
  tegas(hasil.length > 0 && hasil.every(Boolean), `${hasil.filter(Boolean).length} dari ${hasil.length} lulus`);
});

await langkah('Beranda menampilkan layar login dengan nama sekolah', async () => {
  await page.goto(baseUrl);
  await page.waitForSelector('#tombol-masuk', { timeout: 120_000 });
  tegas((await page.textContent('#nama-sekolah')).includes('(contoh)'), 'nama sekolah contoh tidak tampil');
});

await langkah('Sandi salah ditolak dengan pesan', async () => {
  await masuk('kepsek', 'salah');
  await page.waitForSelector('#galat-masuk');
  tegas((await page.textContent('#galat-masuk')).includes('salah'), 'pesan galat tidak sesuai');
});

await langkah('Kepala Sekolah masuk: dashboard tampil, menu Audit tersembunyi', async () => {
  await masuk('kepsek', 'contoh123');
  await page.waitForSelector('#ringkasan');
  tegas((await page.textContent('#akun-aktif')).includes('Kepala Sekolah'), 'akun aktif salah');
  tegas((await page.$('#menu-audit')) === null, 'menu Audit terlihat oleh Kepala Sekolah');
  tegas((await page.$('#menu-siswa')) !== null, 'menu Siswa tidak ada');
});

await langkah('Siswa 24, Guru 4, Kelas 3 (data contoh lewat layanan asli)', async () => {
  await page.click('#menu-siswa'); await page.waitForSelector('#tabel-siswa');
  tegas((await baris('#tabel-siswa')) === 24, `siswa = ${await baris('#tabel-siswa')}`);
  await page.click('#menu-guru'); await page.waitForSelector('#tabel-guru');
  tegas((await baris('#tabel-guru')) === 4, 'jumlah guru salah');
  await page.click('#menu-kelas'); await page.waitForSelector('#tabel-kelas');
  tegas((await baris('#tabel-kelas')) === 3, 'jumlah kelas salah');
});

await langkah('Menu yang belum dibangun diberi tahu apa adanya', async () => {
  await page.click('#menu-laporan');
  await page.waitForSelector('#belum-dibangun');
});

await langkah('Keluar kembali ke layar login', async () => {
  await page.click('#tombol-keluar');
  await page.waitForSelector('#tombol-masuk');
});

await langkah('Admin melihat menu Audit', async () => {
  await masuk('admin', 'contoh123');
  await page.waitForSelector('#menu-audit');
  await page.click('#tombol-keluar');
  await page.waitForSelector('#tombol-masuk');
});

await langkah('Data bertahan setelah halaman dimuat ulang (IndexedDB)', async () => {
  await page.reload();
  await page.waitForSelector('#tombol-masuk', { timeout: 120_000 });
  await masuk('wali7a', 'contoh123');
  await page.waitForSelector('#ringkasan');
  await page.click('#menu-pengaturan');
  await page.waitForSelector('#reset-data');
  const catatan = await page.textContent('#isi');
  tegas(catatan.includes('dipulihkan'), `catatan: ${catatan.slice(0, 120)}`);
});

await browser.close();
for (const t of consoleErrors) console.log(`::warning::Konsol browser: ${t}`);
console.log(`Ringkasan: ${failed === 0 ? 'semua langkah lulus' : `${failed} langkah gagal`}.`);
process.exit(failed === 0 ? 0 : 1);
