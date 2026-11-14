// Menguji web uji di Chromium nyata: pemeriksaan teknis (?diagnostik) lalu
// alur pakai sebenarnya (login, menu per peran, daftar data, data bertahan
// setelah dimuat ulang). Hasil ditulis sebagai anotasi GitHub.
import { execFileSync } from 'node:child_process';
import { fileURLToPath } from 'node:url';
import { chromium } from 'playwright';

const baseUrl = (process.argv[2] ?? 'http://127.0.0.1:8080/').replace(/\/?$/, '/');
// Alamat kedua (opsional): salinan publish yang sama dengan appsettings "Mode": "nyata".
const realUrl = process.argv[3]?.replace(/\/?$/, '/');
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
// HANYA_MODE_NYATA=1: hanya langkah "Mode nyata" (dipakai repo Absensi-Uji-Real
// untuk memeriksa hasil deploy yang memakai "Mode": "nyata").
const onlyReal = process.env.HANYA_MODE_NYATA === '1';
const langkah = async (nama, fn) => {
  if (onlyReal && !nama.startsWith('Mode nyata')) return;
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

await langkah('Kepala Sekolah masuk: dashboard tampil, menu Audit tersedia (P-21)', async () => {
  await masuk('kepsek', 'contoh123');
  await page.waitForSelector('#ringkasan');
  tegas((await page.textContent('#akun-aktif')).includes('Kepala Sekolah'), 'akun aktif salah');
  tegas((await page.$('#menu-audit')) !== null, 'menu Audit tidak tampil untuk Kepala Sekolah');
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

await langkah('Registrasi kartu guru: konfirmasi wajib, UID siswa/guru duplikat ditolak, Batal tanpa menyimpan', async () => {
  await page.click('#menu-guru');
  await page.click('#register-teacher-card-1');
  tegas(await page.locator('#teacher-registration-uid').evaluate(el => el === document.activeElement), 'kolom kartu guru tidak mendapat fokus');
  await page.locator('#teacher-registration-uid').pressSequentially('0000222201');
  await page.press('#teacher-registration-uid', 'Enter');
  tegas(await page.locator('#teacher-registration-success').count() === 0, 'Enter reader menyimpan kartu guru tanpa konfirmasi');
  await page.click('#save-teacher-registration');
  await page.waitForSelector('#teacher-registration-success');
  tegas((await page.textContent('#teacher-registration-success')).includes('0000222201'), 'nol depan UID guru berubah');
  await page.keyboard.press('Tab');
  tegas(await page.evaluate(() => document.querySelector('[role="dialog"]')?.contains(document.activeElement)), 'fokus keluar dari dialog guru');
  await page.getByRole('button', { name: 'Tutup registrasi guru', exact: true }).click();
  tegas(await page.locator('#register-teacher-card-1').count() === 0, 'guru dengan kartu aktif masih dapat didaftarkan ulang');
  const registered = page.locator('#tabel-guru tbody tr').filter({ hasText: 'Ibu Sari' });
  tegas((await registered.textContent()).includes('0000222201'), 'UID tidak tampil pada baris guru');

  await page.click('#register-teacher-card-2');
  for (const uid of ['0000111101', '0000222201']) {
    await page.fill('#teacher-registration-uid', uid);
    await page.click('#save-teacher-registration');
    await page.waitForSelector('#teacher-registration-error');
    tegas((await page.textContent('#teacher-registration-error')).includes('sudah terdaftar'), 'duplikat lintas siswa/guru tidak dijelaskan');
    tegas(await page.locator('#teacher-registration-success').count() === 0, 'duplikat dilaporkan berhasil');
  }
  await page.fill('#teacher-registration-uid', '0000222299');
  await page.press('#teacher-registration-uid', 'Escape');
  await page.waitForSelector('[role="dialog"]', { state: 'detached' });
  await page.click('#register-teacher-card-2');
  tegas(await page.inputValue('#teacher-registration-uid') === '', 'UID batal tertinggal');
  tegas(!(await page.textContent('#tabel-guru')).includes('0000222299'), 'Batal menyimpan kartu');
  await page.press('#teacher-registration-uid', 'Escape');

  await page.click('#menu-siswa');
  await page.click('#register-card-3');
  await page.fill('#registration-uid', '0000222201');
  await page.click('#save-registration');
  await page.waitForSelector('[role="dialog"] [role="alert"]');
  tegas(await page.locator('#registration-success').count() === 0, 'kartu guru dapat dipakai ulang untuk siswa');
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

await langkah('Kartu guru diproses reader bersama: Masuk lalu Hadir dan riwayat tersimpan', async () => {
  await setClock('2026-11-04T07:25:00');
  await tapCard('0000222201');
  await page.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('Masuk · Ibu Sari'));
  await setClock('2026-11-04T13:10:00');
  await tapCard('0000222201');
  await page.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('Tap pulang tercatat · Ibu Sari'));
  await page.click('#menu-guru');
  await page.click('#teacher-detail-1');
  await page.waitForSelector('#person-history');
  const row = await page.locator('#person-history tbody tr').filter({ hasText: '04/11' }).textContent();
  tegas(row.includes('07:25:00') && row.includes('13:10:00') && row.includes('Hadir'), `riwayat guru salah: ${row}`);
  await page.click('#close-person-detail');
  await page.click('#menu-absensi');
});

await langkah('Klik nama membuka detail siswa/guru beserta riwayat dan rekap bulan', async () => {
  const historyRow = date => page.locator('#person-history tbody tr').filter({ hasText: date });
  await page.click('#attendance-detail-1');
  await page.waitForSelector('#person-detail');
  tegas((await page.textContent('#person-detail-name')).includes('Siswa 7A-1'), 'nama di panel salah');
  tegas((await page.textContent('#person-detail-month')).includes('November 2026'), 'bulan panel bukan bulan skenario');
  const first = await historyRow('04/11').textContent();
  tegas(first.includes('07:30:00') && first.includes('13:10:00') && first.includes('Hadir'), `riwayat tap tidak tampil: ${first}`);
  tegas((await page.textContent('#person-detail-recap')).includes('Hadir'), 'rekap bulan tidak tampil');
  tegas((await page.textContent('#person-detail-card')).includes('0000111101'), 'kartu RFID tidak tampil');
  await page.keyboard.press('Escape');
  await page.waitForSelector('#person-detail', { state: 'detached' });

  await page.click('#menu-siswa');
  await page.click('#student-detail-2');
  await page.waitForSelector('#person-detail');
  const second = await historyRow('04/11').textContent();
  tegas(second.includes('07:30:01') && second.includes('Terlambat'), `riwayat terlambat tidak tampil: ${second}`);
  await page.click('#person-detail-prev');
  await page.waitForFunction(() => document.querySelector('#person-detail-month')?.textContent?.includes('Oktober 2026'));
  tegas(await page.$eval('#person-detail-next', b => !b.disabled), 'tidak bisa kembali ke bulan berjalan');
  await page.setViewportSize({ width: 390, height: 844 });
  tegas(!(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1)), 'panel detail meluap di HP');
  await page.setViewportSize({ width: 1440, height: 900 });
  await page.click('#close-person-detail');
  await page.waitForSelector('#person-detail', { state: 'detached' });

  await page.click('#menu-guru');
  await page.click('#teacher-detail-1');
  await page.waitForSelector('#person-detail');
  tegas(await page.locator('#person-history, #person-detail-empty').count() === 1, 'riwayat guru tidak tampil');
  tegas(await page.locator('#person-detail-recap').count() === 1, 'rekap guru tidak tampil');
  await page.click('#close-person-detail');
  await page.waitForSelector('#person-detail', { state: 'detached' });
  await page.click('#menu-absensi');
  await page.waitForSelector('#tap-uid');
});

await langkah('Koreksi status: alasan wajib, atas permintaan wali, tersimpan dan ditandai koreksi', async () => {
  await page.click('#menu-koreksi');
  await page.waitForSelector('#correction-table');
  tegas(await page.inputValue('#correction-date') === '2026-11-04', 'tanggal koreksi bukan tanggal skenario');
  await page.click('#correct-2');
  await page.waitForSelector('#correction-form');
  tegas(await page.$eval('#correction-save', b => b.disabled), 'koreksi tanpa alasan dapat disimpan');
  await page.selectOption('#correction-status', 'Izin');
  await page.fill('#correction-reason', 'Surat izin orang tua (contoh)');
  await page.selectOption('#correction-requester', { index: 1 });
  await page.click('#correction-save');
  await page.waitForSelector('#correction-success');
  await page.click('#correction-cancel');
  await page.waitForSelector('#correction-form', { state: 'detached' });
  const row = await page.locator('#correction-table tbody tr').filter({ hasText: 'Siswa 7A-2' }).textContent();
  tegas(row.includes('Izin') && row.includes('07:30:01'), `status/jam setelah koreksi salah: ${row}`);
  await page.click('#menu-siswa');
  await page.click('#student-detail-2');
  await page.waitForSelector('#person-detail');
  const history = await page.locator('#person-history tbody tr').filter({ hasText: '04/11' }).textContent();
  tegas(history.includes('Izin') && history.includes('koreksi'), `penanda koreksi tidak tampil: ${history}`);
  await page.click('#close-person-detail');
  await page.waitForSelector('#person-detail', { state: 'detached' });
  await page.click('#menu-absensi');
  await page.waitForSelector('#tap-uid');
});

await langkah('Unggah foto siswa dan guru: dikecilkan 200×200, berkas bukan gambar ditolak', async () => {
  const portrait = Buffer.from(await page.evaluate(() => {
    const c = document.createElement('canvas'); c.width = 300; c.height = 400;
    const g = c.getContext('2d'); g.fillStyle = '#275baa'; g.fillRect(0, 0, 300, 400);
    return c.toDataURL('image/png').split(',')[1];
  }), 'base64');
  await page.click('#menu-siswa');
  await page.click('#student-detail-3');
  await page.waitForSelector('#person-detail-initials');
  await page.setInputFiles('#person-photo-input', { name: 'catatan.txt', mimeType: 'text/plain', buffer: Buffer.from('bukan gambar') });
  await page.waitForSelector('#person-photo-message.danger');
  tegas(await page.locator('#person-detail-photo').count() === 0, 'berkas bukan gambar tersimpan sebagai foto');
  await page.setInputFiles('#person-photo-input', { name: 'siswa.png', mimeType: 'image/png', buffer: portrait });
  await page.waitForSelector('#person-photo-message.positive');
  await page.waitForSelector('#person-detail-photo');
  const size = await page.$eval('#person-detail-photo', img => img.complete ? [img.naturalWidth, img.naturalHeight] : [0, 0]);
  tegas(size[0] === 200 && size[1] === 200, `ukuran foto ${size.join('×')}`);
  await page.click('#close-person-detail');
  await page.waitForSelector('#person-detail', { state: 'detached' });
  tegas(await page.locator('#student-detail-3 img.mini-avatar').count() === 1, 'foto tidak tampil di daftar siswa');
  await page.click('#menu-guru');
  await page.click('#teacher-detail-1');
  await page.setInputFiles('#person-photo-input', { name: 'guru.png', mimeType: 'image/png', buffer: portrait });
  await page.waitForSelector('#person-detail-photo');
  await page.click('#close-person-detail');
  await page.waitForSelector('#person-detail', { state: 'detached' });
  await page.click('#menu-absensi');
  await page.waitForSelector('#tap-uid');
});

const downloadBytes = async (selector) => {
  const [download] = await Promise.all([page.waitForEvent('download', { timeout: 60_000 }), page.click(selector)]);
  const chunks = [];
  for await (const chunk of await download.createReadStream()) chunks.push(chunk);
  return { name: download.suggestedFilename(), bytes: Buffer.concat(chunks) };
};

await langkah('Laporan: pratinjau harian/bulanan/semester dan unduh Excel memakai penulis asli', async () => {
  await page.click('#menu-laporan');
  await page.waitForSelector('#report-show');
  tegas(await page.inputValue('#report-date') === '2026-11-04', 'tanggal laporan bukan tanggal skenario');
  await page.click('#report-show');
  await page.waitForSelector('#report-table');
  tegas((await page.textContent('#report-table')).includes('Siswa 7A-1'), 'laporan harian tidak memuat tap hari ini');
  const excel = await downloadBytes('#report-excel');
  tegas(excel.name.endsWith('.xlsx') && excel.bytes.length > 1000 && excel.bytes.subarray(0, 2).toString() === 'PK', `berkas Excel tidak valid: ${excel.name} ${excel.bytes.length} B`);
  await page.selectOption('#report-kind', 'Monthly');
  await page.click('#report-show');
  await page.waitForSelector('#report-table.report-grid');
  tegas(await page.locator('#report-table thead th').count() === 30 + 7, 'kolom tanggal bulanan tidak lengkap');
  await page.selectOption('#report-kind', 'Semester');
  await page.click('#report-show');
  await page.waitForSelector('#report-table');
  await page.selectOption('#report-kind', 'PerPerson');
  await page.selectOption('#report-person', { index: 1 });
  await page.click('#report-show');
  await page.waitForSelector('#report-card');
});

await langkah('Laporan: PDF di browser lewat Cetak / Simpan PDF, Excel per siswa dari panel', async () => {
  await page.selectOption('#report-kind', 'Daily');
  await page.fill('#report-date', '2026-11-04');
  await page.click('#report-show');
  await page.waitForSelector('#report-table');
  tegas(await page.locator('#report-pdf, #person-report-pdf').count() === 0, 'tombol PDF yang tidak berfungsi di browser masih tampil');
  tegas((await page.textContent('#report-print')).includes('Simpan PDF'), 'jalur Cetak / Simpan PDF tidak tersedia');
  await page.click('#menu-siswa');
  await page.click('#student-detail-1');
  await page.waitForSelector('#person-detail');
  const card = await downloadBytes('#person-report-excel');
  tegas(card.name.includes('Siswa 7A-1') && card.bytes.subarray(0, 2).toString() === 'PK', `laporan per siswa dari panel tidak valid: ${card.name}`);
  await page.click('#close-person-detail');
  await page.waitForSelector('#person-detail', { state: 'detached' });
  await page.click('#menu-absensi');
  await page.waitForSelector('#tap-uid');
});

await langkah('Layar gerbang: TV terpisah, fullscreen, foto siswa/guru dan reader bersama', async () => {
  const longName = 'Siswa Contoh dengan Nama Panjang untuk Memeriksa Keterbacaan Layar Gerbang Sekolah';
  await page.click('#menu-siswa');
  await page.click('#edit-student-2');
  const originalName = await page.inputValue('#master-edit-name');
  await page.fill('#master-edit-name', longName);
  await page.click('#master-edit-save');
  await page.waitForSelector('#master-edit-success');
  await page.getByRole('button', { name: 'Tutup ubah data' }).click();
  await setClock('2026-11-05T07:29:00');
  await page.click('#menu-layar-gerbang');
  await page.waitForSelector('#gate-controls');
  const popupPromise = context.waitForEvent('page');
  await page.click('#gate-open');
  const tv = await popupPromise;
  await tv.waitForFunction(() => document.querySelector('#gate-fullscreen')?.disabled === false);
  tegas((await tv.textContent('#gate-school')).includes('SMP Negeri Contoh'), 'nama sekolah tidak tersaji di TV');
  tegas(await tv.locator('#menu-siswa, #menu-guru, #akun-aktif').count() === 0, 'TV memiliki kontrol admin');
  await tv.click('#gate-fullscreen');
  await tv.waitForFunction(() => document.fullscreenElement !== null);
  await tv.locator('#gate-reader').pressSequentially('0000111101');
  await tv.press('#gate-reader', 'Enter');
  await tv.waitForFunction(() => document.querySelector('#gate-cards')?.textContent?.includes('Siswa 7A-1'));
  tegas((await tv.textContent('#gate-cards')).includes('Hadir'), 'status siswa salah di TV');
  await tv.locator('#gate-reader').pressSequentially('0000222201');
  await tv.press('#gate-reader', 'Enter');
  await tv.waitForFunction(() => document.querySelector('#gate-cards')?.textContent?.includes('Ibu Sari'));
  tegas((await tv.textContent('#gate-cards')).includes('Guru'), 'label guru hilang');
  tegas(await tv.locator('#gate-cards .card').count() >= 2, 'tap siswa dan guru tidak tampil bersamaan');
  tegas(await tv.locator('#gate-cards img.photo').count() >= 1, 'foto guru yang sudah diunggah tidak tampil');

  await tv.setViewportSize({ width: 1920, height: 1080 });
  for (const uid of ['0000111102', '0000999999']) {
    await tv.fill('#gate-reader', uid, { force: true });
    await tv.press('#gate-reader', 'Enter');
  }
  await tv.waitForFunction(() => document.querySelectorAll('#gate-cards .card').length === 4);
  tegas((await tv.textContent('#gate-cards')).includes(longName), 'nama panjang terpotong pada TV');
  const layout = await tv.locator('#gate-cards .card').evaluateAll(cards => cards.map(card => {
    const name = card.querySelector('.name');
    const box = card.getBoundingClientRect();
    const text = name.getBoundingClientRect();
    return { viewport: [innerWidth, innerHeight], card: [box.left, box.right, box.bottom], name: [text.left, text.right] };
  }));
  const fits = layout.every(({ viewport: [width, height], card: [left, right, bottom], name: [nameLeft, nameRight] }) =>
    left >= -1 && right <= width + 1 && bottom <= height + 1 && nameLeft >= left - 1 && nameRight <= right + 1);
  tegas(fits, `empat kartu atau nama panjang meluap pada TV 1920 × 1080: ${JSON.stringify(layout)}`);

  await page.click('#menu-dashboard');
  tegas(await page.locator('#ringkasan').count() === 1 && !tv.isClosed(), 'dashboard dan layar TV tidak dapat dibuka terpisah');
  tegas((await page.textContent('.tap-cards')).includes('Siswa 7A-1'), 'dashboard tidak menampilkan tap dari TV');
  await page.click('#menu-layar-gerbang');
  await page.click('#gate-stop');
  await page.fill('#gate-confirm-password', 'salah');
  await page.click('#gate-confirm-submit');
  await page.waitForSelector('#gate-confirm-error');
  tegas((await page.textContent('#gate-receiving-state')).includes('aktif'), 'sandi salah menghentikan penerimaan');
  await page.fill('#gate-confirm-password', 'contoh123');
  await page.click('#gate-confirm-submit');
  await page.waitForFunction(() => document.querySelector('#gate-receiving-state')?.textContent?.includes('dihentikan'));
  tegas(!tv.isClosed(), 'Hentikan tap ikut menutup TV');
  await tv.waitForFunction(() => document.querySelector('#gate-reader')?.disabled === true);
  await page.click('#menu-absensi');
  await page.waitForSelector('#attendance-reader-stopped');
  tegas(await page.locator('#tap-uid').isDisabled(), 'halaman Absensi masih menerima reader ketika gerbang dihentikan');
  await page.click('#menu-layar-gerbang');
  await page.click('#gate-start');
  await tv.waitForFunction(() => document.querySelector('#gate-reader')?.disabled === false);

  await page.click('#gate-close');
  await page.fill('#gate-confirm-password', 'contoh123');
  const closed = tv.waitForEvent('close');
  await page.click('#gate-confirm-submit');
  await closed;
  tegas((await page.textContent('#gate-receiving-state')).includes('aktif'), 'menutup TV ikut menghentikan tap');
  // No display remains, but the controller can keep receiving from its input.
  await page.fill('#gate-controller-uid', 'KARTU-TIDAK-TERDAFTAR');
  await page.press('#gate-controller-uid', 'Enter');
  await page.waitForFunction(() => document.querySelector('#gate-controller-uid')?.value === '');
  await page.click('#menu-siswa');
  await page.click('#edit-student-2');
  await page.fill('#master-edit-name', originalName);
  await page.click('#master-edit-save');
  await page.waitForSelector('#master-edit-success');
  await page.getByRole('button', { name: 'Tutup ubah data' }).click();
  await setClock('2026-11-04T13:10:00');
});

await langkah('URL TV langsung tidak membuka sesi dan pesan palsu tidak memberi data', async () => {
  const direct = await context.newPage();
  await direct.goto(`${baseUrl}gate.html`);
  tegas(await direct.locator('#gate-fullscreen').isDisabled(), 'URL langsung dapat membuka fullscreen sesi');
  tegas(await direct.locator('#gate-reader').isDisabled(), 'URL langsung dapat memproses RFID');
  tegas(await direct.locator('#gate-cards .card').count() === 0, 'URL langsung membocorkan identitas');
  await direct.evaluate(() => window.postMessage({ type: 'gate-snapshot', token: 'palsu', value: { cards: [{ name: 'Data palsu' }] } }, location.origin));
  tegas(await direct.locator('#gate-cards .card').count() === 0, 'pesan tanpa pengendali diterima');
  await direct.close();
});

await langkah('Layout desktop dan HP tidak meluap di luar halaman', async () => {
  for (const size of [{ width: 1440, height: 900 }, { width: 1366, height: 768 }, { width: 1280, height: 768 }, { width: 768, height: 1024 }, { width: 390, height: 844 }]) {
    await page.setViewportSize(size);
    for (const menu of ['dashboard', 'siswa', 'guru', 'absensi', 'layar-gerbang', 'pengaturan']) {
      await page.click(`#menu-${menu}`);
      await page.waitForTimeout(100);
      const overflow = await page.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1);
      tegas(!overflow, `luapan ${menu} pada ${size.width}px`);
    }
  }
  await page.setViewportSize({ width: 1440, height: 900 });
});

// PANDUAN-UI-UX §3–§5 dan §12, diperiksa pada gaya yang benar-benar dihitung
// browser: satu keluarga huruf, tiga radius, dua bayangan, tanpa gradasi,
// tanpa jarak huruf, tanpa label kapital semua.
await langkah('Tampilan mengikuti panduan UI: huruf, radius, bayangan, gradasi, label kapital', async () => {
  await page.evaluate(() => document.fonts.ready);
  tegas(await page.evaluate(() => document.fonts.check('14px "IBM Plex Sans"')), 'IBM Plex Sans tidak termuat');
  const menus = await page.$$eval('nav [id^="menu-"]', nodes => nodes.map(n => n.id));
  tegas(menus.length >= 8, `menu yang diperiksa terlalu sedikit: ${menus.length}`);
  const problems = [];
  let checked = 0;
  for (const id of menus) {
    await page.click(`#${id}`);
    await page.waitForTimeout(150);
    const found = await page.evaluate(() => {
      const allowedRadius = new Set(['0px', '6px', '10px', '999px', '50%']);
      const allowedShadow = ['none', 'rgba(20, 24, 31, 0.06) 0px 1px 2px 0px', 'rgba(20, 24, 31, 0.12) 0px 4px 12px 0px'];
      const acronyms = new Set(['NISN', 'NIP', 'NPSN', 'RFID', 'PDF', 'WITA', 'TV', 'UID', 'SMP', 'HP', 'NO', 'ID', 'TU', 'II', 'III', 'IV', 'VII', 'VIII', 'IX', 'NAMA', 'KELAS', 'ALAMAT', 'PANGKAT', 'JABATAN']);
      const out = [];
      let count = 0;
      for (const el of document.querySelectorAll('body *')) {
        const rect = el.getBoundingClientRect();
        if (rect.width === 0 || rect.height === 0) continue;
        const cs = getComputedStyle(el);
        if (cs.visibility === 'hidden') continue;
        count++;
        const tag = `${el.tagName.toLowerCase()}${el.id ? '#' + el.id : ''}${el.classList.length ? '.' + [...el.classList].join('.') : ''}`;
        for (const corner of [cs.borderTopLeftRadius, cs.borderBottomRightRadius])
          if (!allowedRadius.has(corner)) out.push(`radius ${corner} pada ${tag}`);
        if (!allowedShadow.includes(cs.boxShadow) && document.activeElement !== el) out.push(`bayangan ${cs.boxShadow} pada ${tag}`);
        if (cs.backgroundImage.includes('gradient')) out.push(`gradasi pada ${tag}`);
        if (cs.letterSpacing !== 'normal' && cs.letterSpacing !== '0px') out.push(`jarak huruf ${cs.letterSpacing} pada ${tag}`);
        if (cs.textTransform === 'uppercase') out.push(`huruf kapital paksa pada ${tag}`);
        for (const node of el.childNodes) {
          if (node.nodeType !== 3) continue;
          const words = node.textContent.split(/[^A-Za-zÀ-ÿ]+/).filter(w => w.length >= 3 && w === w.toUpperCase() && !acronyms.has(w));
          if (words.length >= 2) out.push(`label kapital "${node.textContent.trim().slice(0, 40)}" pada ${tag}`);
        }
      }
      return { out, count };
    });
    checked += found.count;
    for (const f of found.out) problems.push(`${id}: ${f}`);
  }
  tegas(checked > menus.length * 50, `elemen yang diperiksa terlalu sedikit: ${checked}`);
  console.log(`::notice::Panduan UI: ${menus.length} menu, ${checked} elemen terlihat diperiksa`);
  const unique = [...new Set(problems)];
  tegas(unique.length === 0, `pelanggaran panduan UI (${unique.length}): ${unique.slice(0, 12).join(' | ')}`);
});

await langkah('Kalender dan pratinjau Notifikasi bisa dibuka', async () => {
  await page.click('#menu-kalender');
  await page.waitForSelector('#school-calendar');
  await page.click('#menu-notifikasi');
  await page.waitForSelector('#school-notifications');
});

await langkah('Keluar akun menutup TV dan kembali ke layar login', async () => {
  await page.click('#menu-layar-gerbang');
  const popupPromise = context.waitForEvent('page');
  await page.click('#gate-open');
  const tv = await popupPromise;
  await tv.waitForFunction(() => document.querySelector('#gate-fullscreen')?.disabled === false);
  const closed = tv.waitForEvent('close');
  await page.click('#tombol-keluar');
  await closed;
  await page.waitForSelector('#tombol-masuk');
});

await langkah('Admin melihat menu Audit', async () => {
  await masuk('admin', 'contoh123');
  await page.waitForSelector('#menu-audit');
  tegas(await page.locator('#menu-layar-gerbang').count() === 1, 'admin tidak mendapat menu gerbang');
  await page.click('#menu-layar-gerbang');
  await page.waitForSelector('#gate-controls');
  const popupPromise = context.waitForEvent('page');
  await page.click('#gate-open');
  const tv = await popupPromise;
  await tv.waitForFunction(() => document.querySelector('#gate-fullscreen')?.disabled === false);
  const closed = tv.waitForEvent('close');
  await page.click('#tombol-keluar');
  await closed;
  await page.waitForSelector('#tombol-masuk');
});

await langkah('Data bertahan setelah halaman dimuat ulang (IndexedDB)', async () => {
  await page.reload();
  await page.waitForSelector('#tombol-masuk', { timeout: 120_000 });
  // Reload resets the in-memory scenario clock; restore the same school date
  // before checking persisted attendance. Opening the day is idempotent.
  await masuk('admin', 'contoh123');
  await page.waitForSelector('#ringkasan');
  await setClock('2026-11-04T13:10:00');
  await page.click('#menu-guru');
  tegas((await page.textContent('#tabel-guru')).includes('0000222201'), 'kartu guru hilang setelah reload');
  await page.click('#teacher-detail-1');
  await page.waitForSelector('#person-history');
  const teacherHistory = await page.locator('#person-history tbody tr').filter({ hasText: '04/11' }).textContent();
  tegas(teacherHistory.includes('07:25:00') && teacherHistory.includes('13:10:00'), 'riwayat guru hilang setelah reload');
  await page.click('#close-person-detail');
  await page.click('#tombol-keluar');
  await page.waitForSelector('#tombol-masuk');
  await masuk('wali7a', 'contoh123');
  await page.waitForSelector('#ringkasan');
  tegas(await page.locator('#menu-layar-gerbang, #dashboard-open-tv').count() === 0, 'wali mendapat pengendali gerbang');
  await page.fill('#workspace-command', 'Layar gerbang');
  await page.press('#workspace-command', 'Enter');
  tegas(await page.locator('#gate-controls, #gate-open').count() === 0, 'wali dapat membuka gerbang melalui perintah');
  tegas((await page.textContent('#isi')).includes('tidak berwenang'), 'perintah gerbang wali tidak menjelaskan penolakan');
  await page.click('#menu-siswa');
  await page.waitForSelector('#tabel-siswa');
  tegas(await baris('#tabel-siswa') === 8, 'wali melihat siswa di luar kelasnya');
  tegas((await page.textContent('#tabel-siswa')).includes('0000111101'), 'kartu tidak bertahan setelah reload');
  tegas(await page.locator('[id^="register-card-"]').count() === 0, 'wali mendapat tindakan registrasi kartu');
  tegas(await page.locator('#student-add, #student-import-excel, [id^="edit-student-"], [id^="replace-student-card-"], #menu-notifikasi, #menu-audit').count() === 0, 'wali mendapat tindakan pengelolaan sekolah');
  await page.click('#menu-kalender');
  await page.waitForSelector('#school-calendar');
  tegas(await page.locator('#calendar-save, #calendar-finalize, #calendar-lock').count() === 0, 'wali dapat mengubah kalender');
  await page.click('#menu-siswa');
  tegas(await page.locator('#student-detail-3 img.mini-avatar').count() === 1, 'foto siswa tidak bertahan setelah reload');
  await page.click('#student-detail-3');
  await page.waitForSelector('#person-detail-photo');
  tegas(await page.locator('#person-photo-input').count() === 0, 'wali mendapat tombol unggah foto');
  await page.click('#close-person-detail');
  await page.waitForSelector('#person-detail', { state: 'detached' });
  await page.click('#menu-absensi');
  await page.waitForSelector('#attendance-table');
  tegas(await page.locator('#tap-uid').count() === 0, 'wali mendapat input pemrosesan kartu');
  const persistedFirst = await page.locator('#attendance-table tbody tr').filter({ hasText: 'Siswa 7A-1 (contoh)' }).textContent();
  const persistedSecond = await page.locator('#attendance-table tbody tr').filter({ hasText: 'Siswa 7A-2 (contoh)' }).textContent();
  tegas(persistedFirst.includes('07:30:00') && persistedFirst.includes('13:10:00'), 'jam masuk/pulang tidak bertahan pada tanggal skenario yang sama');
  tegas(persistedSecond.includes('07:30:01'), 'detik jam masuk tidak bertahan setelah reload');
  await page.click('#attendance-detail-1');
  await page.waitForSelector('#person-detail');
  tegas((await page.locator('#person-history tbody tr').filter({ hasText: '04/11' }).textContent()).includes('13:10:00'), 'wali tidak melihat riwayat siswanya');
  tegas(await page.locator('#person-detail-recap-note').count() === 1, 'wali mendapat rekap laporan');
  await page.click('#history-correct-20261104');
  await page.waitForSelector('#correction-form');
  tegas(await page.locator('#correction-requester').count() === 0, 'wali mendapat kolom atas permintaan');
  await page.selectOption('#correction-status', 'Sakit');
  await page.fill('#correction-reason', 'Surat dokter (contoh)');
  await page.click('#correction-save');
  await page.waitForSelector('#correction-success');
  await page.waitForFunction(() => document.querySelector('#person-history tbody tr')?.textContent?.includes('Sakit'));
  await page.click('#correction-cancel');
  await page.click('#close-person-detail');
  await page.waitForSelector('#person-detail', { state: 'detached' });
  await page.click('#menu-koreksi');
  await page.waitForSelector('#correction-class');
  tegas(await page.locator('#correction-class option').count() === 1, 'wali melihat kelas lain di Koreksi');
  // P-22: wali kelas membuka laporan siswa kelasnya saja; tanpa pilihan semua kelas atau laporan guru.
  await page.click('#menu-laporan');
  await page.waitForSelector('#report-homeroom-scope');
  tegas(await page.locator('#report-subject').count() === 0, 'wali dapat memilih laporan guru');
  const homeroomClasses = await page.$$eval('#report-class option', (o) => o.map((x) => x.textContent.trim()));
  tegas(homeroomClasses.length === 1 && homeroomClasses[0] === '7A', `kelas laporan wali: ${homeroomClasses.join(', ')}`);
  await page.selectOption('#report-kind', 'Semester');
  await page.click('#report-show');
  await page.waitForSelector('#report-table');
  tegas(await baris('#report-table') === 8, `laporan wali memuat ${await baris('#report-table')} siswa, bukan 8 siswa 7A`);
  const homeroomExcel = await downloadBytes('#report-excel');
  tegas(homeroomExcel.bytes.subarray(0, 2).toString() === 'PK', 'unduhan Excel wali bukan berkas xlsx');
  await page.click('#menu-pengaturan');
  await page.waitForSelector('#reset-data');
  const catatan = await page.textContent('#isi');
  tegas(catatan.includes('dipulihkan'), `catatan: ${catatan.slice(0, 120)}`);
});

const studentHeaders = ['NISN', 'NAMA_LENGKAP', 'JENIS_KELAMIN', 'TEMPAT_LAHIR', 'TANGGAL_LAHIR',
  'ALAMAT', 'KELAS', 'NAMA_ORANG_TUA', 'NO_HP_ORANG_TUA'];
const teacherHeaders = ['NAMA', 'NIP', 'PANGKAT', 'JABATAN', 'NO_KARTU'];
const importStudentRow = (id, name, card = null) =>
  [id, name, 'L', null, '2013-06-01', null, '7A', null, null, card];
const uploadWorkbook = async (rows, name = 'data-uji.xlsx') => {
  const fixture = fileURLToPath(new URL('./master-data-fixture.py', import.meta.url));
  const buffer = execFileSync('python3', [fixture], { input: JSON.stringify(rows) });
  await page.setInputFiles('#master-import-file', {
    name,
    mimeType: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
    buffer
  });
  await page.waitForSelector('#master-import-preview', { timeout: 30_000 });
};
const editMaster = async (selector, name, field, value) => {
  await page.click(selector);
  tegas(await page.locator('#master-edit-identifier').getAttribute('readonly') !== null, 'identitas master dapat diubah');
  await page.fill('#master-edit-name', name);
  await page.fill(field, value);
  await page.click('#master-edit-save');
  await page.waitForSelector('#master-edit-success');
  await page.getByRole('button', { name: 'Tutup ubah data' }).click();
};
const closeImport = () => page.getByRole('button', { name: 'Tutup impor Excel' }).click();

await langkah('Admin mengubah master siswa dan guru tanpa mengganti identitas atau kartu', async () => {
  await page.click('#tombol-keluar');
  await page.waitForSelector('#tombol-masuk');
  await masuk('admin', 'contoh123');
  await page.waitForSelector('#ringkasan');
  await setClock('2026-11-05T07:29:00');
  await page.click('#menu-siswa');
  await editMaster('#edit-student-1', 'Siswa hasil ubah', '#master-edit-birth-place', 'Makassar');
  const student = await page.locator('#tabel-siswa tbody tr').filter({ hasText: 'Siswa hasil ubah' }).textContent();
  tegas(student.includes('9000000001') && student.includes('0000111101'), 'NISN atau kartu siswa berubah');
  await page.click('#menu-guru');
  await editMaster('#edit-teacher-1', 'Guru hasil ubah', '#master-edit-position', 'Guru Matematika');
  const teacher = await page.locator('#tabel-guru tbody tr').filter({ hasText: 'Guru hasil ubah' }).textContent();
  tegas(teacher.includes('Guru Matematika') && teacher.includes('0000222201'), 'jabatan atau kartu guru salah');
});

await langkah('Kanal notifikasi orang tua: WhatsApp memakai nomor HP langsung, pesan hanya diantre', async () => {
  let stage = 'mulai';
  try {
    const openStudent = async () => {
      await page.click('#menu-siswa');
      await page.click('#student-detail-1');
      await page.waitForSelector('#person-detail');
    };
    const closeStudent = async () => {
      await page.keyboard.press('Escape');
      await page.waitForSelector('#person-detail', { state: 'detached' });
    };
    stage = 'buka detail siswa';
    await openStudent();
    stage = 'kanal awal';
    tegas((await page.textContent('#person-detail-channel')).includes('Telegram'), 'kanal awal bukan Telegram');
    stage = 'tolak WhatsApp tanpa nomor';
    await page.selectOption('#person-channel-select', 'WhatsApp');
    await page.click('#person-channel-save');
    await page.waitForSelector('#person-channel-message.danger');
    tegas((await page.textContent('#person-channel-message')).includes('Nomor HP orang tua'), 'WhatsApp tanpa nomor HP tidak ditolak');
    tegas((await page.textContent('#person-detail-channel')).includes('Telegram'), 'kanal berubah walau ditolak');
    await closeStudent();

    stage = 'isi nomor HP';
    await page.click('#menu-siswa');
    await editMaster('#edit-student-1', 'Siswa hasil ubah', '#master-edit-phone', '080000000001');
    stage = 'simpan WhatsApp';
    await openStudent();
    await page.selectOption('#person-channel-select', 'WhatsApp');
    await page.click('#person-channel-save');
    await page.waitForSelector('#person-channel-message.positive');
    tegas((await page.textContent('#person-detail-channel')).includes('WhatsApp · 6280000000001'), 'nomor WhatsApp tidak dinormalisasi');
    await closeStudent();

    stage = 'tap terlambat';
    await setClock('2026-11-07T08:20:00');
    await tapCard('0000111101');
    await page.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('Terlambat'));
    stage = 'antrean notifikasi';
    await page.click('#menu-notifikasi');
    await page.waitForSelector('#notification-outbox');
    await page.selectOption('#notification-channel', 'WhatsApp');
    const row = await page.locator('#notification-outbox tbody tr').filter({ hasText: 'Siswa hasil ubah' }).first().textContent();
    tegas(row.includes('WhatsApp') && row.includes('TERLAMBAT') && row.includes('antre'), `pesan WhatsApp tidak diantre: ${row}`);
    await page.selectOption('#notification-channel', 'Telegram');
    tegas(await page.locator('#notification-outbox tbody tr').filter({ hasText: 'Siswa hasil ubah' }).count() === 0, 'pesan siswa WhatsApp ikut masuk antrean Telegram');

    stage = 'audit';
    await page.click('#menu-audit');
    await page.fill('#audit-search', 'ubah_kanal_notifikasi');
    tegas((await page.textContent('#audit-table')).includes('whatsapp'), 'perubahan kanal tidak tercatat di audit');
  } catch (e) {
    const drawer = await page.locator('#person-detail').innerText({ timeout: 1000 }).catch(() => '(panel detail tidak terbuka)');
    const select = await page.locator('#person-channel-select').inputValue({ timeout: 1000 }).catch(() => '(tidak ada)');
    const messageRole = await page.locator('#person-channel-message').getAttribute('role', { timeout: 1000 }).catch(() => '(tidak ada)');
    const saveDisabled = await page.locator('#person-channel-save').isDisabled({ timeout: 1000 }).catch(() => '(tidak ada)');
    throw new Error(`[${stage}] ${String(e.message).split('\n')[0]} | pilihan=${select} tombolNonaktif=${saveDisabled} peranPesan=${messageRole} | panel: ${drawer.replace(/\s+/g, ' ').slice(0, 600)}`);
  } finally {
    if (await page.locator('#person-detail').count()) {
      await page.click('#close-person-detail', { timeout: 5000 }).catch(() => {});
      await page.waitForSelector('#person-detail', { state: 'detached', timeout: 5000 }).catch(() => {});
    }
    // Later steps expect the school day used before this step.
    await setClock('2026-11-05T07:29:00').catch(() => {});
  }
});

