using Absensi.Data.Akun;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Waktu;

// RENCANA-TUGAS.md Tugas 9 — bagian "jadwal harian" dari layar Pengaturan
// (PRD §7, PANDUAN-UI-UX.md §9: "jadwal harian" ada di dalam Pengaturan,
// bukan menu sendiri). Setiap perubahan lewat JalurPerubahanData
// (PRD §12).
public sealed class LayananJadwalHari
{
    private readonly RepositoriJadwalHari _repositoriJadwalHari = new();
    private readonly RepositoriTahunAjaran _repositoriTahunAjaran = new();
    private readonly RepositoriKuncimBulan _repositoriKuncimBulan = new();
    private readonly JalurPerubahanData _jalurPerubahanData = new();

    // PRD §7: "seluruh nilai dapat diubah per hari" — ketujuh baris
    // disimpan sekaligus, satu transaksi (dikonfirmasi pemilik proyek
    // 2026-09-18, pola sama seperti Registrasi Kartu Tugas 8 dan impor
    // Excel — PRD §16.3).
    private static readonly string[] DayNames = { "", "Senin", "Selasa", "Rabu", "Kamis", "Jumat", "Sabtu", "Minggu" };

    // PRD §7 P-15: a schedule change for the future has an effective date;
    // earlier versions stay, so attendance on earlier dates keeps the
    // schedule that applied then. Effective date must be after today and
    // inside the academic period. Saving again for the same date replaces
    // that (still future) version.
    public void ScheduleFrom(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int tahunAjaranId, DateOnly effectiveFrom, IReadOnlyList<JadwalHariBaris> days)
    {
        AuthorizationGuard.EnsureFullAuthority(akun, "mengubah jadwal harian");

        if (effectiveFrom <= DateOnly.FromDateTime(waktu))
        {
            throw new InvalidOperationException("Tanggal mulai berlaku harus setelah hari ini. Jadwal untuk hari ini dan sebelumnya tidak diubah (PRD §7).");
        }

        if (_repositoriTahunAjaran.CariRentang(connection, tahunAjaranId) is not { } range || effectiveFrom < range.TglMulai || effectiveFrom > range.TglSelesai)
        {
            throw new InvalidOperationException("Tanggal mulai berlaku harus berada di dalam periode akademik.");
        }

        if (days.Count != 7 || days.Select(day => day.Hari).Distinct().Count() != 7 || days.Any(day => day.Hari is < 1 or > 7))
        {
            throw new InvalidOperationException("Jadwal harus berisi tujuh hari, Senin sampai Minggu.");
        }

        foreach (var day in days.Where(day => day.HariSekolah))
        {
            ValidateTimes(day);
        }

        _jalurPerubahanData.JalankanDenganCatatan(
            connection,
            akun,
            waktu,
            aksi: "jadwal_berlaku_mulai",
            entitas: "jadwal_hari",
            perubahan: transaction =>
            {
                var before = _repositoriJadwalHari.LoadTimeline(connection, tahunAjaranId, transaction);
                foreach (var day in days.OrderBy(day => day.Hari))
                {
                    _repositoriJadwalHari.SaveChange(connection, transaction, tahunAjaranId, effectiveFrom, day);
                }

                var columns = days.OrderBy(day => day.Hari)
                    .Select(day => new RingkasanPerubahan.Kolom(DayNames[day.Hari], Describe(before.For(effectiveFrom, day.Hari)), Describe(day)))
                    .Prepend(new RingkasanPerubahan.Kolom("berlaku_mulai", null, effectiveFrom.ToString("yyyy-MM-dd")))
                    .ToArray();
                return RingkasanPerubahan.Bandingkan(tahunAjaranId, columns);
            });
    }

