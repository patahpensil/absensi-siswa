using Absensi.Data.Akun;
using Absensi.Data.Koreksi;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Waktu;

// RENCANA-TUGAS.md Tugas 9 — layar Kalender (PRD §6). Setiap perubahan
// lewat JalurPerubahanData (PRD §12).
public sealed class LayananKalender
{
    private readonly RepositoriKalender _repositoriKalender = new();
    private readonly RepositoriOutbox _repositoriOutbox = new();
    private readonly RepositoriKuncimBulan _repositoriKuncimBulan = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    // Menandai satu tanggal atau satu rentang tanggal sekaligus, satu
    // transaksi (Tugas 9, "Perintah": "input manual per tanggal atau per
    // rentang"). Keterangan wajib diisi untuk jenis Libur — PRD §6 tidak
    // menjabarkan alur "libur mendadak" terpisah dari libur terjadwal
    // (mis. libur semester), jadi keduanya lewat jalur yang sama dan
    // sama-sama wajib mengisi keterangan (dikonfirmasi pemilik proyek
    // 2026-09-18). Begitu suatu tanggal ditandai Libur, seluruh baris
    // outbox tanggal itu yang masih 'antre' dibatalkan dalam transaksi
    // yang sama — kriteria selesai Tugas 9.
    public void TandaiRentang(
        SqliteConnection connection,
        AkunTersimpan akun,
        DateTime waktu,
        DateOnly tanggalAwal,
        DateOnly tanggalAkhir,
        JenisHariKalender jenis,
        string? keterangan)
    {
        // PRD §3: kalender pendidikan termasuk "kewenangan penuh" (Kepala
        // Sekolah/Admin) — wali kelas tidak disebutkan berwenang di sini.
        if (akun.Peran == PeranAkun.WaliKelas)
        {
            throw new InvalidOperationException("Wali kelas tidak berwenang mengubah kalender pendidikan (PRD §3).");
        }

        if (tanggalAkhir < tanggalAwal)
        {
            throw new ArgumentException("Tanggal akhir tidak boleh sebelum tanggal awal.", nameof(tanggalAkhir));
        }

        if (jenis == JenisHariKalender.Libur && string.IsNullOrWhiteSpace(keterangan))
        {
            throw new ArgumentException("Keterangan wajib diisi saat menandai tanggal sebagai libur (PRD §6).", nameof(keterangan));
        }

        var jumlahHari = tanggalAkhir.DayNumber - tanggalAwal.DayNumber + 1;

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: jenis == JenisHariKalender.Libur ? "tandai_libur" : "tandai_hari_sekolah",
            entitas: "kalender",
            perubahan: transaction =>
            {
                if (_repositoriKuncimBulan.CariPertamaDalamRentang(connection, tanggalAwal, tanggalAkhir, transaction) is { } locked)
                    throw new InvalidOperationException(locked.PesanGalat);

                var years = new RepositoriTahunAjaran();
                var before = new List<string>(jumlahHari);
                for (var i = 0; i < jumlahHari; i++)
                {
                    var tanggal = tanggalAwal.AddDays(i);
                    var period = years.FindForDate(connection, tanggal, transaction);
                    var oldKind = period is { } id ? _repositoriKalender.TentukanJenisHari(connection, tanggal, id, transaction).ToString() : "Tanpa periode";
                    before.Add($"{tanggal:yyyy-MM-dd} {oldKind}");
                    _repositoriKalender.Tandai(connection, transaction, tanggal, jenis, keterangan, akun.Id, waktu);
                    LocalAttendanceClosure.InvalidateDay(connection, transaction, tanggal);

                    if (jenis == JenisHariKalender.Libur)
                    {
                        _repositoriOutbox.BatalkanAntreUntukTanggal(connection, transaction, tanggal);
                    }
                }

                return new CatatanPerubahan(null, string.Join(", ", before),
                    $"{tanggalAwal:yyyy-MM-dd}..{tanggalAkhir:yyyy-MM-dd} · {jenis} · {keterangan}");
            });
    }
}
