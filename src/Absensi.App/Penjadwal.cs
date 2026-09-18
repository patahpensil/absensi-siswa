using Absensi.Data;
using Absensi.Data.Notifikasi;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Absensi.Domain.Kehadiran;
using Absensi.Telegram;
using Microsoft.Data.Sqlite;

namespace Absensi.App;

// RENCANA-TUGAS.md Tugas 10 — menjalankan titik pengiriman batch PRD §9.2
// (rekap 08.00, batas akhir tap masuk, finalisasi Bolos) tepat pada jam
// yang dikonfigurasi jadwal_hari, plus memutar pengirim outbox dan
// penerima pairing secara berkala. PRD §9.4: toleransi jadwal (bawaan 15
// menit, dari pengaturan) — jadwal yang terlewat lebih dari itu dibuang,
// tidak dikejar; tanggal libur membatalkan seluruh jadwal (tidak dijalankan
// sama sekali, bukan hanya notifikasinya — lihat DetakAsync). RENCANA-TUGAS.md
// Tugas 11 menambah: pergantian tanggal terdeteksi di sini juga memicu
// pembukaan hari untuk tanggal baru (jaring pengaman bila aplikasi tetap
// menyala melewati 00.01 — App.xaml.cs hanya menjalankannya sekali saat
// start) dan finalisasi PRD §16.16 (guru tap masuk saja → Tidak Lengkap)
// untuk tanggal yang baru berakhir.
public sealed class Penjadwal
{
    private readonly SqliteConnection _connection;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly LayananNotifikasi _layananNotifikasi = new();
    private readonly RepositoriJadwalHari _repositoriJadwalHari = new();
    private readonly RepositoriTahunAjaran _repositoriTahunAjaran = new();
    private readonly RepositoriKalender _repositoriKalender = new();
    private readonly RepositoriPengaturan _repositoriPengaturan = new();
    private readonly RepositoriAbsensi _repositoriAbsensi = new();
    private readonly PembukaanHari _pembukaanHari = new();
    private readonly PenentuStatusKehadiran _penentuStatusKehadiran = new();
    private readonly PengirimOutbox _pengirimOutbox;
    private readonly PemrosesPembaruanPairing _pemrosesPembaruanPairing;

    private DateOnly? _tanggalTerakhirDiproses;
    private bool _rekapSudahDiproses;
    private bool _batasAkhirSudahDiproses;
    private bool _bolosSudahDiproses;

    public Penjadwal(SqliteConnection connection, IPenyediaWaktu penyediaWaktu, IKlienBotTelegram klienBotTelegram)
    {
        _connection = connection;
        _penyediaWaktu = penyediaWaktu;
        _pengirimOutbox = new PengirimOutbox(klienBotTelegram);
        _pemrosesPembaruanPairing = new PemrosesPembaruanPairing(klienBotTelegram);
    }

    // Dipanggil DispatcherTimer App.xaml.cs secara berkala (mis. tiap 30
    // detik) — cukup sering supaya jendela toleransi 15 menit selalu
    // tertangkap, cukup jarang supaya tidak membebani. Seluruh isi dijaga
    // try-catch di App.xaml.cs: satu detak yang gagal (mis. galat jaringan
    // sesaat) tidak boleh mematikan aplikasi absensi gerbang yang sedang
    // dipakai — dicoba lagi detak berikutnya.
    public async Task DetakAsync()
    {
        var sekarang = _penyediaWaktu.Sekarang;
        var tanggal = DateOnly.FromDateTime(sekarang);

        if (_tanggalTerakhirDiproses != tanggal)
        {
            if (_tanggalTerakhirDiproses is { } tanggalSebelumnya)
            {
                FinalisasiAkhirHariGuru(tanggalSebelumnya);
            }

            _tanggalTerakhirDiproses = tanggal;
            _rekapSudahDiproses = false;
            _batasAkhirSudahDiproses = false;
            _bolosSudahDiproses = false;

            JalankanPembukaanHariBilaSudahDikonfigurasi(tanggal);
        }

        JalankanBatchBilaSaatnya(tanggal, sekarang);

        await _pengirimOutbox.KirimAntreanAsync(_connection, _penyediaWaktu);
        await _pemrosesPembaruanPairing.ProsesPembaruanAsync(_connection, _penyediaWaktu);
    }

