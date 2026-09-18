using Absensi.Data.Repositori;
using Absensi.Domain.Akun;
using Absensi.Domain.Kehadiran;
using Absensi.Domain.Laporan;
using Microsoft.Data.Sqlite;

namespace Absensi.Data.Laporan;

// RENCANA-TUGAS.md Tugas 12 (PRD §11, §17.8). Hanya membaca — laporan
// tidak mengubah data, jadi tidak lewat JalurPerubahanData (CLAUDE.md
// §4.1 aturan 4 hanya berlaku untuk PERUBAHAN data). Mengembalikan DTO
// murni Absensi.Domain.Laporan; Absensi.Laporan yang mencetaknya ke
// Excel/PDF, App/Laporan yang menampilkan pratinjaunya di layar.
//
// PRD §3: "laporan" ada di daftar "Kewenangan penuh mencakup" (Kepala
// Sekolah/Admin) — berbeda dari Koreksi/Kalender, wali kelas TIDAK
// disebutkan berwenang sama sekali di sini (bukan "kelasnya sendiri
// saja"), jadi ditolak total, bukan difilter per kelas (CLAUDE.md §4.5
// aturan 19: pemeriksaan peran di lapisan layanan).
public sealed class LayananLaporan
{
    private readonly RepositoriAbsensi _repositoriAbsensi = new();
    private readonly RepositoriKalender _repositoriKalender = new();
    private readonly RepositoriTahunAjaran _repositoriTahunAjaran = new();
    private readonly RepositoriSiswa _repositoriSiswa = new();
    private readonly RepositoriGuru _repositoriGuru = new();

    // Harian (§11, satu tanggal) dan Mingguan (§11.1, Senin-Sabtu) berbagi
    // bentuk baris yang sama — pemanggil (App/Laporan) yang menentukan
    // rentangnya sesuai jenis laporan yang dipilih operator.
    public IReadOnlyList<BarisKejadian> Kejadian(
        SqliteConnection connection, AkunTersimpan akun, DateOnly tanggalAwal, DateOnly tanggalAkhir, int? kelasId, JenisSubjekLaporan subjek)
    {
        PastikanBerwenang(akun);

        if (subjek == JenisSubjekLaporan.Guru)
        {
            return _repositoriAbsensi.DaftarGuruUntukRentang(connection, tanggalAwal, tanggalAkhir)
                .Select(b => new BarisKejadian(
                    b.Tanggal, b.Nama, b.Nip, null,
                    KodeStatus.Huruf(b.Status), b.JamMasuk, b.JamPulang,
                    KodeStatus.Keterangan(b.Status, b.JamMasuk, b.Keterangan)))
                .ToList();
        }

        return _repositoriAbsensi.DaftarSiswaUntukRentang(connection, tanggalAwal, tanggalAkhir, kelasId)
            .Select(b => new BarisKejadian(
                b.Tanggal, b.Nama, b.Nisn, b.KelasNama,
                KodeStatus.Huruf(b.Status), b.JamMasuk, b.JamPulang,
                KodeStatus.Keterangan(b.Status, b.JamMasuk, b.Keterangan)))
            .ToList();
    }

