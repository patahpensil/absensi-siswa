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

await langkah('Laporan PDF: hasil di browser dicatat apa adanya', async () => {
  await page.selectOption('#report-kind', 'Daily');
  await page.fill('#report-date', '2026-11-04');
  await page.click('#report-show');
  await page.waitForSelector('#report-table');
  const pending = page.waitForEvent('download', { timeout: 60_000 }).catch(() => null);
  await page.click('#report-pdf');
  await page.waitForSelector('#report-message', { timeout: 60_000 });
  let result = `ditolak: ${await page.textContent('#report-message')}`;
  if (await page.locator('#report-message.positive').count() === 1) {
    const download = await pending;
    const chunks = [];
    for await (const chunk of await download.createReadStream()) chunks.push(chunk);
    const bytes = Buffer.concat(chunks);
    result = `unduhan ${download.suggestedFilename()} ${bytes.length} B, header ${bytes.subarray(0, 4).toString()}`;
  }
  console.log(`::notice::PDF laporan — ${result}`);
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
  tegas((await tv.textContent('#gate-cards')).includes('GURU'), 'label guru hilang');
  tegas(await tv.locator('#gate-cards .card').count() >= 2, 'tap siswa dan guru tidak tampil bersamaan');
  tegas(await tv.locator('#gate-cards img.photo').count() >= 1, 'foto guru yang sudah diunggah tidak tampil');

  await page.click('#menu-dashboard');
  tegas(await page.locator('#ringkasan').count() === 1 && !tv.isClosed(), 'dashboard dan layar TV tidak dapat dibuka terpisah');
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
  for (const size of [{ width: 1440, height: 900 }, { width: 1366, height: 768 }, { width: 768, height: 1024 }, { width: 390, height: 844 }]) {
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

await langkah('Menu yang belum dibangun diberi tahu apa adanya', async () => {
  await page.click('#menu-kalender');
  await page.waitForSelector('#belum-dibangun');
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
  await page.click('#menu-siswa');
  await page.waitForSelector('#tabel-siswa');
  tegas(await baris('#tabel-siswa') === 8, 'wali melihat siswa di luar kelasnya');
  tegas((await page.textContent('#tabel-siswa')).includes('0000111101'), 'kartu tidak bertahan setelah reload');
  tegas(await page.locator('[id^="register-card-"]').count() === 0, 'wali mendapat tindakan registrasi kartu');
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
  await page.click('#menu-laporan');
  await page.waitForSelector('#report-not-allowed');
  await page.click('#menu-pengaturan');
  await page.waitForSelector('#reset-data');
  const catatan = await page.textContent('#isi');
  tegas(catatan.includes('dipulihkan'), `catatan: ${catatan.slice(0, 120)}`);
});

await browser.close();
for (const t of consoleErrors) console.log(`::warning::Konsol browser: ${t}`);
console.log(`Ringkasan: ${failed === 0 ? 'semua langkah lulus' : `${failed} langkah gagal`}.`);
process.exit(failed === 0 ? 0 : 1);