await langkah('Impor siswa menampilkan pratinjau dan mendukung berkas dengan atau tanpa RFID', async () => {
  await page.click('#menu-siswa');
  await page.click('#student-import-excel');
  const downloadReady = page.waitForEvent('download');
  await page.click('#master-import-template');
  const download = await downloadReady;
  tegas(download.suggestedFilename() === 'template-siswa.xlsx', 'nama template Excel salah');

  await uploadWorkbook([studentHeaders, importStudentRow('0012345678', 'Siswa Excel Tanpa Kartu')]);
  tegas(await baris('#tabel-siswa') === 24, 'pratinjau sudah menambah siswa');
  await page.click('#master-import-confirm');
  await page.waitForSelector('#master-import-success');
  await closeImport();
  const noCard = await page.locator('#tabel-siswa tbody tr').filter({ hasText: 'Siswa Excel Tanpa Kartu' }).textContent();
  tegas(noCard.includes('0012345678') && noCard.includes('Belum terdaftar'), 'NISN nol depan atau status tanpa kartu salah');

  await page.click('#student-import-excel');
  await uploadWorkbook([[...studentHeaders, 'NO_KARTU'], importStudentRow('0012345679', 'Siswa Excel RFID', '0000999101')]);
  tegas((await page.textContent('#master-import-preview')).includes('0000999101'), 'RFID tidak tampak di pratinjau');
  await page.click('#master-import-confirm');
  await page.waitForSelector('#master-import-success');
  tegas((await page.textContent('#master-import-success')).includes('1 kartu ditautkan'), 'hasil tautan tidak tampil');
  await closeImport();

  await page.click('#menu-absensi');
  await tapCard('0000999101');
  await page.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('Hadir · Siswa Excel RFID'));
  const attendance = await page.locator('#attendance-table tbody tr').filter({ hasText: 'Siswa Excel RFID' }).textContent();
  tegas(attendance.includes('07:29:00'), 'kartu siswa baru tidak mencatat jam masuk');
});