    // Bulanan (§11.2): grid satu baris per orang, kolom tanggal terpisah
    // (KolomTanggalBulanan) supaya penulis Excel/PDF tahu kolom mana yang
    // diarsir tanpa mengulang logika kalender per orang.
    public LaporanBulanan Bulanan(
        SqliteConnection connection, AkunTersimpan akun, int tahunAjaranId, int tahun, int bulan, int? kelasId, JenisSubjekLaporan subjek)
    {
        PastikanBerwenang(akun);

        var tanggalAwal = new DateOnly(tahun, bulan, 1);
        var tanggalAkhir = tanggalAwal.AddMonths(1).AddDays(-1);
        var jenisHari = _repositoriKalender.DaftarJenisHariUntukRentang(connection, tahunAjaranId, tanggalAwal, tanggalAkhir);
        var kolom = jenisHari
            .OrderBy(kv => kv.Key)
            .Select(kv => new KolomTanggalBulanan(kv.Key, kv.Value.Jenis == JenisHariKalender.Libur, kv.Value.Alasan))
            .ToList();
        var hariEfektif = jenisHari.Values.Count(j => j.Jenis == JenisHariKalender.Sekolah);

        if (subjek == JenisSubjekLaporan.Guru)
        {
            var baris = _repositoriAbsensi.DaftarGuruUntukRentang(connection, tanggalAwal, tanggalAkhir)
                .GroupBy(b => (b.GuruId, b.Nama, b.Nip))
                .OrderBy(g => g.Key.Nama)
                .Select(g => new BarisBulanan(
                    g.Key.Nama, g.Key.Nip, null,
                    g.ToDictionary(b => b.Tanggal, b => KodeStatus.Huruf(b.Status)),
                    HitungRekapGuru(g.Select(b => b.Status).ToList(), hariEfektif)))
                .ToList();
            return new LaporanBulanan(kolom, baris);
        }

        var barisSiswa = _repositoriAbsensi.DaftarSiswaUntukRentang(connection, tanggalAwal, tanggalAkhir, kelasId)
            .GroupBy(b => (b.SiswaId, b.Nama, b.Nisn, b.KelasNama))
            .OrderBy(g => g.Key.KelasNama).ThenBy(g => g.Key.Nama)
            .Select(g => new BarisBulanan(
                g.Key.Nama, g.Key.Nisn, g.Key.KelasNama,
                g.ToDictionary(b => b.Tanggal, b => KodeStatus.Huruf(b.Status)),
                HitungRekapSiswa(g.Select(b => b.Status).ToList(), hariEfektif)))
            .ToList();
        return new LaporanBulanan(kolom, barisSiswa);
    }

    // Per Orang (§11.3/§17.8): satu kartu, identitas diselesaikan lewat
    // Repositori*.CariIdentitasUntukLaporan (bukan dari baris absensi)
    // supaya orang yang tidak punya baris sama sekali di rentang ini
    // (baru pindah masuk, atau rentang sebelum dia terdaftar) tetap
    // menghasilkan kartu yang benar alih-alih melempar galat.
    public KartuOrang PerOrang(
        SqliteConnection connection, AkunTersimpan akun, int id, JenisSubjekLaporan subjek, int tahunAjaranId, DateOnly tanggalAwal, DateOnly tanggalAkhir)
    {
        PastikanBerwenang(akun);

        var hariEfektif = HitungHariEfektif(connection, tahunAjaranId, tanggalAwal, tanggalAkhir);

        if (subjek == JenisSubjekLaporan.Guru)
        {
            var identitas = _repositoriGuru.CariIdentitasUntukLaporan(connection, id)
                ?? throw new InvalidOperationException($"Guru {id} tidak ditemukan.");
            var baris = _repositoriAbsensi.DaftarGuruUntukRentang(connection, tanggalAwal, tanggalAkhir)
                .Where(b => b.GuruId == id)
                .ToList();
            return BangunKartuGuru(connection, id, identitas.Nama, identitas.Nip, baris, hariEfektif);
        }

        var identitasSiswa = _repositoriSiswa.CariIdentitasUntukLaporan(connection, id, tahunAjaranId)
            ?? throw new InvalidOperationException($"Siswa {id} tidak ditemukan.");
        var barisSiswa = _repositoriAbsensi.DaftarSiswaUntukRentang(connection, tanggalAwal, tanggalAkhir, kelasId: null)
            .Where(b => b.SiswaId == id)
            .ToList();
        return BangunKartuSiswa(connection, id, identitasSiswa.Nama, identitasSiswa.Nisn, identitasSiswa.KelasNama, barisSiswa, hariEfektif);
    }

