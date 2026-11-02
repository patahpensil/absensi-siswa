namespace Absensi.Telegram.Tests;

// Pengganti IKlienBotTelegram untuk pengujian — tidak pernah menyentuh
// jaringan sungguhan.
internal sealed class KlienBotTelegramPalsu : IKlienBotTelegram
{
    private readonly Queue<PembaruanTelegram> _pembaruanAntre = new();

    public List<(string ChatId, string Teks)> PesanTerkirim { get; } = new();

    // ChatIdGagal = ditolak Telegram (hitung percobaan); JaringanMati = tidak
    // ada koneksi sama sekali (tidak boleh menghabiskan jatah percobaan).
    public HashSet<string> ChatIdGagal { get; } = new();

    public bool JaringanMati { get; set; }

    public int JumlahPanggilanKirim { get; private set; }

    public int JumlahGagalBerturutan { get; set; }

    public void AntrekanPembaruan(PembaruanTelegram pembaruan) => _pembaruanAntre.Enqueue(pembaruan);

    public Task<HasilKirimPesan> KirimPesanAsync(string tokenBot, string chatId, string teks)
    {
        JumlahPanggilanKirim++;
        if (JaringanMati)
        {
            return Task.FromResult(HasilKirimPesan.GagalJaringan);
        }

        if (ChatIdGagal.Contains(chatId))
        {
            return Task.FromResult(HasilKirimPesan.Ditolak);
        }

        PesanTerkirim.Add((chatId, teks));
        return Task.FromResult(HasilKirimPesan.Berhasil);
    }

    public Task<IReadOnlyList<PembaruanTelegram>> AmbilPembaruanAsync(string tokenBot, long offsetTerakhir)
    {
        var hasil = _pembaruanAntre.ToList();
        _pembaruanAntre.Clear();
        return Task.FromResult<IReadOnlyList<PembaruanTelegram>>(hasil);
    }
}
