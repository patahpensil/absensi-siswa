// Menguji web uji di Chromium nyata: pemeriksaan teknis (?diagnostik) lalu
// alur pakai sebenarnya (login, menu per peran, daftar data, data bertahan
// setelah dimuat ulang). Hasil ditulis sebagai anotasi GitHub.
import { chromium } from 'playwright';

const baseUrl = (process.argv[2] ?? 'http://127.0.0.1:8080/').replace(/\/?$/, '/');
const browser = await chromium.launch();
const context = await browser.newContext();
const page = await context.newPage();
const consoleErrors = [];
page.on('console', (m) => { if (['error', 'critical'].includes(m.type()) || m.text().startsWith('crit:')) consoleErrors.push(m.text()); });
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
    for (const t of consoleErrors.splice(0)) console.log(`::warning::Konsol: ${t.slice(0, 1500)}`);
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

await langkah('Pencarian siswa dan filter kelas bekerja tanpa mengubah data', async () => {
  await page.click('#menu-siswa');
  await page.selectOption('#student-class', '7A');
  await page.waitForFunction(() => document.querySelectorAll('#tabel-siswa tbody tr').length === 8);
  await page.fill('#student-search', '9000000001');
  await page.waitForFunction(() => document.querySelectorAll('#tabel-siswa tbody tr').length === 1);
  await page.fill('#student-search', 'tidak-ada');
  await page.waitForFunction(() => document.querySelectorAll('#tabel-siswa tbody tr').length === 0);
  await page.fill('#student-search', '');
  await page.selectOption('#student-class', '');
});

await langkah('Bilah perintah mencari NISN dan pintasan keyboard membuka menu', async () => {
  await page.keyboard.press('Control+k');
  tegas(await page.$eval('#workspace-command', el => el === document.activeElement), 'fokus bukan di bilah perintah');
  await page.fill('#workspace-command', '9000000002');
  await page.press('#workspace-command', 'Enter');
  await page.waitForFunction(() => document.querySelectorAll('#tabel-siswa tbody tr').length === 1 && document.querySelector('#tabel-siswa').textContent.includes('9000000002'));
  await page.locator('#workspace-command').blur();
  await page.keyboard.press('F2');
  await page.waitForSelector('#tap-uid');
});

await langkah('Registrasi satu siswa satu kartu menjaga UID nol depan dan dapat dibatalkan', async () => {
  await page.click('#menu-siswa');
  for (const [id, uid] of [[1, '0000111101'], [2, '0000111102']]) {
    await page.click(`#register-card-${id}`);
    await page.locator('#registration-uid').pressSequentially(uid);
    await page.press('#registration-uid', 'Enter');
    tegas(await page.locator('#registration-success').count() === 0, 'Enter menyimpan tanpa konfirmasi');
    await page.click('#save-registration');
    await page.waitForSelector('#registration-success');
    tegas((await page.textContent('#registration-success')).includes(uid), 'UID tersimpan berbeda');
    await page.keyboard.press('Tab');
    tegas(await page.evaluate(() => document.querySelector('[role="dialog"]')?.contains(document.activeElement)), 'fokus keluar dari dialog setelah Simpan');
    await page.getByRole('button', { name: 'Tutup registrasi', exact: true }).click();
  }
  await page.click('#register-card-3');
  await page.fill('#registration-uid', '0000111199');
  await page.press('#registration-uid', 'Escape');
  await page.waitForSelector('[role="dialog"]', { state: 'hidden' });
  await page.click('#register-card-3');
  tegas(await page.inputValue('#registration-uid') === '', 'UID sementara tertinggal setelah Batal');
  await page.press('#registration-uid', 'Escape');
});

const setClock = async value => {
  await page.click('#menu-pengaturan');
  await page.fill('#scenario-time', value.replace(/:00$/, ''));
  await page.click('#apply-scenario');
  await page.waitForFunction(expected => document.querySelector('#jam-simulasi')?.textContent?.includes(expected), value.slice(11));
  await page.click('#menu-absensi');
  await page.waitForSelector('#tap-uid');
};
const tapCard = async uid => {
  await page.locator('#tap-uid').pressSequentially(uid);
  await page.press('#tap-uid', 'Enter');
  await page.waitForFunction(() => document.querySelector('#tap-uid')?.value === '' && document.activeElement?.id === 'tap-uid');
};

await langkah('Tap tepat 07.30 Hadir; 07.30.01 Terlambat memakai layanan asli', async () => {
  await setClock('2026-11-04T07:30:00');
  await tapCard('0000111101');
  await page.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('Hadir ·'));
  await setClock('2026-11-04T07:30:01');
  await tapCard('0000111102');
  await page.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('Terlambat ·'));
  tegas((await page.locator('#attendance-table tbody tr').filter({hasText:'Siswa 7A-2'}).textContent()).includes('07:30:01'), 'detik jam masuk hilang');
  await page.selectOption('#attendance-status', 'Terlambat');
  await page.waitForFunction(() => document.querySelectorAll('#attendance-table tbody tr').length === 1);
});

await langkah('Tap terlalu cepat dan kartu tidak dikenal ditolak dengan alasan', async () => {
  await tapCard('0000111102');
  await page.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('belum mencapai 15 detik'));
  await tapCard('KARTU-TIDAK-TERDAFTAR');
  await page.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('Kartu belum terdaftar'));
});

await langkah('Tap pulang mengisi jam tanpa menghapus status masuk', async () => {
  await setClock('2026-11-04T13:10:00');
  await tapCard('0000111101');
  await page.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('Tap pulang tercatat'));
  const row = page.locator('#attendance-table tbody tr').filter({ hasText: 'Siswa 7A-1' });
  tegas((await row.textContent()).includes('13:10:00'), 'jam pulang tidak tersimpan');
});

await langkah('Layout desktop dan HP tidak meluap di luar halaman', async () => {
  for (const size of [{ width: 1440, height: 900 }, { width: 1366, height: 768 }, { width: 768, height: 1024 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(size);
    for (const menu of ['dashboard', 'siswa', 'absensi', 'pengaturan']) {
      await page.click(`#menu-${menu}`);
      await page.waitForTimeout(100);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1);
      tegas(!overflow, `luapan ${menu} pada ${size.width}px`);
    }
  }
  await page.setViewportSize({ width: 1440, height: 900 });
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
  await page.click('#menu-siswa');
  await page.waitForSelector('#tabel-siswa');
  tegas(await baris('#tabel-siswa') === 8, 'wali melihat siswa di luar kelasnya');
  tegas((await page.textContent('#tabel-siswa')).includes('0000111101'), 'kartu tidak bertahan setelah reload');
  tegas(await page.locator('[id^="register-card-"]').count() === 0, 'wali mendapat tindakan registrasi kartu');
  await page.click('#menu-absensi');
  await page.waitForSelector('#attendance-table');
  tegas(await page.locator('#tap-uid').count() === 0, 'wali mendapat input pemrosesan kartu');
  tegas((await page.textContent('#attendance-table')).includes('13:10:00'), 'jam pulang tidak bertahan setelah reload');
  await page.click('#menu-pengaturan');
  await page.waitForSelector('#reset-data');
  const catatan = await page.textContent('#isi');
  tegas(catatan.includes('dipulihkan'), `catatan: ${catatan.slice(0, 120)}`);
});

await browser.close();
for (const t of consoleErrors) console.log(`::warning::Konsol browser: ${t}`);
console.log(`Ringkasan: ${failed === 0 ? 'semua langkah lulus' : `${failed} langkah gagal`}.`);
process.exit(failed === 0 ? 0 : 1);
