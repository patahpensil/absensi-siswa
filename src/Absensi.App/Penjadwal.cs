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
// sama sekali, bukan hanya notifikasinya — lihat DetakAsync).
public sealed class Penjadwal
{
    private readonly SqliteConnection _connection;
    private readonly IPenyediaWaktu _penyediaWaktu;
    private readonly LayananNotifikasi _layananNotifikasi = new();
    private readonly RepositoriJadwalHari _repositoriJadwalHari = new();
    private readonly RepositoriTahunAjaran _repositoriTahunAjaran = new();
    private readonly RepositoriKalender _repositoriKalender = new();
    private readonly RepositoriPengaturan _repositoriPengaturan = new();
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
    // tertangkap, cukup jarang supaya tidak membebani.
    public async Task DetakAsync()
    {
        var sekarang = _penyediaWaktu.Sekarang;
        var tanggal = DateOnly.FromDateTime(sekarang);

        if (_tanggalTerakhirDiproses != tanggal)
        {
            _tanggalTerakhirDiproses = tanggal;
            _rekapSudahDiproses = false;
            _batasAkhirSudahDiproses = false;
            _bolosSudahDiproses = false;
        }

        JalankanBatchBilaSaatnya(tanggal, sekarang);

        await _pengirimOutbox.KirimAntreanAsync(_connection, _penyediaWaktu);
        await _pemrosesPembaruanPairing.ProsesPembaruanAsync(_connection, _penyediaWaktu);
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