    // Cetak sekaligus satu kelas (siswa, §11.3) atau seluruh guru (§17.8) —
    // kelasId diabaikan untuk guru, guru tidak dikelompokkan per kelas.
    public IReadOnlyList<KartuOrang> PerKelasAtauSemua(
        SqliteConnection connection, AkunTersimpan akun, JenisSubjekLaporan subjek, int? kelasId, int tahunAjaranId, DateOnly tanggalAwal, DateOnly tanggalAkhir)
    {
        PastikanBerwenang(akun);

        var hariEfektif = HitungHariEfektif(connection, tahunAjaranId, tanggalAwal, tanggalAkhir);

        if (subjek == JenisSubjekLaporan.Guru)
        {
            return _repositoriAbsensi.DaftarGuruUntukRentang(connection, tanggalAwal, tanggalAkhir)
                .GroupBy(b => (b.GuruId, b.Nama, b.Nip))
                .OrderBy(g => g.Key.Nama)
                .Select(g => BangunKartuGuru(connection, g.Key.GuruId, g.Key.Nama, g.Key.Nip, g.ToList(), hariEfektif))
                .ToList();
        }

        return _repositoriAbsensi.DaftarSiswaUntukRentang(connection, tanggalAwal, tanggalAkhir, kelasId)
            .GroupBy(b => (b.SiswaId, b.Nama, b.Nisn, b.KelasNama))
            .OrderBy(g => g.Key.KelasNama).ThenBy(g => g.Key.Nama)
            .Select(g => BangunKartuSiswa(connection, g.Key.SiswaId, g.Key.Nama, g.Key.Nisn, g.Key.KelasNama, g.ToList(), hariEfektif))
            .ToList();
    }

    // Semester (§11.4): rentang mengikuti batas tahun_ajaran aktif itu
    // sendiri, bukan tanggal yang dipilih operator — SKEMA-DATABASE.md
    // menyimpan satu semester sebagai satu baris tahun_ajaran
    // (tgl_mulai/tgl_selesai sudah dibatasi ke satu semester, bukan
    // setahun penuh), jadi tidak perlu konsep batas semester terpisah.
    public IReadOnlyList<BarisSemester> Semester(SqliteConnection connection, AkunTersimpan akun, int tahunAjaranId, int? kelasId, JenisSubjekLaporan subjek)
    {
        PastikanBerwenang(akun);

        var rentang = _repositoriTahunAjaran.CariRentang(connection, tahunAjaranId)
            ?? throw new InvalidOperationException($"Tahun ajaran {tahunAjaranId} tidak ditemukan.");
        var hariEfektif = HitungHariEfektif(connection, tahunAjaranId, rentang.TglMulai, rentang.TglSelesai);

        if (subjek == JenisSubjekLaporan.Guru)
        {
            return _repositoriAbsensi.DaftarGuruUntukRentang(connection, rentang.TglMulai, rentang.TglSelesai)
                .GroupBy(b => (b.GuruId, b.Nama, b.Nip))
                .OrderBy(g => g.Key.Nama)
                .Select(g => new BarisSemester(g.Key.Nama, g.Key.Nip, null, HitungRekapGuru(g.Select(b => b.Status).ToList(), hariEfektif)))
                .ToList();
        }

        return _repositoriAbsensi.DaftarSiswaUntukRentang(connection, rentang.TglMulai, rentang.TglSelesai, kelasId)
            .GroupBy(b => (b.SiswaId, b.Nama, b.Nisn, b.KelasNama))
            .OrderBy(g => g.Key.KelasNama).ThenBy(g => g.Key.Nama)
            .Select(g => new BarisSemester(g.Key.Nama, g.Key.Nisn, g.Key.KelasNama, HitungRekapSiswa(g.Select(b => b.Status).ToList(), hariEfektif)))
            .ToList();
    }

    private static void PastikanBerwenang(AkunTersimpan akun)
    {
        if (akun.Peran == PeranAkun.WaliKelas)
        {
            throw new InvalidOperationException("Wali kelas tidak berwenang mengakses laporan (PRD §3).");
        }
    }

    private int HitungHariEfektif(SqliteConnection connection, int tahunAjaranId, DateOnly tanggalAwal, DateOnly tanggalAkhir) =>
        _repositoriKalender.DaftarJenisHariUntukRentang(connection, tahunAjaranId, tanggalAwal, tanggalAkhir)
            .Values.Count(j => j.Jenis == JenisHariKalender.Sekolah);

    private KartuOrang BangunKartuSiswa(
        SqliteConnection connection, int siswaId, string nama, string nisn, string? kelas,
        IReadOnlyList<BarisAbsensiSiswaMentah> baris, int hariEfektif)
    {
        var rekap = HitungRekapSiswa(baris.Select(b => b.Status).ToList(), hariEfektif);
        var rincian = baris
            .Where(b => b.Status != StatusAbsensiSiswa.Hadir)
            .Select(b => new RincianKejadian(b.Tanggal, KodeStatus.Huruf(b.Status), KodeStatus.Keterangan(b.Status, b.JamMasuk, b.Keterangan)))
            .ToList();
        return new KartuOrang(nama, nisn, kelas, _repositoriSiswa.CariFotoPath(connection, siswaId), rekap, rincian);
    }

