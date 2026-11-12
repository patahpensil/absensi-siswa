using System.Globalization;
using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Backup;

// PRD §18: dua titik pemicu (pembukaan aplikasi, setelah batas akhir tap
// masuk — App.xaml.cs dan Penjadwal yang memutuskan KAPAN, kelas ini hanya
// tahu CARA), retensi 7 berkas lokal terakhir, dan enkripsi sebelum
// diunggah ke cloud (lihat IPenyimpanCloudBackup untuk status server-nya).
public sealed class LayananBackup
{
    private const int JumlahBerkasDisimpan = 7;
    private const string FormatWaktu = "yyyyMMdd-HHmmss";

    private readonly string _folderBackup;
    private readonly IPenyimpanCloudBackup _penyimpanCloud;
    private readonly JalurPerubahanData _jalurPerubahanData = new();
    // Master Bug & UI/UX Register A-39: subfolder "arsip-audit" di bawah
    // folderBackup yang sama -- sengaja diturunkan di sini (bukan
    // parameter konstruktor baru) supaya seluruh pemanggil yang sudah ada
    // (App.xaml.cs, test) tidak perlu diubah.
    private readonly LayananArsipAudit _layananArsipAudit;

    public LayananBackup(string folderBackup, IPenyimpanCloudBackup penyimpanCloud)
    {
        _folderBackup = folderBackup;
        _penyimpanCloud = penyimpanCloud;
        _layananArsipAudit = new LayananArsipAudit(Path.Combine(folderBackup, "arsip-audit"));
    }

    // Titik normal (PRD §18.1): snapshot lokal, lalu — bila lisensi sudah
    // aktif — versi terenkripsinya "diunggah" (lihat catatan stub di
    // IPenyimpanCloudBackup). Ketentuan baseline desktop (PRD §21.9
    // meninjaunya untuk server, D-02): berbeda dari notifikasi, titik
    // ini DIKEJAR walau aplikasi baru dibuka lama setelah jadwalnya —
    // pemanggil (Penjadwal) karena itu tidak membuang panggilan ini
    // walau sudah lewat toleransi, tidak seperti titik notifikasi lain.
    public async Task JalankanAsync(SqliteConnection sumber, DateTime waktu, string? kunciLisensiAktif)
    {
        var data = BuatSnapshotLokal(sumber, waktu, kunciLisensiAktif);

        if (!string.IsNullOrWhiteSpace(kunciLisensiAktif))
        {
            var terenkripsi = EnkripsiBackup.Enkripsi(data, kunciLisensiAktif);
            await _penyimpanCloud.UnggahAsync(kunciLisensiAktif, terenkripsi, waktu);
        }
    }

    public IReadOnlyList<BerkasBackup> DaftarBackupLokal()
    {
        if (!Directory.Exists(_folderBackup))
        {
            return Array.Empty<BerkasBackup>();
        }

        return Directory.GetFiles(_folderBackup, "*.db")
            .Select(path => new BerkasBackup(path, WaktuDariNamaBerkas(path)))
            .OrderByDescending(b => b.WaktuDibuat)
            .ToList();
    }

    // PRD §18.4 langkah 1 & 2: cadangan kondisi saat ini dibuat DULU, dan
    // aksi restore tercatat di audit — keduanya selagi koneksi ke database
    // yang AKAN diganti masih terbuka. Isi berkas sumber restore dibaca ke
    // memori DI SINI (bukan belakangan) supaya rotasi 7-berkas di atas
    // tidak pernah bisa menghapusnya duluan sebelum sungguh dipakai.
    //
    // PRD §24.3: pemeriksaan peran di lapisan layanan ini,
    // bukan hanya menyembunyikan tombol Pulihkan di PengaturanViewModel.
    public byte[] SiapkanRestore(SqliteConnection sumber, AkunTersimpan akun, DateTime waktu, BerkasBackup berkasSumberRestore)
    {
        if (akun.Peran == PeranAkun.WaliKelas)
        {
            throw new InvalidOperationException("Wali kelas tidak berwenang memulihkan backup (PRD §18.4).");
        }

        // Master Bug & UI/UX Register A-39: arsip riwayat audit dibuat DAN
        // diverifikasi DI SINI, sebelum apa pun yang destruktif. Kalau ini
        // melempar, database sumber belum disentuh sama sekali -- pemanggil
        // (PengaturanViewModel.Restore, dibungkus PenangananGalat.Coba)
        // tidak pernah lanjut ke TerapkanRestore.
        var pathArsip = _layananArsipAudit.BuatDanVerifikasi(sumber, waktu);

        // Backup lokal terenkripsi dibuka dengan kunci lisensi aplikasi ini;
        // gagal dibuka = restore batal sebelum apa pun ditimpa.
        var kunciLisensi = new Absensi.Data.Lisensi.RepositoriLisensi().BacaStatus(sumber)?.Kunci;
        var dataSumberRestore = EnkripsiBackup.BukaLokal(File.ReadAllBytes(berkasSumberRestore.PathBerkas), kunciLisensi);
        BuatSnapshotLokal(sumber, waktu, kunciLisensi);
        var pathCadangan = Path.Combine(_folderBackup, $"{waktu.ToString(FormatWaktu)}.db");

        _jalurPerubahanData.Jalankan(
            sumber,
            akun,
            waktu,
            aksi: "restore_backup",
            entitas: "sekolah",
            perubahan: _ => null,
            nilaiLama: Path.GetFileName(pathCadangan),
            nilaiBaru: berkasSumberRestore.NamaBerkas);

        // Ditemukan lewat tes gagal (LayananAuditTests, A-39 Tahap 2): baris
        // restore_backup di atas ditulis SETELAH arsip dibuat (sengaja,
        // lihat komentar di atas), jadi tidak pernah ikut ter-snapshot --
        // catatan "sebelum restore ini" sendiri hilang begitu `sumber`
        // ditimpa. Disalin manual ke arsip yang sudah ada di sini.
        _layananArsipAudit.TambahkanBarisTerakhir(pathArsip, sumber);

        return dataSumberRestore;
    }