await langkah('Impor guru menolak kartu milik siswa dan menyimpan satu batch valid setelah konfirmasi', async () => {
  await page.click('#menu-guru');
  await page.click('#teacher-import-excel');
  await uploadWorkbook([
    teacherHeaders,
    ['Guru Excel RFID', '000123456789012345', 'Pembina', 'Guru Kelas', '0000999102'],
    ['Guru Konflik', '000123456789012346', null, null, '0000999101']
  ]);
  tegas(await page.locator('#master-import-confirm').isDisabled(), 'impor masih bisa dikonfirmasi saat RFID konflik');
  tegas((await page.textContent('#master-import-preview')).includes('siswa atau guru lain'), 'alasan konflik RFID tidak tampil');
  tegas(await baris('#tabel-guru') === 4, 'baris valid ikut tersimpan sebelum konfirmasi');

  await uploadWorkbook([teacherHeaders, ['Guru Excel RFID', '000123456789012345', 'Pembina', 'Guru Kelas', '0000999102']], 'guru-valid.xlsx');
  await page.click('#master-import-confirm');
  await page.waitForSelector('#master-import-success');
  await closeImport();
  tegas(await baris('#tabel-guru') === 5, 'jumlah guru setelah impor salah');
});

await langkah('Perubahan master dan kartu hasil impor bertahan setelah halaman dimuat ulang', async () => {
  await page.reload();
  await page.waitForSelector('#tombol-masuk', { timeout: 120_000 });
  await masuk('admin', 'contoh123');
  await page.waitForSelector('#ringkasan');
  await page.click('#menu-siswa');
  tegas((await page.textContent('#tabel-siswa')).includes('Siswa hasil ubah'), 'edit siswa hilang');
  const student = await page.locator('#tabel-siswa tbody tr').filter({ hasText: 'Siswa Excel RFID' }).textContent();
  tegas(student.includes('0012345679') && student.includes('0000999101'), 'impor siswa tidak bertahan');
  await page.click('#menu-guru');
  tegas((await page.textContent('#tabel-guru')).includes('Guru hasil ubah'), 'edit guru hilang');
  const teacher = await page.locator('#tabel-guru tbody tr').filter({ hasText: 'Guru Excel RFID' }).textContent();
  tegas(teacher.includes('000123456789012345') && teacher.includes('0000999102'), 'impor guru tidak bertahan');
});