    // PRD §16.7: "00.01 hari berganti → aplikasi membuat baris absensi".
    // App.xaml.cs hanya menjalankan ini sekali saat aplikasi dibuka; ini
    // jaring pengaman bila aplikasi tetap menyala melewati tengah malam
    // (wajar untuk layar gerbang yang dipasang permanen).
    private void JalankanPembukaanHariBilaSudahDikonfigurasi(DateOnly tanggal)
    {
        if (_repositoriTahunAjaran.CariIdAktif(_connection) is null)
        {
            return;
        }

        _pembukaanHari.Jalankan(_connection, tanggal);
    }

    // PRD §16.16 (24.00, akhir hari): guru dengan tap masuk saja menjadi
    // Tidak Lengkap, menunggu keputusan Kepala Sekolah (PRD §16.17). Bukan
    // tindakan Kepala Sekolah — tidak lewat LayananStatusGuru/diaudit,
    // sama seperti PembukaanHari.
    private void FinalisasiAkhirHariGuru(DateOnly tanggal)
    {
        foreach (var guruId in _repositoriAbsensi.DaftarGuruMasukTanpaPulang(_connection, tanggal))
        {
            var statusBaru = _penentuStatusKehadiran.TentukanStatusAkhirHariGuru(StatusAbsensiGuru.Masuk);
            using var transaction = _connection.BeginTransaction();
            _repositoriAbsensi.FinalisasiAkhirHariGuru(_connection, transaction, tanggal, guruId, statusBaru);
            transaction.Commit();
        }
    }

    private void JalankanBatchBilaSaatnya(DateOnly tanggal, DateTime sekarang)
    {
        if (_repositoriTahunAjaran.CariIdAktif(_connection) is not { } tahunAjaranId)
        {
            return;
        }

        // PRD §9.4: "Tanggal diblok → seluruh jadwal dibatalkan" — bukan
        // hanya notifikasinya, jadi batch ini tidak dijalankan sama sekali
        // pada tanggal libur.
        if (_repositoriKalender.TentukanJenisHari(_connection, tanggal, tahunAjaranId) != JenisHariKalender.Sekolah)
        {
            return;
        }

        var jadwal = _repositoriJadwalHari.CariUntukHari(_connection, tahunAjaranId, HariKalender.DariTanggal(tanggal));
        if (jadwal is not { HariSekolah: true })
        {
            return;
        }

        var toleransi = TimeSpan.FromMinutes(_repositoriPengaturan.BacaInt(_connection, "toleransi_jadwal_menit", 15));

        CobaJalankan(ref _rekapSudahDiproses, jadwal.JamRekap, sekarang, toleransi,
            () => _layananNotifikasi.JalankanRekapPagi(_connection, tanggal, sekarang));

        CobaJalankan(ref _batasAkhirSudahDiproses, jadwal.BatasTapMasuk, sekarang, toleransi,
            () => _layananNotifikasi.JalankanBatasAkhirTapMasuk(_connection, tanggal, sekarang));

        CobaJalankan(ref _bolosSudahDiproses, TambahSatuJam(jadwal.MulaiPulang), sekarang, toleransi,
            () => _layananNotifikasi.JalankanFinalisasiBolos(_connection, tanggal, sekarang));
    }

    // PRD §9.4: jadwal yang sudah lewat lebih dari toleransi dibuang tanpa
    // dijalankan (mis. aplikasi baru menyala 20 menit setelah jam rekap).
    private static void CobaJalankan(ref bool sudahDiproses, TimeOnly? jamJadwal, DateTime sekarang, TimeSpan toleransi, Action jalankan)
    {
        if (sudahDiproses || jamJadwal is not { } jam)
        {
            return;
        }

        var jamSekarang = TimeOnly.FromDateTime(sekarang);
        if (jamSekarang < jam)
        {
            return;
        }

        sudahDiproses = true;

        if (jamSekarang - jam <= toleransi)
        {
            jalankan();
        }
    }

    private static TimeOnly? TambahSatuJam(TimeOnly? jam) => jam?.Add(TimeSpan.FromHours(1));
}