    private KartuOrang BangunKartuGuru(
        SqliteConnection connection, int guruId, string nama, string nip,
        IReadOnlyList<BarisAbsensiGuruMentah> baris, int hariEfektif)
    {
        var rekap = HitungRekapGuru(baris.Select(b => b.Status).ToList(), hariEfektif);
        var rincian = baris
            .Where(b => b.Status is not (StatusAbsensiGuru.Hadir or StatusAbsensiGuru.Masuk))
            .Select(b => new RincianKejadian(b.Tanggal, KodeStatus.Huruf(b.Status), KodeStatus.Keterangan(b.Status, b.JamMasuk, b.Keterangan)))
            .ToList();
        return new KartuOrang(nama, nip, null, _repositoriGuru.CariFotoPath(connection, guruId), rekap, rincian);
    }

    // PRD §11.5: Terlambat dan Bolos tetap dihitung hadir untuk
    // persentase, tapi masing-masing tetap punya kolom rekap sendiri
    // ("jumlah terlambat", "jumlah bolos") — beda dari KodeStatus.Huruf
    // yang memetakan keduanya ke huruf berbeda ("B") untuk sel grid.
    private static RekapKehadiran HitungRekapSiswa(IReadOnlyList<StatusAbsensiSiswa> status, int hariEfektif)
    {
        var dihitungHadir = status.Count(KodeStatus.DihitungHadir);
        var persentase = hariEfektif == 0 ? 0 : Math.Round(dihitungHadir * 100.0 / hariEfektif, 1);
        return new RekapKehadiran(
            Hadir: status.Count(s => s == StatusAbsensiSiswa.Hadir),
            Sakit: status.Count(s => s == StatusAbsensiSiswa.Sakit),
            Izin: status.Count(s => s == StatusAbsensiSiswa.Izin),
            Alpa: status.Count(s => s == StatusAbsensiSiswa.Alpa),
            Terlambat: status.Count(s => s == StatusAbsensiSiswa.Terlambat),
            Bolos: status.Count(s => s == StatusAbsensiSiswa.Bolos),
            DinasLuar: 0,
            Cuti: 0,
            TidakLengkap: 0,
            HariEfektif: hariEfektif,
            Persentase: persentase);
    }

    // TidakLengkap (guru tap masuk tanpa tap pulang, PRD §17.3 — butuh
    // keputusan Kepala Sekolah) PUNYA kolom rekap sendiri, tidak dilipat
    // ke Hadir (dikonfirmasi pemilik proyek 2026-09-18) — hanya Masuk
    // (transien, hari masih berjalan) yang tetap masuk hitungan Hadir.
    // Tetap ikut pembilang persentase (KodeStatus.DihitungHadir) — dua
    // hal berbeda dari kolom rekapnya sendiri.
    private static RekapKehadiran HitungRekapGuru(IReadOnlyList<StatusAbsensiGuru> status, int hariEfektif)
    {
        var dihitungHadir = status.Count(KodeStatus.DihitungHadir);
        var persentase = hariEfektif == 0 ? 0 : Math.Round(dihitungHadir * 100.0 / hariEfektif, 1);
        return new RekapKehadiran(
            Hadir: status.Count(s => s is StatusAbsensiGuru.Hadir or StatusAbsensiGuru.Masuk),
            Sakit: status.Count(s => s == StatusAbsensiGuru.Sakit),
            Izin: status.Count(s => s == StatusAbsensiGuru.Izin),
            Alpa: status.Count(s => s == StatusAbsensiGuru.Alpa),
            Terlambat: 0,
            Bolos: 0,
            DinasLuar: status.Count(s => s == StatusAbsensiGuru.DinasLuar),
            Cuti: status.Count(s => s == StatusAbsensiGuru.Cuti),
            TidakLengkap: status.Count(s => s == StatusAbsensiGuru.TidakLengkap),
            HariEfektif: hariEfektif,
            Persentase: persentase);
    }
}