await langkah('Admin menambah siswa/guru dan mengganti kartu dengan konfirmasi serta penolakan UID duplikat', async () => {
  await page.click('#menu-siswa');
  await page.click('#student-add');
  await page.fill('#master-edit-identifier', '0012345680');
  await page.fill('#master-edit-name', 'Siswa tambah manual');
  await page.selectOption('#master-edit-class', '1');
  await page.click('#master-edit-save');
  await page.waitForSelector('#master-edit-success');
  await page.getByRole('button', { name: 'Tutup tambah data' }).click();
  tegas((await page.locator('#tabel-siswa tbody tr').filter({ hasText: 'Siswa tambah manual' }).textContent()).includes('0012345680'), 'NISN tidak tersimpan utuh');

  await page.click('#replace-student-card-1');
  await page.fill('#registration-uid', '0000222201');
  await page.click('#save-registration');
  await page.getByRole('dialog').getByRole('alert').waitFor();
  tegas((await page.getByRole('dialog').getByRole('alert').textContent()).includes('sudah terdaftar'), 'RFID duplikat tidak dijelaskan');
  await page.fill('#registration-uid', '0000999201');
  await page.selectOption('#replacement-reason', 'rusak');
  await page.click('#save-registration');
  await page.waitForSelector('#registration-success');
  await page.getByRole('button', { name: 'Tutup registrasi', exact: true }).click();

  await page.click('#menu-guru');
  await page.click('#teacher-add');
  await page.fill('#master-edit-identifier', '000000123456789001');
  await page.fill('#master-edit-name', 'Guru tambah manual');
  await page.click('#master-edit-save');
  await page.waitForSelector('#master-edit-success');
  await page.getByRole('button', { name: 'Tutup tambah data' }).click();
  tegas((await page.textContent('#tabel-guru')).includes('000000123456789001'), 'NIP tidak tersimpan utuh');
  await page.click('#replace-teacher-card-1');
  await page.fill('#teacher-registration-uid', '0000999202');
  await page.click('#save-teacher-registration');
  await page.waitForSelector('#teacher-registration-success');
  await page.getByRole('button', { name: 'Tutup registrasi guru' }).click();
  tegas((await page.textContent('#tabel-guru')).includes('0000999202'), 'kartu pengganti guru tidak tampil');
});

