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
}
