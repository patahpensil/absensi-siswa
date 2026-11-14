using System.Collections.ObjectModel;
using Absensi.App.Mvvm;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain;
using Microsoft.Data.Sqlite;

namespace Absensi.App.MasterData;

public enum AcademicPeriodAction
{
    Prepare,
    Promote,
    Graduate,
    Activate,
    ChangeDates,
}

public sealed record AcademicPeriodActionOption(AcademicPeriodAction Value, string Label);

public sealed record PeriodOption(AcademicPeriod Period)
{
    public int Id => Period.Id;

    public string Label => $"{Period.Name} · {Period.Semester} · {Period.Start:dd/MM/yyyy} – {Period.End:dd/MM/yyyy}{(Period.Active ? " (aktif)" : "")}";
}

public sealed class StudentChoice(SiswaDaftar student) : ObservableObject
{
    private bool _selected = true;

    public int Id => student.Id;

    public string Name => student.Nama;

    public string Nisn => student.Nisn;

    public bool Selected
    {
        get => _selected;
        set => SetField(ref _selected, value);
    }
}

// PRD §16.14 (desktop): the same five actions as the web "Tahun ajaran" dialog.
// Every rule (authority, overlap, recorded days, membership) is checked by
// AcademicPeriodService; this view model only gathers the choices.
public sealed class AcademicPeriodViewModel : ObservableObject
{
    private readonly SqliteConnection _connection;
    private readonly AkunTersimpan _account;
    private readonly IPenyediaWaktu _clock;
    private readonly AcademicPeriodService _service = new();
    private readonly RepositoriTahunAjaran _periods = new();
    private readonly RepositoriKelas _classes = new();

    private AcademicPeriodAction _action = AcademicPeriodAction.Prepare;
    private string _name = string.Empty;
    private string _semester = "genap";
    private DateTime? _start;
    private DateTime? _end;
    private PeriodOption? _sourcePeriod;
    private KelasDaftar? _sourceClass;
    private PeriodOption? _targetPeriod;
    private KelasDaftar? _targetClass;
    private PeriodOption? _editPeriod;
    private DateTime? _editStart;
    private DateTime? _editEnd;
    private bool _confirmed;
    private string? _message;
    private bool _succeeded;

    public AcademicPeriodViewModel(SqliteConnection connection, AkunTersimpan account, IPenyediaWaktu clock)
    {
        _connection = connection;
        _account = account;
        _clock = clock;
        SaveCommand = new RelayCommand(Save, CanSave);
        ReloadPeriods();
        var latestEnd = Periods.Select(p => p.Period.End).DefaultIfEmpty(DateOnly.FromDateTime(clock.Sekarang)).Max();
        _start = latestEnd.AddDays(1).ToDateTime(TimeOnly.MinValue);
        _end = latestEnd.AddDays(1).AddMonths(6).AddDays(-1).ToDateTime(TimeOnly.MinValue);
    }

    public event EventHandler? PeriodsChanged;

    public IReadOnlyList<AcademicPeriodActionOption> Actions { get; } =
    [
        new(AcademicPeriodAction.Prepare, "Siapkan periode baru"),
        new(AcademicPeriodAction.Promote, "Naik kelas massal"),
        new(AcademicPeriodAction.Graduate, "Tetapkan lulus"),
        new(AcademicPeriodAction.Activate, "Aktifkan periode"),
        new(AcademicPeriodAction.ChangeDates, "Ubah rentang periode"),
    ];

    public IReadOnlyList<string> Semesters { get; } = ["ganjil", "genap"];

    public ObservableCollection<PeriodOption> Periods { get; } = new();

    public ObservableCollection<KelasDaftar> SourceClasses { get; } = new();

    public ObservableCollection<PeriodOption> TargetPeriods { get; } = new();

    public ObservableCollection<KelasDaftar> TargetClasses { get; } = new();

    public ObservableCollection<StudentChoice> Students { get; } = new();

    public RelayCommand SaveCommand { get; }

