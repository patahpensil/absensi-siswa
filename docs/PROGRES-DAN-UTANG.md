# Progres dan Utang — checkpoint 2026-11-02

Satu-satunya folder kerja proyek: `D:\Project Absensi SMP 1`. Berkas ini adalah
ringkasan status terkini; rincian per butir ada di `MASTER-BUG-UIUX-REGISTER.md`,
`TODO-VERSI-BERIKUTNYA.md`, dan `RENCANA-TUGAS.md`.

## 1. Kondisi
- Tes Release: **592 hijau** (Domain 54, Laporan 32, Data 241, Telegram 10, App 255).
- Repo git lokal, cabang `master`. **Belum ada remote GitHub** (`git remote -v` kosong) —
  tidak ada yang bisa di-push sampai pemilik menambahkan remote.

## 2. Selesai pada checkpoint ini (hasil pembacaan menyeluruh 2026-11-02)
| # | Perbaikan | Tes |
|---|---|---|
| T1 | `Penjadwal.DetakAsync` kini dilindungi bendera re-entrancy: detak yang tumpang tindih dilewati, pesan outbox tidak terkirim ganda. | `DetakAsync_DetakSebelumnyaMasihBerjalan_DetakBerikutnyaDilewati` |
| T2 | `IKlienBotTelegram.KirimPesanAsync` mengembalikan `HasilKirimPesan` (Berhasil / GagalJaringan / Ditolak). Tanpa internet atau 429/5xx **tidak** lagi menghabiskan jatah 5 percobaan dan menghentikan siklus; hanya penolakan nyata (4xx) yang dihitung (PRD §9.4). | 2 tes baru `PengirimOutboxTests` |
| T4 | `token_bot` (dan kunci berisi "token"/"sandi") tidak lagi ditulis ke `audit.nilai_baru`; yang tercatat `(disamarkan)`. | `LayananPengaturanAuditTests` (2) |
| T5 | `LayananAkun.ResetSandi`/`NonaktifkanAkun` memeriksa peran **target**: Admin hanya atas Wali Kelas (PRD §3.1); tidak ada yang bisa menonaktifkan dirinya sendiri; target tak ada ditolak. | 6 tes baru `LayananAkunTests` |
| T7 | Foto siswa/guru dan logo di PDF/Cetak laporan kini di-resolve dari `data/foto` / `data/` (sebelumnya `File.Exists` pada nama polos → tak pernah tercetak). | — (butuh cek visual UAT) |
| T9 | `GerbangViewModel.ProsesTap` menangkap galat yang bisa dipulihkan (tahun ajaran hilang, SQLite sibuk): kartu "Tap belum tercatat", bunyi gagal, aplikasi tidak tertutup. | — (butuh UAT gerbang) |
| doc | Register A-38/A-39 disinkronkan dengan commit; `PANDUAN-PEMASANGAN` tidak lagi menyebut "Impor foto" / restore "dari cloud". | |

## 3. Utang yang MASIH TERBUKA (urut prioritas)
**Dari pembacaan kode, belum dikerjakan:**
1. **T3** Status Bolos hanya ditulis di job notifikasi (ikut toleransi 15 menit); `FinalisasiAkhirHariGuru` hanya jalan saat pergantian tanggal di dalam proses. PC mati pada jamnya → status tidak pernah ditetapkan. Perlu keputusan produk (final saat start untuk hari-hari lampau).
2. **T6** Cek peran di lapisan layanan untuk `LayananSiswa/Guru/Kelas/Pengaturan/Pairing/Impor` (CLAUDE.md §4.5 aturan 19; A-42 hanya menyebut `LayananGuru`).
3. **T8** Libur ditandai setelah hari dibuka meninggalkan baris `alpa` yang ikut rekap (belum diuji).
4. Jaga `GuruViewModel.Simpan` dengan `PenangananGalat.Coba`; `TapKartuTunggalViewModel.TapUid` dan `AkunViewModel.Tambah/Nonaktifkan` hanya menangkap `InvalidOperationException`.
5. `HashSandi.Verifikasi` tak menjaga hash rusak; backup lokal tak terenkripsi; cadangan pra-restore ikut rotasi 7.
6. Token bot lama yang **sudah** tertulis di audit instalasi yang ada tidak bisa dihapus (audit immutable) — sarankan ganti token lewat BotFather.
7. Tes yang belum ada: foto/logo di PDF (T7), `ProsesTap` galat (T9).

