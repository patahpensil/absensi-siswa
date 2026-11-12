using Absensi.Data.Akun;
using Absensi.Data.Lisensi;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Wizard;

// PRD §16.1: wizard instalasi pertama. Seluruh langkah ditulis dalam SATU
// transaksi saat langkah terakhir ("Selesai") ditekan — bukan per langkah —
// supaya wizard yang dibatalkan di tengah jalan tidak meninggalkan sekolah
// tanpa tahun ajaran/jadwal/akun (App.xaml.cs mendeteksi wizard sudah
// selesai lewat ADA-tidaknya baris `sekolah`, jadi baris itu baru boleh
// muncul kalau semuanya berhasil).
//
// Pengecualian tunggal terhadap PRD §12 ("seluruh
// perubahan data lewat JalurPerubahanData") ada di sini: akun Kepala
// Sekolah yang dibuat wizard adalah akun PERTAMA — belum ada akun lain
// untuk berperan sebagai aktor JalurPerubahanData. Baris audit tetap
// ditulis, memakai akun yang baru dibuat itu sendiri sebagai aktornya
// (satu-satunya kejadian ini boleh terjadi, karena sekolah tanpa akun tidak
// mungkin lolos wizard dua kali).
public sealed class LayananWizard
{
    private readonly RepositoriSekolah _repositoriSekolah = new();
    private readonly RepositoriTahunAjaran _repositoriTahunAjaran = new();
    private readonly RepositoriJadwalHari _repositoriJadwalHari = new();
    private readonly RepositoriLisensi _repositoriLisensi = new();
    private readonly RepositoriAkun _repositoriAkun = new();
    private readonly RepositoriAudit _repositoriAudit = new();

    public AkunTersimpan Selesaikan(SqliteConnection connection, DateTime waktu, DataWizardInstalasi data)
    {
        Validasi(data);

        using var transaction = connection.BeginTransaction();

        _repositoriSekolah.Simpan(connection, transaction, data.NamaSekolah, data.Npsn, data.Alamat, data.LogoPath);

        var tahunAjaranId = _repositoriTahunAjaran.Tambah(
            connection, transaction, data.NamaTahunAjaran, data.Semester, data.TglMulaiTahunAjaran, data.TglSelesaiTahunAjaran);

        foreach (var baris in data.JadwalHarian)
        {
            _repositoriJadwalHari.Perbarui(connection, transaction, tahunAjaranId, baris);
        }

        _repositoriLisensi.Tulis(connection, transaction, data.KunciLisensi, waktu);

        var sandiHash = HashSandi.Buat(data.SandiKepalaSekolah);
        var akunId = _repositoriAkun.Tambah(
            connection, transaction, data.UsernameKepalaSekolah, sandiHash, PeranAkun.KepalaSekolah, guruId: null, dibuatOleh: null, waktu);

        _repositoriAudit.Catat(
            connection, transaction, waktu, akunId,
            aksi: "wizard_selesai",
            entitas: "akun",
            entitasId: akunId,
            nilaiLama: null,
            nilaiBaru: data.UsernameKepalaSekolah,
            atasPermintaan: null);

        transaction.Commit();

        return _repositoriAkun.CariBerdasarkanId(connection, akunId)!;
    }

    // Aturan isian wizard diperiksa di layanan (PRD §24.3), sehingga wizard
    // desktop dan web uji menolak hal yang sama. Daftar belum tap (P-06)
    // boleh kosong pada instalasi pertama.
    private static void Validasi(DataWizardInstalasi data)
    {
        if (string.IsNullOrWhiteSpace(data.KunciLisensi))
            throw new InvalidOperationException("Kunci lisensi wajib diisi.");
        if (string.IsNullOrWhiteSpace(data.NamaSekolah) || data.NamaSekolah.Trim().Length > 120)
            throw new InvalidOperationException("Nama sekolah wajib diisi, paling banyak 120 karakter.");
        if (!string.IsNullOrWhiteSpace(data.Npsn) && !System.Text.RegularExpressions.Regex.IsMatch(data.Npsn.Trim(), "^[0-9]{8}$"))
            throw new InvalidOperationException("NPSN terdiri dari 8 angka.");
        if (string.IsNullOrWhiteSpace(data.NamaTahunAjaran) || data.Semester is not ("ganjil" or "genap"))
            throw new InvalidOperationException("Nama tahun ajaran dan semester (ganjil/genap) wajib diisi.");
        if (data.TglSelesaiTahunAjaran <= data.TglMulaiTahunAjaran)
            throw new InvalidOperationException("Tanggal selesai tahun ajaran harus setelah tanggal mulai.");
        if (data.JadwalHarian.Count != 7 || data.JadwalHarian.Select(h => h.Hari).Distinct().Count() != 7)
            throw new InvalidOperationException("Jadwal harus berisi tujuh hari, Senin sampai Minggu.");

        string[] namaHari = ["", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu", "Minggu"];
        foreach (var hari in data.JadwalHarian.Where(h => h.HariSekolah))
        {
            var nama = hari.Hari is >= 1 and <= 7 ? namaHari[hari.Hari] : $"Hari {hari.Hari}";
            if (hari.JamMasuk is not { } masuk || hari.JamRekap is not { } rekap || hari.BatasTapMasuk is not { } batas || hari.MulaiPulang is not { } pulang)
                throw new InvalidOperationException($"{nama}: jam masuk, rekap orang tua, batas akhir tap masuk dan mulai pulang wajib diisi untuk hari sekolah.");
            if (!(masuk < rekap && rekap < batas && batas <= pulang))
                throw new InvalidOperationException($"{nama}: urutan jam harus batas hadir < rekap orang tua < batas akhir tap masuk ≤ mulai pulang.");
        }

        LayananAkun.PastikanUsernameValid(data.UsernameKepalaSekolah);
        LayananAkun.PastikanSandiValid(data.SandiKepalaSekolah);
    }
}