    public AcademicPeriodAction SelectedAction
    {
        get => _action;
        set
        {
            if (!SetField(ref _action, value)) return;
            SourcePeriod = null;
            TargetPeriod = null;
            EditPeriod = null;
            Confirmed = false;
            Message = null;
            RefreshTargetPeriods();
            OnPropertyChanged(nameof(ShowPrepare));
            OnPropertyChanged(nameof(ShowSource));
            OnPropertyChanged(nameof(ShowTargetPeriod));
            OnPropertyChanged(nameof(ShowTargetClass));
            OnPropertyChanged(nameof(ShowConfirm));
            OnPropertyChanged(nameof(ShowChangeDates));
            OnPropertyChanged(nameof(ConfirmText));
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    public bool ShowPrepare => _action == AcademicPeriodAction.Prepare;

    public bool ShowSource => _action is AcademicPeriodAction.Promote or AcademicPeriodAction.Graduate;

    public bool ShowTargetPeriod => _action is AcademicPeriodAction.Promote or AcademicPeriodAction.Activate;

    public bool ShowTargetClass => _action == AcademicPeriodAction.Promote;

    public bool ShowConfirm => _action is AcademicPeriodAction.Graduate or AcademicPeriodAction.Activate;

    public bool ShowChangeDates => _action == AcademicPeriodAction.ChangeDates;

    public string ConfirmText => _action == AcademicPeriodAction.Graduate
        ? "Saya setuju menonaktifkan siswa terpilih dan kartu aktifnya."
        : "Saya setuju menjadikan periode tujuan sebagai periode aktif.";

    public string Name
    {
        get => _name;
        set { if (SetField(ref _name, value)) SaveCommand.RaiseCanExecuteChanged(); }
    }

    public string Semester
    {
        get => _semester;
        set => SetField(ref _semester, value);
    }

    public DateTime? Start
    {
        get => _start;
        set { if (SetField(ref _start, value)) SaveCommand.RaiseCanExecuteChanged(); }
    }

    public DateTime? End
    {
        get => _end;
        set { if (SetField(ref _end, value)) SaveCommand.RaiseCanExecuteChanged(); }
    }

    public PeriodOption? SourcePeriod
    {
        get => _sourcePeriod;
        set
        {
            if (!SetField(ref _sourcePeriod, value)) return;
            SourceClasses.Clear();
            if (value is not null)
            {
                foreach (var room in _classes.DaftarUntukTahunAjaran(_connection, value.Id)
                             .Where(room => _action != AcademicPeriodAction.Graduate || room.Tingkat == 9))
                {
                    SourceClasses.Add(room);
                }
            }

            SourceClass = null;
            TargetPeriod = null;
            RefreshTargetPeriods();
        }
    }

    public KelasDaftar? SourceClass
    {
        get => _sourceClass;
        set
        {
            if (!SetField(ref _sourceClass, value)) return;
            LoadStudents();
            Confirmed = false;
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    public PeriodOption? TargetPeriod
    {
        get => _targetPeriod;
        set
        {
            if (!SetField(ref _targetPeriod, value)) return;
            TargetClasses.Clear();
            if (value is not null && _action == AcademicPeriodAction.Promote)
            {
                foreach (var room in _classes.DaftarUntukTahunAjaran(_connection, value.Id))
                {
                    TargetClasses.Add(room);
                }
            }

            TargetClass = null;
            Confirmed = false;
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    public KelasDaftar? TargetClass
    {
        get => _targetClass;
        set { if (SetField(ref _targetClass, value)) SaveCommand.RaiseCanExecuteChanged(); }
    }

    public PeriodOption? EditPeriod
    {
        get => _editPeriod;
        set
        {
            if (!SetField(ref _editPeriod, value)) return;
            EditStart = value?.Period.Start.ToDateTime(TimeOnly.MinValue);
            EditEnd = value?.Period.End.ToDateTime(TimeOnly.MinValue);
            Message = null;
            SaveCommand.RaiseCanExecuteChanged();
        }
    }

    public DateTime? EditStart
    {
        get => _editStart;
        set { if (SetField(ref _editStart, value)) SaveCommand.RaiseCanExecuteChanged(); }
    }

    public DateTime? EditEnd
    {
        get => _editEnd;
        set { if (SetField(ref _editEnd, value)) SaveCommand.RaiseCanExecuteChanged(); }
    }

    public bool Confirmed
    {
        get => _confirmed;
        set { if (SetField(ref _confirmed, value)) SaveCommand.RaiseCanExecuteChanged(); }
    }

    public string? Message
    {
        get => _message;
        private set => SetField(ref _message, value);
    }

    public bool Succeeded
    {
        get => _succeeded;
        private set => SetField(ref _succeeded, value);
    }

    private bool CanSave() => _action switch
    {
        AcademicPeriodAction.Prepare => !string.IsNullOrWhiteSpace(_name) && _start is not null && _end is not null,
        AcademicPeriodAction.Promote => _sourceClass is not null && _targetClass is not null && Students.Any(s => s.Selected),
        AcademicPeriodAction.Graduate => _sourceClass is not null && Students.Any(s => s.Selected) && _confirmed,
        AcademicPeriodAction.Activate => _targetPeriod is not null && _confirmed,
        AcademicPeriodAction.ChangeDates => _editPeriod is not null && _editStart is not null && _editEnd is not null,
        _ => false,
    };

    private void Save()
    {
        var time = _clock.Sekarang;
        var selected = Students.Where(s => s.Selected).Select(s => s.Id).ToList();
        string result = string.Empty;
        Succeeded = false;
        var ok = PenangananGalat.Coba(() =>
        {
            switch (_action)
            {
                case AcademicPeriodAction.Prepare:
                    _service.Prepare(_connection, _account, time, _name, _semester,
                        DateOnly.FromDateTime(_start!.Value), DateOnly.FromDateTime(_end!.Value));
                    result = "Periode disiapkan. Buat kelas untuk periode itu di layar Kelas (pilih periodenya di atas tabel).";
                    break;
                case AcademicPeriodAction.Promote:
                    _service.Promote(_connection, _account, time, _sourceClass!.Id, _targetClass!.Id, selected);
                    result = $"{selected.Count} siswa ditempatkan pada kelas {_targetClass.Nama}.";
                    break;
                case AcademicPeriodAction.Graduate:
                    _service.Graduate(_connection, _account, time, _sourceClass!.Id, selected);
                    result = $"{selected.Count} siswa ditetapkan lulus; kartunya dinonaktifkan.";
                    break;
                case AcademicPeriodAction.Activate:
                    _service.Activate(_connection, _account, time, _targetPeriod!.Id);
                    result = "Periode aktif diperbarui. Keluar lalu masuk lagi agar seluruh layar memakai periode baru.";
                    break;
                case AcademicPeriodAction.ChangeDates:
                    _service.ChangeDates(_connection, _account, time, _editPeriod!.Id,
                        DateOnly.FromDateTime(_editStart!.Value), DateOnly.FromDateTime(_editEnd!.Value));
                    result = $"Rentang periode menjadi {_editStart:dd/MM/yyyy} – {_editEnd:dd/MM/yyyy}.";
                    break;
            }
        }, error => Message = error, "AcademicPeriodViewModel.Save");

        if (!ok)
        {
            return;
        }

        Succeeded = true;
        Message = result;
        Confirmed = false;
        if (_action is AcademicPeriodAction.Prepare or AcademicPeriodAction.Activate or AcademicPeriodAction.ChangeDates)
        {
            ReloadPeriods();
            PeriodsChanged?.Invoke(this, EventArgs.Empty);
        }

        if (_action is AcademicPeriodAction.Promote or AcademicPeriodAction.Graduate)
        {
            LoadStudents();
        }
    }

    private void ReloadPeriods()
    {
        Periods.Clear();
        foreach (var period in _periods.ListPeriods(_connection))
        {
            Periods.Add(new PeriodOption(period));
        }

        RefreshTargetPeriods();
    }

    // Promote: periods after the source; Activate: every inactive period.
    private void RefreshTargetPeriods()
    {
        TargetPeriods.Clear();
        foreach (var option in Periods.Where(option => _action == AcademicPeriodAction.Activate
                     ? !option.Period.Active
                     : _sourcePeriod is { } source && option.Period.Start > source.Period.End))
        {
            TargetPeriods.Add(option);
        }
    }

    private void LoadStudents()
    {
        foreach (var student in Students)
        {
            student.PropertyChanged -= OnStudentChanged;
        }

        Students.Clear();
        if (_sourceClass is { } room && _sourcePeriod is { } period)
        {
            foreach (var student in new RepositoriSiswa().DaftarUntukTahunAjaran(_connection, period.Id)
                         .Where(student => student.Aktif && student.KelasNama == room.Nama))
            {
                var choice = new StudentChoice(student);
                choice.PropertyChanged += OnStudentChanged;
                Students.Add(choice);
            }
        }

        SaveCommand.RaiseCanExecuteChanged();
    }

    private void OnStudentChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) =>
        SaveCommand.RaiseCanExecuteChanged();
}
