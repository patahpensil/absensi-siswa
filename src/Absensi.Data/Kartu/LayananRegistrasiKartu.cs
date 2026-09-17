using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Kartu;

// RENCANA-TUGAS.md Tugas 8 — Registrasi Kartu (PRD §16.5, §16.11, §17.9).
// Setiap perubahan lewat JalurPerubahanData (CLAUDE.md §4.1 aturan 4).
public sealed class LayananRegistrasiKartu
{
    private readonly RepositoriKartu _repositoriKartu = new();
    private readonly RepositoriSiswa _repositoriSiswa = new();
    private readonly RepositoriGuru _repositoriGuru = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    // PRD §17.9: keunikan UID diperiksa lintas kedua master, bukan hanya
    // master yang sedang dibuka. Null berarti UID bebas dipakai.
    public PemilikKartuAktif? CariPemilikAktif(SqliteConnection connection, string uid, int tahunAjaranId)
    {
        var kartu = _repositoriKartu.CariAktifBerdasarkanUid(connection, uid);
        if (kartu is null)
        {
            return null;
        }

        if (kartu.PemilikTipe == JenisPemilikKartu.Siswa)
        {
            var (nama, kelasNama) = _repositoriSiswa.CariNamaDanKelas(connection, kartu.PemilikId, tahunAjaranId);
            return new PemilikKartuAktif(JenisPemilikKartu.Siswa, kartu.PemilikId, nama, kelasNama);
        }

        return new PemilikKartuAktif(JenisPemilikKartu.Guru, kartu.PemilikId, _repositoriGuru.CariNama(connection, kartu.PemilikId), KelasNama: null);
    }

    // PRD §16.5 langkah 5: "Seluruh sesi tersimpan" — semua-atau-tidak
    // sama sekali dalam satu transaksi (CLAUDE.md §4.2 aturan 10, sama
    // seperti impor Excel; dikonfirmasi pemilik proyek 2026-09-18: kalau
    // aplikasi gagal di tengah, operator tidak boleh diminta mengulang
    // kartu yang sudah sempat ditap). Pemanggil (ViewModel) wajib sudah
    // memvalidasi tiap UID lewat CariPemilikAktif sebelum sampai sini.
    public void DaftarkanSesi(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, IReadOnlyList<PendaftaranKartu> pendaftaran)
    {
        if (pendaftaran.Count == 0)
        {
            return;
        }

        PastikanBolehGuru(akun, pendaftaran.Select(p => p.PemilikTipe));

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "daftar_kartu",
            entitas: "kartu",
            perubahan: transaction =>
            {
                foreach (var p in pendaftaran)
                {
                    _repositoriKartu.Daftarkan(connection, transaction, p.Uid, p.PemilikTipe, p.PemilikId, waktu);
                }

                return null;
            },
            nilaiBaru: string.Join(", ", pendaftaran.Select(p => $"{p.Uid}->{p.NamaTampil}")));
    }

    // PRD §16.11: kartu lama dinonaktifkan (bukan dihapus — CLAUDE.md §4.2
    // aturan 8), kartu baru dikaitkan ke pemilik yang sama, satu transaksi.
    // Riwayat absensi tetap utuh karena menunjuk ke pemilik, bukan ke kartu
    // (aturan 7).
    public void GantiKartuHilang(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        JenisPemilikKartu pemilikTipe,
        int pemilikId,
        string uidBaru)
    {
        PastikanBolehGuru(akun, new[] { pemilikTipe });

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "ganti_kartu",
            entitas: "kartu",
            perubahan: transaction =>
            {
                _repositoriKartu.Nonaktifkan(connection, transaction, pemilikTipe, pemilikId, waktu, alasan: "hilang");
                _repositoriKartu.Daftarkan(connection, transaction, uidBaru, pemilikTipe, pemilikId, waktu);
                return pemilikId;
            },
            nilaiBaru: uidBaru);
    }

    // PRD §17.10: registrasi/penggantian kartu guru hanya TU/Admin/Kepala
    // Sekolah — wali kelas tidak berwenang. Kartu siswa tidak dibatasi
    // perannya di PRD, jadi tidak diperiksa di sini.
    private static void PastikanBolehGuru(AkunTersimpan akun, IEnumerable<JenisPemilikKartu> pemilikTipe)
    {
        if (akun.Peran == PeranAkun.WaliKelas && pemilikTipe.Contains(JenisPemilikKartu.Guru))
        {
            throw new InvalidOperationException("Wali kelas tidak berwenang mendaftarkan atau mengganti kartu guru (PRD §17.10).");
        }
    }
}
