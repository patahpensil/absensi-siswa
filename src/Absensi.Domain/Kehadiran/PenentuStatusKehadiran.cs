namespace Absensi.Domain.Kehadiran;

// Satu-satunya kelas penentuan status kehadiran (CLAUDE.md §4.1 aturan 1).
// Urutan pemeriksaan mengikuti docs/PRD.md §16.8, §16.8.a, §16.8.b persis —
// jangan disusun ulang "agar lebih efisien" (CLAUDE.md §4.3 aturan 12).
// Kelas ini tidak mengenal WPF maupun SQLite dan tidak menulis apa pun;
// pemanggil yang menyimpan keputusan ke database.
public sealed class PenentuStatusKehadiran
{
    public KeputusanTap Tentukan(KonteksTap konteks)
    {
        // PRD 16.8 langkah 2
        if (konteks.ModeRegistrasiAktif)
        {
            return KeputusanTap.Registrasi();
        }

        // PRD 16.8 langkah 3
        if (konteks.JenisHariIni == JenisHariKalender.Libur)
        {
            return KeputusanTap.Ditolak(AlasanTap.TanggalLibur);
        }

        // PRD 16.8 langkah 4-5
        return konteks.PemilikTerdaftar switch
        {
            null => KeputusanTap.Ditolak(AlasanTap.KartuTidakTerdaftar),
            JenisPemilikKartu.Siswa => TentukanUntukSiswa(konteks.Waktu, konteks.Siswa!),
            JenisPemilikKartu.Guru => TentukanUntukGuru(konteks.Waktu, konteks.Guru!),
            _ => throw new ArgumentOutOfRangeException(nameof(konteks)),
        };
    }

    // Catatan PRD 16.8.b: status guru menjadi HADIR hanya setelah tap
    // pulang. Bila sampai akhir hari hanya ada tap masuk, statusnya
    // TIDAK_LENGKAP dan menunggu keputusan Kepala Sekolah.
    public StatusAbsensiGuru TentukanStatusAkhirHariGuru(StatusAbsensiGuru statusSaatIni) =>
        statusSaatIni == StatusAbsensiGuru.Masuk
            ? StatusAbsensiGuru.TidakLengkap
            : statusSaatIni;

    // PRD §16.7 (14.10/Jumat 12.30, "satu jam setelah jam pulang") dan §8:
    // siswa yang tercatat masuk (Hadir atau Terlambat — keduanya berarti
    // "punya catatan masuk") tapi belum tap pulang ditetapkan BOLOS.
    public StatusAbsensiSiswa TentukanStatusAkhirHariSiswa(StatusAbsensiSiswa statusSaatIni, bool sudahTapPulang) =>
        statusSaatIni is StatusAbsensiSiswa.Hadir or StatusAbsensiSiswa.Terlambat && !sudahTapPulang
            ? StatusAbsensiSiswa.Bolos
            : statusSaatIni;

    // PRD 16.8.a — Kartu Siswa
    private static KeputusanTap TentukanUntukSiswa(DateTime waktu, KonteksTapSiswa siswa)
    {
        if (!siswa.SiswaAktif)
        {
            return KeputusanTap.Ditolak(AlasanTap.PemilikTidakAktif);
        }

        if (siswa.TapTerakhirKartuIni is { } tapTerakhir && waktu - tapTerakhir < siswa.JedaTapMinimal)
        {
            return KeputusanTap.Ditolak(AlasanTap.JedaTapTerlaluCepat);
        }

        // PRD §8.3/§16.8.a: koreksi Izin/Sakit mendahului tap masuk maupun pulang.
        if (siswa.StatusSaatIni is StatusAbsensiSiswa.Izin or StatusAbsensiSiswa.Sakit)
        {
            return KeputusanTap.TidakBerubahSiswa(siswa.StatusSaatIni);
        }

        var jamSekarang = TimeOnly.FromDateTime(waktu);

        if (jamSekarang >= siswa.MulaiAbsenPulang)
        {
            return siswa.SudahTapMasukHariIni
                ? KeputusanTap.IsiJamPulangSiswa()
                : KeputusanTap.Ditolak(AlasanTap.BelumPunyaCatatanMasuk);
        }

        if (siswa.SudahTapMasukHariIni)
        {
            return KeputusanTap.Diabaikan();
        }

        if (jamSekarang <= siswa.JamMasuk)
        {
            return KeputusanTap.IsiJamMasukSiswa(StatusAbsensiSiswa.Hadir);
        }

        if (jamSekarang <= siswa.BatasAkhirTapMasuk)
        {
            return KeputusanTap.IsiJamMasukSiswa(StatusAbsensiSiswa.Terlambat);
        }

        return KeputusanTap.Ditolak(AlasanTap.LewatBatasTapMasuk);
    }

    // PRD 16.8.b — Kartu Guru
    private static KeputusanTap TentukanUntukGuru(DateTime waktu, KonteksTapGuru guru)
    {
        if (!guru.GuruAktif)
        {
            return KeputusanTap.Ditolak(AlasanTap.PemilikTidakAktif);
        }

        if (guru.TapTerakhirKartuIni is { } tapTerakhir && waktu - tapTerakhir < guru.JedaTapMinimal)
        {
            return KeputusanTap.Ditolak(AlasanTap.JedaTapTerlaluCepat);
        }

        if (guru.StatusDitetapkanDiMuka is { } statusDiMuka)
        {
            return KeputusanTap.TidakBerubahGuru(statusDiMuka);
        }

        var jamSekarang = TimeOnly.FromDateTime(waktu);

        if (jamSekarang >= guru.MulaiTapPulang)
        {
            return guru.SudahTapMasukHariIni
                ? KeputusanTap.IsiJamPulangGuru()
                : KeputusanTap.Ditolak(AlasanTap.BelumPunyaCatatanMasuk);
        }

        if (guru.SudahTapMasukHariIni)
        {
            return KeputusanTap.Diabaikan();
        }

        return KeputusanTap.IsiJamMasukGuru();
    }
}