await langkah('Admin membuat kelas, mengganti wali dan melihat riwayat penugasan', async () => {
  await setClock('2026-12-01T07:29:00');
  await page.click('#menu-kelas');
  await page.click('#class-add');
  await page.fill('#class-edit-name', '9Z');
  await page.selectOption('#class-edit-level', '9');
  await page.click('#class-edit-save');
  await page.waitForSelector('#class-edit-success');
  await page.getByRole('button', { name: 'Tutup formulir kelas' }).click();
  tegas((await page.textContent('#tabel-kelas')).includes('9Z'), 'kelas baru tidak tampil');
  await page.click('#class-edit-1');
  await page.selectOption('#class-edit-teacher', '4');
  await page.click('#class-edit-save');
  await page.waitForSelector('#class-edit-success');
  await page.getByRole('button', { name: 'Tutup formulir kelas' }).click();
  await page.click('#class-edit-1');
  tegas(await page.locator('#class-homeroom-history tbody tr').count() === 2, 'wali lama tidak disimpan');
  tegas((await page.textContent('#class-homeroom-history')).includes('Pak Andi'), 'wali pengganti belum aktif');
  await page.getByRole('button', { name: 'Tutup formulir kelas' }).click();
  await page.click('#tombol-keluar');
  await page.waitForSelector('#tombol-masuk');
  await masuk('wali7a', 'contoh123');
  await page.waitForSelector('#ringkasan');
  await page.click('#menu-siswa');
  await page.waitForSelector('#tabel-siswa');
  tegas(await baris('#tabel-siswa') === 0, 'wali lama masih melihat siswa kelas 7A');
  await page.click('#tombol-keluar');
  await page.waitForSelector('#tombol-masuk');
  await masuk('admin', 'contoh123');
  await page.waitForSelector('#ringkasan');
});

await langkah('Kalender wajib alasan, hari ditutup, dan bulan belum lengkap diblokir', async () => {
  await page.click('#menu-kalender');
  await page.fill('#calendar-from', '2026-12-01');
  await page.fill('#calendar-to', '2026-12-01');
  await page.click('#calendar-save');
  await page.waitForSelector('#calendar-result[role=alert]');
  await page.fill('#calendar-reason', 'Libur pengujian');
  await page.click('#calendar-save');
  await page.waitForSelector('#calendar-result[role=status]');
  tegas((await page.textContent('#school-calendar')).includes('Libur pengujian'), 'libur yang disimpan tidak tampil');
  await page.click('#menu-notifikasi');
  tegas((await page.textContent('#school-notifications')).includes('Jadwal notifikasi tidak berlaku'), 'notifikasi masih dijadwalkan pada hari libur');

  await page.click('#menu-kalender');
  await page.getByRole('button', { name: 'Bulan sebelumnya' }).click();
  tegas((await page.textContent('#calendar-lock-readiness')).includes('belum ditutup'), 'bulan yang belum lengkap tidak diblokir');
  tegas(await page.locator('#calendar-lock').count() === 0, 'bulan belum lengkap dapat dikunci');
  await page.fill('#calendar-from', '2026-11-04');
  await page.fill('#calendar-to', '2026-11-04');
  await page.click('#calendar-finalize');
  await page.waitForSelector('#calendar-result[role=status]');
  tegas((await page.textContent('#calendar-result')).includes('04/11/2026'), 'hasil tutup hari tidak tampil');
  await page.click('#menu-audit');
  await page.fill('#audit-search', 'tandai_libur');
  tegas((await page.textContent('#audit-table')).includes('Libur pengujian'), 'perubahan kalender tidak terlihat di audit');
});