    // Dipanggil setelah koneksi ke database lama ditutup (SQLite tidak bisa
    // ditimpa selagi ada koneksi aktif memegangnya) — sengaja statis dan
    // tidak menyentuh SqliteConnection apa pun. Pemanggil (App.xaml.cs)
    // membuka koneksi baru ke berkas ini setelahnya.
    public static void TerapkanRestore(byte[] dataSumberRestore, string pathDatabaseTujuan)
    {
        SqliteConnection.ClearAllPools();
        File.WriteAllBytes(pathDatabaseTujuan, dataSumberRestore);

        // WAL/SHM peninggalan database lama tidak relevan lagi untuk
        // berkas yang baru saja ditimpa — dibiarkan bisa membuat SQLite
        // membaca campuran data lama dan baru saat database dibuka lagi.
        File.Delete(pathDatabaseTujuan + "-wal");
        File.Delete(pathDatabaseTujuan + "-shm");
    }

    // Mengembalikan isi SQLite polos snapshot. Berkas lokal ditulis
    // terenkripsi bila kunci lisensi tersedia; tanpa lisensi aktif (sebelum
    // aktivasi) tetap polos seperti sebelumnya.
    private byte[] BuatSnapshotLokal(SqliteConnection sumber, DateTime waktu, string? kunciLisensi)
    {
        Directory.CreateDirectory(_folderBackup);
        var pathTujuan = Path.Combine(_folderBackup, $"{waktu.ToString(FormatWaktu)}.db");
        var pathSementara = pathTujuan + ".tmp";

        using (var tujuan = new SqliteConnection($"Data Source={pathSementara};Pooling=False"))
        {
            tujuan.Open();
            sumber.BackupDatabase(tujuan);
        }

        // Microsoft.Data.Sqlite mengumpulkan (pool) koneksi native walau
        // sudah Dispose() — tanpa ini, Windows masih mengunci berkas yang
        // baru saja ditulis sehingga baca/hapus berikutnya gagal.
        SqliteConnection.ClearAllPools();

        var data = File.ReadAllBytes(pathSementara);
        File.WriteAllBytes(pathTujuan, string.IsNullOrWhiteSpace(kunciLisensi) ? data : EnkripsiBackup.BungkusLokal(data, kunciLisensi));
        File.Delete(pathSementara);

        HapusBerkasSelainNTerbaru(JumlahBerkasDisimpan);
        return data;
    }

    private void HapusBerkasSelainNTerbaru(int jumlahDisimpan)
    {
        // Nama berkas yyyyMMdd-HHmmss.db terurut leksikografis = terurut
        // kronologis, jadi tidak perlu mem-parse tanggalnya lagi di sini.
        var berkasLama = Directory.GetFiles(_folderBackup, "*.db")
            .OrderByDescending(path => path)
            .Skip(jumlahDisimpan);

        foreach (var path in berkasLama)
        {
            File.Delete(path);
        }
    }

    private static DateTime WaktuDariNamaBerkas(string path) =>
        DateTime.ParseExact(Path.GetFileNameWithoutExtension(path), FormatWaktu, CultureInfo.InvariantCulture);
}
