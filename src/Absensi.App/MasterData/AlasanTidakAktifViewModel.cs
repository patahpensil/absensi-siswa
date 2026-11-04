using Absensi.App.Mvvm;

namespace Absensi.App.MasterData;

// Dialog kecil dipakai bersama oleh layar Siswa (PRD §5.3: lulus/pindah/
// keluar) dan Guru (pensiun/pindah) untuk meminta alasan sebelum menandai
// tidak aktif — tidak ada penghapusan (PRD §5.3/§12).
public sealed class AlasanTidakAktifViewModel : ObservableObject
{
    private string _alasan;

    public AlasanTidakAktifViewModel(IReadOnlyList<string> pilihan)
    {
        Pilihan = pilihan;
        _alasan = pilihan[0];
        KonfirmasiCommand = new RelayCommand(() => Dikonfirmasi?.Invoke(this, Alasan));
    }

    public event EventHandler<string>? Dikonfirmasi;

    public IReadOnlyList<string> Pilihan { get; }

    public string Alasan
    {
        get => _alasan;
        set => SetField(ref _alasan, value);
    }

    public RelayCommand KonfirmasiCommand { get; }
}