await langkah('Naik kelas massal mempertahankan kartu dan laporan periode sebelumnya', async () => {
  await page.click('#menu-kelas');
  await page.click('#academic-open');
  await page.fill('#academic-name', '2026/2027');
  await page.fill('#academic-start', '2027-01-04');
  await page.fill('#academic-end', '2027-06-30');
  await page.click('#academic-submit');
  await page.waitForSelector('#academic-result[role=status]');
  await page.getByRole('button', { name: 'Tutup tahun ajaran' }).click();

  await page.selectOption('#classes-period', '2');
  await page.click('#class-add');
  await page.fill('#class-edit-name', '8Z');
  await page.selectOption('#class-edit-level', '8');
  await page.click('#class-edit-save');
  await page.waitForSelector('#class-edit-success');
  await page.getByRole('button', { name: 'Tutup formulir kelas' }).click();

  await page.click('#academic-open');
  await page.selectOption('#academic-action', 'Promote');
  await page.selectOption('#academic-source-year', '1');
  await page.selectOption('#academic-source-class', { label: '7A' });
  const checks = page.locator('.period-students input');
  for (let i = 0; i < await checks.count(); i++) {
    const box = checks.nth(i);
    if (await box.getAttribute('id') !== 'academic-student-1') await box.uncheck();
  }
  await page.selectOption('#academic-target-year', '2');
  await page.selectOption('#academic-target-class', { label: '8Z' });
  await page.click('#academic-submit');
  await page.waitForSelector('#academic-result[role=status]');
  tegas((await page.textContent('#academic-result')).includes('1 siswa'), 'seleksi siswa tidak dipertahankan');
  await page.getByRole('button', { name: 'Tutup tahun ajaran' }).click();

  await setClock('2027-01-04T07:29:00');
  await page.click('#menu-kelas');
  await page.click('#academic-open');
  await page.selectOption('#academic-action', 'Activate');
  await page.selectOption('#academic-target-year', '2');
  await page.check('#academic-confirm');
  await page.click('#academic-submit');
  await page.waitForSelector('#academic-result[role=status]');
  await page.getByRole('button', { name: 'Tutup tahun ajaran' }).click();
  await page.click('#menu-siswa');
  const row = await page.locator('#tabel-siswa tbody tr').filter({ hasText: 'Siswa hasil ubah' }).textContent();
  tegas(row.includes('8Z') && row.includes('0000999201'), 'kelas baru atau kartu lama hilang');

  await page.click('#menu-laporan');
  await page.selectOption('#report-period', '1');
  await page.selectOption('#report-class', '1');
  await page.fill('#report-date', '2026-11-04');
  await page.click('#report-show');
  await page.waitForSelector('#report-table');
  const history = await page.locator('#report-table tbody tr').filter({ hasText: 'Siswa hasil ubah' }).textContent();
  tegas(history.includes('7A') && history.includes('07:30:00'), 'riwayat laporan kelas asal berubah');
});


await langkah('Rentang periode dapat diperbaiki; tanggal yang sudah tercatat tetap terlindungi', async () => {
  await page.click('#menu-kelas');
  await page.click('#academic-open');
  await page.selectOption('#academic-action', 'ChangeDates');
  await page.selectOption('#academic-edit-year', '2');
  tegas(await page.inputValue('#academic-edit-end') === '2027-06-30', 'tanggal akhir periode tidak terisi');
  await page.fill('#academic-edit-end', '2027-06-26');
  await page.click('#academic-submit');
  await page.waitForSelector('#academic-result[role=status]');
  tegas((await page.textContent('#academic-result')).includes('26/06/2027'), 'hasil ubah rentang tidak tampil');

  await page.selectOption('#academic-edit-year', '1');
  await page.fill('#academic-edit-end', '2026-11-01');
  await page.click('#academic-submit');
  await page.waitForSelector('#academic-result[role=alert]');
  tegas((await page.textContent('#academic-result')).includes('Absensi sudah tercatat'), 'tanggal tercatat tidak dilindungi');
  await page.getByRole('button', { name: 'Tutup tahun ajaran' }).click();
});

await langkah('Mutasi siswa dan nonaktif guru: data tetap, kartu siswa dinonaktifkan, tercatat di audit', async () => {
  await page.click('#menu-siswa');
  const studentRow = page.locator('#tabel-siswa tbody tr').filter({ hasText: 'Siswa Excel RFID' });
  await studentRow.locator('[id^=mutate-student-]').click();
  await page.waitForSelector('#mutation-title');
  await page.selectOption('#mutation-reason', 'pindah');
  await page.fill('#mutation-note', 'ke SMP Negeri 2 (contoh)');
  await page.click('#mutation-confirm');
  await page.waitForSelector('#mutation-success');
  await page.click('#mutation-done');
  await page.waitForSelector('#mutation-title', { state: 'detached' });
  const student = await studentRow.textContent();
  tegas(student.includes('Tidak aktif') && student.includes('pindah: ke SMP Negeri 2') && student.includes('Belum terdaftar'), `mutasi siswa salah: ${student}`);
  tegas(await studentRow.locator('[id^=mutate-student-]').count() === 0, 'siswa tidak aktif masih bisa dimutasi');

  await page.click('#menu-absensi');
  await tapCard('0000999101');
  await page.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('Kartu belum terdaftar'));

  await page.click('#menu-guru');
  const teacherRow = page.locator('#tabel-guru tbody tr').filter({ hasText: 'Guru Excel RFID' });
  await teacherRow.locator('[id^=deactivate-teacher-]').click();
  await page.selectOption('#mutation-reason', 'pensiun');
  await page.click('#mutation-confirm');
  await page.waitForSelector('#mutation-success');
  await page.click('#mutation-done');
  tegas((await teacherRow.textContent()).includes('pensiun'), 'guru tidak tercatat pensiun');

  await page.click('#menu-audit');
  await page.fill('#audit-search', 'nonaktifkan_siswa');
  tegas((await page.textContent('#audit-table')).includes('kartu_aktif'), 'penonaktifan kartu tidak tercatat di audit');
});

await langkah('Akun: admin membuat wali kelas, sandi diatur ulang (P-21) lalu wajib diganti, akun dinonaktifkan tidak bisa masuk', async () => {
  await page.click('#menu-pengaturan');
  await page.waitForSelector('#account-management');
  tegas(await page.locator('#account-role option').count() === 1, 'admin masih bisa memilih peran Admin');
  await page.fill('#account-username', 'wali8a');
  await page.fill('#account-password', 'pendek');
  await page.selectOption('#account-teacher', { label: 'Ibu Lina (contoh)' });
  await page.click('#account-create');
  await page.waitForSelector('#account-message.danger');
  tegas((await page.textContent('#account-message')).includes('minimal 8'), 'sandi pendek tidak ditolak');
  await page.fill('#account-password', 'sandiwali8a');
  await page.click('#account-create');
  await page.waitForSelector('#account-message.positive');
  const account = page.locator('#account-table tbody tr').filter({ hasText: 'wali8a' });
  tegas((await account.textContent()).includes('Ibu Lina'), 'akun wali tidak tertaut ke guru');
  await page.fill('#account-username', 'wali8a');
  await page.fill('#account-password', 'sandiwali8a');
  await page.selectOption('#account-teacher', { label: 'Ibu Lina (contoh)' });
  await page.click('#account-create');
  await page.waitForFunction(() => document.querySelector('#account-message')?.textContent?.includes('sudah dipakai'));

  await account.locator('[id^=account-reset-]').click();
  await page.fill('#account-reset-password', 'sandibaru123');
  await page.click('#account-reset-confirm');
  await page.waitForFunction(() => document.querySelector('#account-message')?.textContent?.includes('wajib membuat sandi baru'));
  // P-21: Admin dapat mengatur ulang sandi Kepala Sekolah, tetapi tidak Admin lain atau dirinya sendiri.
  const headRow = page.locator('#account-table tbody tr').filter({ hasText: 'Kepala Sekolah' }).first();
  tegas(await headRow.locator('[id^=account-reset-]').count() === 1, 'admin tidak dapat mengatur ulang sandi kepala sekolah');
  tegas(await headRow.locator('[id^=account-deactivate-]').count() === 0, 'admin dapat menonaktifkan kepala sekolah');
  await headRow.locator('[id^=account-reset-]').click();
  await page.fill('#account-reset-password', 'sementara123');
  await page.click('#account-reset-confirm');
  await page.waitForFunction(() => document.querySelector('#account-message')?.textContent?.includes('Sandi akun kepsek diatur ulang'));
  await page.click('#tombol-keluar');

  // Pemilik akun yang diatur ulang wajib membuat sandi baru sebelum masuk ke aplikasi.
  await masuk('wali8a', 'sandibaru123');
  await page.waitForSelector('#password-change-required');
  tegas(await page.locator('#akun-aktif').count() === 0, 'aplikasi terbuka sebelum sandi baru dibuat');
  await page.fill('#password-current', 'sandibaru123');
  await page.fill('#password-new', 'sandiwali8b');
  await page.fill('#password-confirm', 'sandiwali8c');
  await page.click('#password-change-save');
  await page.waitForFunction(() => document.querySelector('#password-change-message')?.textContent?.includes('tidak sama'));
  await page.fill('#password-confirm', 'sandiwali8b');
  await page.click('#password-change-save');
  await page.waitForSelector('#akun-aktif');
  await page.click('#tombol-keluar');
  await masuk('wali8a', 'sandiwali8b');
  await page.waitForSelector('#akun-aktif');
  tegas(await page.locator('#password-change-required').count() === 0, 'kewajiban ganti sandi muncul lagi');
  await page.click('#tombol-keluar');

  await masuk('kepsek', 'sementara123');
  await page.waitForSelector('#password-change-required');
  await page.fill('#password-current', 'sementara123');
  await page.fill('#password-new', 'contoh123');
  await page.fill('#password-confirm', 'contoh123');
  await page.click('#password-change-save');
  await page.waitForSelector('#ringkasan');
  await page.click('#tombol-keluar');

  await masuk('admin', 'contoh123');
  await page.waitForSelector('#ringkasan');
  await page.click('#menu-pengaturan');
  await page.locator('#account-table tbody tr').filter({ hasText: 'wali8a' }).locator('[id^=account-deactivate-]').click();
  await page.click('#account-deactivate-confirm');
  await page.waitForFunction(() => document.querySelector('#account-message')?.textContent?.includes('dinonaktifkan'));
  tegas((await page.locator('#account-table tbody tr').filter({ hasText: 'wali8a' }).textContent()).includes('Nonaktif'), 'status akun tidak berubah');
  await page.click('#tombol-keluar');
  await masuk('wali8a', 'sandiwali8b');
  await page.waitForSelector('#galat-masuk');

  await masuk('kepsek', 'contoh123');
  await page.waitForSelector('#ringkasan');
  await page.click('#menu-pengaturan');
  tegas(await page.locator('#account-role option').count() === 2, 'kepala sekolah tidak bisa memilih peran Admin');
});

