using System.Windows.Input;

namespace Absensi.App.Mvvm;

// Implementasi ICommand sendiri, tanpa pustaka MVVM pihak ketiga
// (CLAUDE.md §2: jangan menambah pustaka besar tanpa persetujuan).
public sealed class RelayCommand : ICommand
{
    private readonly Action _eksekusi;
    private readonly Func<bool>? _bisaEksekusi;

    public RelayCommand(Action eksekusi, Func<bool>? bisaEksekusi = null)
    {
        _eksekusi = eksekusi;
        _bisaEksekusi = bisaEksekusi;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _bisaEksekusi?.Invoke() ?? true;

    public void Execute(object? parameter) => _eksekusi();

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}

// Dipakai untuk aksi per baris tabel (mis. Edit, Nonaktifkan) yang butuh
// tahu baris mana yang diklik lewat CommandParameter.
public sealed class RelayCommand<T> : ICommand
{
    private readonly Action<T> _eksekusi;
    private readonly Func<T, bool>? _bisaEksekusi;

    public RelayCommand(Action<T> eksekusi, Func<T, bool>? bisaEksekusi = null)
    {
        _eksekusi = eksekusi;
        _bisaEksekusi = bisaEksekusi;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _bisaEksekusi?.Invoke((T)parameter!) ?? true;

    public void Execute(object? parameter) => _eksekusi((T)parameter!);

    public void RaiseCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
