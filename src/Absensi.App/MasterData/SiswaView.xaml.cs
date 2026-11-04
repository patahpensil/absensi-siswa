using System.Windows;
using System.Windows.Controls;

namespace Absensi.App.MasterData;

// DataContext diisi otomatis oleh DataTemplate (Resources/GayaAplikasi.xaml)
// saat dipakai sebagai konten MainWindow, bukan lewat konstruktor.
//
// Formulir tambah/ubah tampil di dialog (FormSiswaWindow) yang dibuka saat
// SiswaViewModel.FormulirDiminta terpicu — hanya penyambung peristiwa,
// tanpa logika (PRD §24.1).
public partial class SiswaView : UserControl
{
    private SiswaViewModel? _viewModelTerhubung;

    public SiswaView()
    {
        InitializeComponent();

        // Langganan hanya selama View ini TERPASANG (Loaded..Unloaded): SiswaViewModel
        // di-cache antar navigasi, sedangkan View dibuat baru tiap menu diklik — tanpa
        // ini View lama yang belum di-GC ikut membuka dialog kedua/ketiga.
        DataContextChanged += (_, e) =>
        {
            Berhenti();
            _viewModelTerhubung = e.NewValue as SiswaViewModel;
            if (IsLoaded)
            {
                Mulai();
            }
        };
        Loaded += (_, _) => Mulai();
        Unloaded += (_, _) => Berhenti();
    }

    private bool _berlangganan;

    private void Mulai()
    {
        if (_viewModelTerhubung is not null && !_berlangganan)
        {
            _viewModelTerhubung.FormulirDiminta += BukaFormulir;
            _berlangganan = true;
        }
    }

    private void Berhenti()
    {
        if (_viewModelTerhubung is not null && _berlangganan)
        {
            _viewModelTerhubung.FormulirDiminta -= BukaFormulir;
            _berlangganan = false;
        }
    }

    private void BukaFormulir(object? sender, EventArgs e)
    {
        if (_viewModelTerhubung is null)
        {
            return;
        }

        new FormSiswaWindow(_viewModelTerhubung) { Owner = Window.GetWindow(this) }.ShowDialog();
    }
}