await langkah('Identitas sekolah: NPSN salah ditolak, nama dan logo baru tampil di judul aplikasi', async () => {
  await page.click('#menu-pengaturan');
  await page.waitForSelector('#school-identity');
  await page.fill('#school-name', 'SMP Uji Ubah (contoh)');
  await page.fill('#school-npsn', '1234');
  await page.click('#school-identity-save');
  await page.waitForSelector('#school-identity-message.danger');
  tegas((await page.textContent('#school-identity-message')).includes('8 angka'), 'NPSN 4 angka tidak ditolak');
  tegas(!(await page.textContent('#judul-sekolah')).includes('Ubah'), 'nama berubah walau ditolak');
  await page.fill('#school-npsn', '12345678');
  await page.click('#school-identity-save');
  await page.waitForSelector('#school-identity-message.positive');
  tegas((await page.textContent('#judul-sekolah')).trim() === 'SMP Uji Ubah (contoh)', 'judul aplikasi tidak memakai nama baru');

  const logo = Buffer.from(await page.evaluate(() => {
    const c = document.createElement('canvas'); c.width = 120; c.height = 80;
    const g = c.getContext('2d'); g.fillStyle = '#1b7f4c'; g.fillRect(0, 0, 120, 80);
    return c.toDataURL('image/png').split(',')[1];
  }), 'base64');
  await page.setInputFiles('#school-logo-input', { name: 'catatan.txt', mimeType: 'text/plain', buffer: Buffer.from('bukan gambar') });
  await page.waitForSelector('#school-logo-message.danger');
  await page.setInputFiles('#school-logo-input', { name: 'logo-contoh.png', mimeType: 'image/png', buffer: logo });
  await page.waitForSelector('#school-logo-message.positive');
  await page.waitForSelector('#school-logo-preview');
  await page.waitForSelector('#school-logo-header');
});

await langkah('Jadwal berlaku mulai besok (P-15): hari ini tetap jadwal lama, tap besok memakai jadwal baru', async () => {
  await page.click('#menu-pengaturan');
  await page.waitForSelector('#schedule-settings');
  tegas(await page.inputValue('#schedule-effective') === '2027-01-05', 'tanggal berlaku bawaan bukan besok');
  await page.fill('#schedule-arrival-2', '09:00');
  await page.click('#schedule-save');
  await page.waitForSelector('#schedule-message.danger');
  tegas((await page.textContent('#schedule-message')).startsWith('Selasa'), 'urutan jam salah tidak ditolak');
  await page.fill('#schedule-arrival-2', '07:00');
  await page.click('#schedule-save');
  await page.waitForSelector('#schedule-message.positive');
  tegas((await page.textContent('#schedule-view-table tr[data-day="2"]')).includes('07:00'), 'jadwal baru tidak tampil untuk tanggal berlakunya');
  tegas((await page.textContent('#schedule-versions')).includes('05/01/2027'), 'versi jadwal tidak tercantum');
  await page.fill('#schedule-view-date', '2027-01-04');
  await page.waitForFunction(() => document.querySelector('#schedule-view-table tr[data-day="2"]')?.textContent?.includes('07:30'));

  await setClock('2027-01-05T07:15:00');
  await tapCard('0000999201');
  await page.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('Terlambat ·'));
});

await langkah('Kehadiran guru (§16.17, P-21): kepala sekolah menandai Dinas luar di muka, admin juga dapat menetapkan status', async () => {
  await page.click('#menu-guru');
  await page.waitForSelector('#teacher-attendance');
  await page.fill('#teacher-attendance-date', '2027-01-08');
  const row = page.locator('#teacher-attendance-table tbody tr').filter({ hasText: 'Ibu Lina (contoh)' });
  await row.locator('[id^=teacher-status-]').click();
  await page.waitForSelector('#teacher-status-form');
  tegas(await page.locator('#teacher-status-value option').count() === 3, 'pilihan di muka bukan Izin/Dinas luar/Cuti');
  await page.selectOption('#teacher-status-value', 'DinasLuar');
  await page.click('#teacher-status-save');
  await page.waitForSelector('#teacher-status-message.danger');
  await page.fill('#teacher-status-note', 'Surat tugas 12 (contoh)');
  await page.click('#teacher-status-save');
  await page.waitForSelector('#teacher-status-message.positive');
  const stored = await row.textContent();
  tegas(stored.includes('Dinas luar') && stored.includes('Surat tugas 12'), `status guru tidak tersimpan: ${stored}`);

  await page.click('#tombol-keluar');
  await page.waitForSelector('#login-logo');
  tegas((await page.textContent('#status-lisensi')).trim() === 'Lisensi: Aktif', 'status lisensi tidak tampil di layar masuk');
  await masuk('admin', 'contoh123');
  await page.waitForSelector('#ringkasan');
  await page.click('#menu-guru');
  await page.fill('#teacher-attendance-date', '2027-01-08');
  const adminRow = page.locator('#teacher-attendance-table tbody tr').filter({ hasText: 'Ibu Lina (contoh)' });
  await page.waitForFunction(() => document.querySelector('#teacher-attendance-table')?.textContent?.includes('Dinas luar'));
  tegas(await adminRow.locator('[id^=teacher-status-]').count() === 1, 'admin tidak dapat menetapkan status guru (P-21)');
});

await langkah('Audit (admin) mencatat identitas sekolah, jadwal berlaku mulai, dan status guru; saringan akun bekerja', async () => {
  await page.click('#menu-audit');
  for (const [action, expected] of [['ubah_identitas_sekolah', 'SMP Uji Ubah'], ['ubah_logo_sekolah', 'logo-sekolah.png'], ['jadwal_berlaku_mulai', 'berlaku_mulai=2027-01-05'], ['tetapkan_status_guru', 'dinas_luar']]) {
    await page.fill('#audit-search', action);
    await page.waitForFunction(([a, e]) => { const t = document.querySelector('#audit-table')?.textContent ?? ''; return t.includes(a) && t.includes(e); }, [action, expected]);
  }
  await page.fill('#audit-search', 'ubah_identitas_sekolah');
  await page.selectOption('#audit-account', 'admin');
  await page.waitForFunction(() => !(document.querySelector('#audit-table')?.textContent ?? '').includes('ubah_identitas_sekolah'));
  await page.selectOption('#audit-account', 'kepsek');
  await page.waitForFunction(() => (document.querySelector('#audit-table')?.textContent ?? '').includes('ubah_identitas_sekolah'));
});

await langkah('Wizard instalasi web: mulai kosong, isian salah ditolak, akun kepala sekolah baru bisa masuk dan data bertahan setelah muat ulang', async () => {
  await page.click('#menu-pengaturan');
  await page.click('#reset-data');
  await page.click('#confirm-reset-wizard');
  await page.waitForSelector('#install-wizard');
  await page.click('#wizard-next');
  await page.waitForSelector('#wizard-error');
  await page.fill('#wizard-license', 'LISENSI-UJI (contoh)');
  await page.click('#wizard-next');
  await page.fill('#wizard-school', 'SMP Wizard (contoh)');
  await page.click('#wizard-next');
  await page.waitForSelector('#wizard-period-name');
  await page.click('#wizard-next');
  await page.waitForSelector('#wizard-schedule');
  await page.click('#wizard-next');
  await page.fill('#wizard-username', 'kepala.baru');
  await page.fill('#wizard-password', 'pendek');
  await page.fill('#wizard-password-confirm', 'pendek');
  await page.click('#wizard-finish');
  await page.waitForFunction(() => document.querySelector('#wizard-error')?.textContent?.includes('minimal 8'));
  await page.fill('#wizard-password', 'sandibaru123');
  await page.fill('#wizard-password-confirm', 'sandibaru123');
  await page.click('#wizard-finish');
  await page.waitForSelector('#wizard-done');
  await page.click('#wizard-to-login');
  await masuk('kepala.baru', 'sandibaru123');
  await page.waitForSelector('#ringkasan');
  tegas((await page.textContent('#judul-sekolah')).trim() === 'SMP Wizard (contoh)', 'nama sekolah dari wizard tidak dipakai');
  await page.reload();
  await page.waitForSelector('#tombol-masuk');
  tegas((await page.textContent('#nama-sekolah')).trim() === 'SMP Wizard (contoh)', 'hasil wizard hilang setelah muat ulang');
  await masuk('kepala.baru', 'sandibaru123');
  await page.waitForSelector('#ringkasan');
});