    private static void ValidateTimes(JadwalHariBaris day)
    {
        var name = DayNames[day.Hari];
        if (day.JamMasuk is not { } arrival || day.JamRekap is not { } recap || day.BatasTapMasuk is not { } deadline
            || day.MulaiPulang is not { } departure || day.NotTappedListTime is not { } notTapped)
        {
            throw new InvalidOperationException($"{name}: kelima jam wajib diisi untuk hari sekolah.");
        }

        if (!(arrival < recap && recap < deadline && deadline <= departure))
        {
            throw new InvalidOperationException($"{name}: urutan jam harus batas hadir < rekap orang tua < batas akhir tap masuk ≤ mulai pulang.");
        }

        if (!(arrival < notTapped && notTapped <= deadline))
        {
            throw new InvalidOperationException($"{name}: daftar belum tap harus setelah batas hadir dan paling lambat batas akhir tap masuk.");
        }
    }

    private static string? Describe(JadwalHariBaris? day) => day is null
        ? null
        : !day.HariSekolah
            ? "libur"
            : $"hadir {Time(day.JamMasuk)}, belum tap {Time(day.NotTappedListTime)}, rekap {Time(day.JamRekap)}, batas {Time(day.BatasTapMasuk)}, pulang {Time(day.MulaiPulang)}";

    private static string Time(TimeOnly? time) => time?.ToString("HH:mm") ?? "-";

    // Satu pintu simpan untuk layar Pengaturan (desktop). Periode yang belum
    // punya jadwal sama sekali mendapat jadwal dasar (pengisian awal, belum
    // ada hasil tap yang bergantung padanya). Bila jadwal sudah ada, perubahan
    // hanya berlaku mulai tanggal yang dipilih (P-15) dan tidak menimpa
    // jadwal untuk tanggal yang sudah lewat. Mengembalikan true bila disimpan
    // sebagai versi berlaku mulai.
    public bool Save(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int tahunAjaranId, DateOnly effectiveFrom, IReadOnlyList<JadwalHariBaris> days)
    {
        if (!_repositoriJadwalHari.HasStoredSchedule(connection, tahunAjaranId))
        {
            Perbarui(connection, akun, waktu, tahunAjaranId, days);
            return false;
        }

        ScheduleFrom(connection, akun, waktu, tahunAjaranId, effectiveFrom, days);
        return true;
    }

    // Menulis jadwal dasar (jadwal_hari) dan berdampak ke seluruh periode.
    // Hanya untuk pengisian awal; perubahan jadwal yang sudah ada memakai
    // ScheduleFrom (P-15) — lihat Save.
    public void Perbarui(SqliteConnection connection, AkunTersimpan akun, DateTime waktu, int tahunAjaranId, IReadOnlyList<JadwalHariBaris> hari)
    {
        // PRD §3: pengaturan termasuk "kewenangan penuh" (Kepala
        // Sekolah/Admin) — wali kelas tidak disebutkan berwenang di sini.
        if (akun.Peran == PeranAkun.WaliKelas)
        {
            throw new InvalidOperationException("Wali kelas tidak berwenang mengubah jadwal harian (PRD §3).");
        }

        // PRD §10/§16.13: jadwal_hari berdampak retroaktif ke seluruh
        // bulan tahun ajaran ini (dikonfirmasi pemilik proyek 2026-09-18),
        // jadi ditolak kalau ADA bulan yang sudah terkunci di sepanjang
        // rentang tahun ajaran itu, bukan cuma bulan berjalan.
        if (_repositoriTahunAjaran.CariRentang(connection, tahunAjaranId) is { } rentang
            && _repositoriKuncimBulan.CariPertamaDalamRentang(connection, rentang.TglMulai, rentang.TglSelesai) is { } terkunci)
        {
            throw new InvalidOperationException(terkunci.PesanGalat);
        }

        _jalurPerubahanData.Jalankan(
            connection,
            akun,
            waktu,
            aksi: "perbarui_jadwal_hari",
            entitas: "jadwal_hari",
            perubahan: transaction =>
            {
                foreach (var baris in hari)
                {
                    _repositoriJadwalHari.Perbarui(connection, transaction, tahunAjaranId, baris);
                }

                return tahunAjaranId;
            });
    }
}