**Terbuka dari register (tidak berubah):** Bug #11 UAT alur lengkap; A-41/A-42 UAT; A-44 tap RFID hilang di dua monitor; A-45 Playbook; A-7/D-1 crash `STATUS_STACK_OVERFLOW` belum tereproduksi (bukti di `arsip/bukti-uji-c/`); A-4/5/6; A-14/15/16/17 redesain UI; A-19 tes `KlienBotTelegram`; A-21/22/23 (A-23 logo header masih placeholder); A-24 Dashboard belum dibangun; TODO B.2; cadangan sekunder masih lokal; `ImporFotoSiswa/Guru` tanpa entry point UI; lisensi masih stub (PRD: aktivasi online); `README.md` perlu memuat 5 projek tes.

## 4. Proyek Python lama (DIBEKUKAN) — diarsipkan di `arsip/`
Disalin ke `arsip/proyek-python-lama/` (`sumber/` + `riwayat-git-lengkap.bundle` = seluruh riwayat git;
pulihkan dengan `git clone riwayat-git-lengkap.bundle`). Referensi (PRD .docx v2.0, draf, `smp.txt`,
skema) di `arsip/referensi/`. Bukti uji dari build uji lama C: di `arsip/bukti-uji-c/` (catatan,
berkas uji, log; dump crash `.dmp` 1,2 GB tidak ikut git, SHA-256:
`10108` = DBE823A1DEBC114234925CD75F8E0D3A5311C28ED819BA3C0DD83634DB01E2B4,
`7020` = D1D711BF172F72A76E724621743EE28DD09DB1EA5D461D1F0A98695A91EE238C).

Utang proyek Python (jika suatu hari dilanjutkan): Slice 14 Pencadangan; semantik ALPA scan terbuffer
(wajib sebelum WhatsApp aktif); pemicu notifikasi nyata (`antrekan()` tak pernah dipanggil);
ambang koreksi massal hardcode 5000; lampiran guru tak terenkripsi; laporan guru; tabel `devices` mati;
antrean lobi terblokir item yang ditolak server. **Bug terbukti 2026-11-02 (belum diperbaiki):**
(a) edit sel apa pun pada baris siswa di tabel Data Master menghapus NIK (NIK dimuat kosong lalu ditulis
`None`); (b) nomor WA orang tua yang ditambahkan lewat edit tidak mengaktifkan `menerima_notifikasi`.
Lainnya (bacaan): lobi hanya memakai `MockReader`, layar koreksi/laporan meminta ID angka,
Pengaturan bisa menimpa jam dengan 00:00 bila nilai belum ada, PDF Helvetica gagal pada karakter non-Latin-1.

## 5. Pembersihan
Folder di luar proyek yang terkait absensi dihapus setelah isinya diarsipkan dan diverifikasi
(jumlah berkas + SHA-256 untuk dump): `D:\Absensi SMP 1`, `D:\smp.txt`, `D:\master pembuatan aplikasi
absensi Final.zip`, `C:\AbsensiSekolah-*` (10 build uji, semua data uji sintetis), dan 4 berkas absensi di Downloads. **Tidak dihapus:** `C:\c` -- isinya bukan absensi (kerangka profil Hermes + `binance_raw.json` 1,9 MB yang BERBEDA dari `C:\Users\edgar\binance_raw.json`); menunggu keputusan pemilik. **GitHub:** akun `gh` (patahpensil) login, tetapi repo ini tidak punya remote dan belum ada repo absensi di akun; tidak ada yang di-push -- menunggu keputusan pemilik (nama repo, privat/publik). Folder proyek lain di `D:\` (ICT Screening, Malomo, NAHKODA, Ayuma, dst.) **tidak disentuh**.