await langkah('Perubahan tab lama ditolak tanpa menimpa database atau mereset data tab lain', async () => {
  const stale = await context.newPage();
  try {
    await stale.goto(baseUrl);
    await stale.waitForSelector('#tombol-masuk');
    await stale.fill('#username', 'kepala.baru');
    await stale.fill('#sandi', 'sandibaru123');
    await stale.click('#tombol-masuk');
    await stale.click('#menu-pengaturan');
    await stale.waitForSelector('#school-identity');

    await page.click('#menu-pengaturan');
    await page.fill('#school-name', 'SMP Audit Antartab (contoh)');
    await page.click('#school-identity-save');
    await page.waitForSelector('#school-identity-message.positive');

    await stale.fill('#school-address', 'Alamat sintetis dari tab lama');
    await stale.click('#school-identity-save');
    await stale.waitForSelector('#school-identity-message.danger');
    tegas((await stale.textContent('#school-identity-message')).includes('tab lain'), 'konflik tidak dijelaskan');
    await stale.waitForSelector('#retry-storage:disabled');
    await stale.click('#reset-data');
    await stale.click('#confirm-reset-data');

    await page.reload();
    await page.waitForSelector('#tombol-masuk');
    tegas((await page.textContent('#nama-sekolah')).trim() === 'SMP Audit Antartab (contoh)', 'penyimpanan dari tab baru hilang');
    await masuk('kepala.baru', 'sandibaru123');
    await page.click('#menu-pengaturan');
    tegas((await page.inputValue('#school-address')) !== 'Alamat sintetis dari tab lama', 'perubahan tab lama ikut tersimpan');
  } finally {
    await stale.close();
  }
});

await langkah('Cadangan web: unduh .zip, berkas salah ditolak, pulihkan mengembalikan data dan logo', async () => {
  await page.click('#menu-pengaturan');
  await page.waitForSelector('#backup-download');
  const logo = Buffer.from(await page.evaluate(() => {
    const c = document.createElement('canvas'); c.width = 90; c.height = 90;
    const g = c.getContext('2d'); g.fillStyle = '#1b3a6b'; g.fillRect(0, 0, 90, 90);
    return c.toDataURL('image/png').split(',')[1];
  }), 'base64');
  await page.setInputFiles('#school-logo-input', { name: 'logo-cadangan.png', mimeType: 'image/png', buffer: logo });
  await page.waitForSelector('#school-logo-message.positive');
  const nameBefore = (await page.textContent('#judul-sekolah')).trim();

  const backup = await downloadBytes('#backup-download');
  tegas(/^absensi-cadangan-\d{8}-\d{4}\.zip$/.test(backup.name), `nama berkas cadangan ${backup.name}`);
  tegas(backup.bytes.subarray(0, 2).toString() === 'PK', 'cadangan bukan berkas zip');

  await page.fill('#school-name', 'SMP Setelah Cadangan (contoh)');
  await page.click('#school-identity-save');
  await page.waitForSelector('#school-identity-message.positive');

  await page.setInputFiles('#backup-file', { name: 'bukan-cadangan.zip', mimeType: 'application/zip', buffer: Buffer.from('bukan zip') });
  await page.click('#backup-restore');
  await page.waitForSelector('#backup-message.danger');
  tegas((await page.textContent('#judul-sekolah')).includes('Setelah Cadangan'), 'berkas salah mengubah data');

  await page.setInputFiles('#backup-file', { name: backup.name, mimeType: 'application/zip', buffer: backup.bytes });
  await page.click('#backup-restore');
  await page.waitForSelector('#tombol-masuk');
  tegas((await page.textContent('#nama-sekolah')).trim() === nameBefore, 'nama sekolah tidak kembali ke isi cadangan');
  await page.waitForSelector('#login-logo');
  await page.reload();
  await page.waitForSelector('#tombol-masuk');
  tegas((await page.textContent('#nama-sekolah')).trim() === nameBefore, 'hasil pemulihan hilang setelah muat ulang');
  await page.waitForSelector('#login-logo');
  await masuk('kepala.baru', 'sandibaru123');
  await page.waitForSelector('#ringkasan');
});

// Aplikasi dibiarkan terbuka melewati tengah malam tanpa muat ulang (PRD §16.7).
// Jam browser dikendalikan Playwright, bukan jam skenario, karena jam skenario
// selalu membuka hari secara eksplisit dan menutupi bug ini.
await langkah('Tap setelah tengah malam tanpa muat ulang tersimpan di hari baru', async () => {
  const nightContext = await browser.newContext();
  const night = await nightContext.newPage();
  night.on('pageerror', (e) => consoleErrors.push(String(e)));
  try {
    // Kamis 05/11/2026 23.59.50 WITA (UTC+8).
    await night.clock.setFixedTime(new Date('2026-11-05T15:59:50Z'));
    await night.goto(baseUrl);
    await night.waitForSelector('#tombol-masuk', { timeout: 120_000 });
    await night.fill('#username', 'kepsek');
    await night.fill('#sandi', 'contoh123');
    await night.click('#tombol-masuk');
    await night.waitForSelector('#ringkasan');
    tegas((await night.textContent('#jam-simulasi')).includes('05/11/2026 23:59'), `jam aplikasi tidak mengikuti jam browser: ${await night.textContent('#jam-simulasi')}`);

    await night.click('#menu-siswa');
    await night.click('#register-card-1');
    await night.fill('#registration-uid', '0000333301');
    await night.click('#save-registration');
    await night.waitForSelector('#registration-success');
    await night.getByRole('button', { name: 'Tutup registrasi', exact: true }).click();
    await night.click('#menu-absensi');
    await night.waitForSelector('#tap-uid');

    // Jumat 06/11/2026 07.00 WITA, halaman tetap sama.
    await night.clock.setFixedTime(new Date('2026-11-05T23:00:00Z'));
    await night.waitForTimeout(2_500);
    await night.locator('#tap-uid').pressSequentially('0000333301');
    await night.press('#tap-uid', 'Enter');
    await night.waitForFunction(() => document.querySelector('#tap-result')?.textContent?.includes('Hadir ·'));
    const row = night.locator('#attendance-table tbody tr').filter({ hasText: 'Siswa 7A-1' });
    tegas(await row.count() === 1, `baris Siswa 7A-1 hari baru = ${await row.count()}`);
    tegas((await row.textContent()).includes('07:00:00'), `jam masuk hari baru tidak tersimpan: ${(await row.textContent()).replace(/\s+/g, ' ')}`);

    await night.reload();
    await night.waitForSelector('#tombol-masuk', { timeout: 120_000 });
    await night.fill('#username', 'kepsek');
    await night.fill('#sandi', 'contoh123');
    await night.click('#tombol-masuk');
    await night.click('#menu-absensi');
    await night.waitForSelector('#attendance-table');
    const saved = night.locator('#attendance-table tbody tr').filter({ hasText: 'Siswa 7A-1' });
    tegas((await saved.textContent()).includes('07:00:00'), 'tap hari baru hilang setelah muat ulang');
  } finally {
    await nightContext.close();
  }
});

if (realUrl) {
  await langkah('Mode nyata: mulai dari wizard, tanpa akun contoh dan jam skenario, label uji coba data asli', async () => {
    const realContext = await browser.newContext();
    const real = await realContext.newPage();
    real.on('pageerror', (e) => consoleErrors.push(String(e)));
    try {
      await real.goto(realUrl);
      await real.waitForSelector('#install-wizard', { timeout: 60_000 });
      await real.fill('#wizard-license', 'LISENSI-NYATA (contoh)');
      await real.click('#wizard-next');
      await real.fill('#wizard-school', 'SMP Mode Nyata (contoh)');
      await real.click('#wizard-next');
      await real.waitForSelector('#wizard-period-name');
      await real.click('#wizard-next');
      await real.waitForSelector('#wizard-schedule');
      await real.click('#wizard-next');
      await real.fill('#wizard-username', 'kepala.nyata');
      await real.fill('#wizard-password', 'sandinyata123');
      await real.fill('#wizard-password-confirm', 'sandinyata123');
      await real.click('#wizard-finish');
      await real.waitForSelector('#wizard-done');
      await real.click('#wizard-to-login');
      await real.waitForSelector('#tombol-masuk');
      tegas(await real.locator('.test-accounts').count() === 0, 'akun contoh tampil di mode nyata');
      tegas((await real.textContent('.login-card .environment-tag')).includes('Uji coba data asli'), 'label mode nyata tidak tampil di login');
      await real.fill('#username', 'kepala.nyata');
      await real.fill('#sandi', 'sandinyata123');
      await real.click('#tombol-masuk');
      await real.waitForSelector('#ringkasan');
      tegas((await real.textContent('.topbar .environment-tag')).includes('Uji coba data asli'), 'label mode nyata tidak tampil');
      await real.click('#menu-absensi');
      await real.waitForSelector('#tap-uid');
      tegas(!(await real.textContent('#isi')).includes('waktu uji'), 'petunjuk waktu uji tampil di mode nyata');
      for (const menu of ['dashboard', 'siswa', 'guru', 'kelas', 'kalender', 'laporan', 'notifikasi', 'pengaturan']) {
        if (await real.locator(`#menu-${menu}`).count() === 0) continue;
        await real.click(`#menu-${menu}`);
        await real.waitForTimeout(300);
        const text = await real.textContent('#isi');
        const leftover = text.match(/web uji|browser uji|waktu uji|data contoh/i);
        tegas(!leftover, `teks mode uji "${leftover?.[0]}" tampil di menu ${menu} mode nyata`);
      }
      await real.click('#menu-pengaturan');
      await real.waitForSelector('#backup-download');
      tegas(!(await real.textContent('#isi')).includes('selesaikan wizard'), 'catatan penyimpanan masih menyuruh wizard');
      await real.waitForSelector('#storage-persistence');
      tegas(await real.locator('#scenario-time').count() === 0, 'jam skenario tampil di mode nyata');
      await real.click('#reset-data');
      tegas(await real.locator('#confirm-reset-data').count() === 0, 'pilihan data contoh tampil di mode nyata');
      await real.waitForSelector('#confirm-reset-wizard');
      await real.reload();
      await real.waitForSelector('#tombol-masuk');
      tegas((await real.textContent('#nama-sekolah')).trim() === 'SMP Mode Nyata (contoh)', 'data mode nyata hilang setelah muat ulang');
    } finally {
      await realContext.close();
    }
  });
}

await browser.close();
for (const t of consoleErrors) console.log(`::warning::Konsol browser: ${t}`);
console.log(`Ringkasan: ${failed === 0 ? 'semua langkah lulus' : `${failed} langkah gagal`}.`);
process.exit(failed === 0 ? 0 : 1);


