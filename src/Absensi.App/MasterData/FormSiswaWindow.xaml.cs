using System.Windows;

namespace Absensi.App.MasterData;

// Dialog formulir tambah/ubah siswa. Semua logika di SiswaViewModel; jendela ini
// hanya menutup diri saat ViewModel memberi tahu (Simpan berhasil / Batal) dan
// meminta ViewModel membatalkan bila ditutup lewat tombol X.
public partial class FormSiswaWindow : Window
{
    private readonly SiswaViewModel _viewModel;
    private bool _ditutupViewModel;

    public FormSiswaWindow(SiswaViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        // Layar pendek: jangan lebih tinggi dari area kerja (isian menggulir, tombol tetap terlihat).
        MaxHeight = SystemParameters.WorkArea.Height - 16;

        _viewModel.FormulirSelesai += SaatFormulirSelesai;
        Closed += (_, _) => _viewModel.FormulirSelesai -= SaatFormulirSelesai;
        Closing += (_, _) =>
        {
            // Ditutup lewat X / Alt+F4: kembalikan ViewModel ke keadaan "tambah" kosong.
            if (!_ditutupViewModel)
            {
                _ditutupViewModel = true;
                _viewModel.FormulirSelesai -= SaatFormulirSelesai;
                _viewModel.BatalEditCommand.Execute(null);
            }
        };

        Loaded += (_, _) => KolomNisn.Focus();
    }

    private void SaatFormulirSelesai(object? sender, EventArgs e)
    {
        if (_ditutupViewModel)
        {
            return;
        }

        _ditutupViewModel = true;
        Close();
    }
}
