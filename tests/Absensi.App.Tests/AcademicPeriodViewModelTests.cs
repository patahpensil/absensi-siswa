using Absensi.App.MasterData;
using Absensi.Data.MasterData;
using Absensi.Data.Repositori;
using Absensi.Domain.Akun;

namespace Absensi.App.Tests;

// PRD §16.14 desktop: the "Tahun ajaran" window drives AcademicPeriodService.
public sealed class AcademicPeriodViewModelTests
{
    private static readonly DateTime Now = new(2026, 9, 17, 9, 0, 0);
    private static readonly AkunTersimpan Admin = new(1, "admin1", "x", PeranAkun.Admin, null, true);

    private static MigratedDatabase Prepare()
    {
        var database = new MigratedDatabase();
        database.SeedSekolahUntukGerbang();
        database.TambahAkun(1, "admin1", "admin", guruId: null);
        return database;
    }

    [Fact]
    public void PrepareThenChangeDates_StoredAndPeriodListRefreshed()
    {
        using var database = Prepare();
        var viewModel = new AcademicPeriodViewModel(database.Connection, Admin, new PenyediaWaktuUji(Now));
        var changes = 0;
        viewModel.PeriodsChanged += (_, _) => changes++;

        viewModel.Name = "2026/2027";
        viewModel.Semester = "genap";
        viewModel.Start = new DateTime(2027, 1, 4);
        viewModel.End = new DateTime(2027, 6, 30);
        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.Succeeded, viewModel.Message);
        Assert.Equal(2, viewModel.Periods.Count);

        viewModel.SelectedAction = AcademicPeriodAction.ChangeDates;
        viewModel.EditPeriod = viewModel.Periods.Single(p => !p.Period.Active);
        Assert.Equal(new DateTime(2027, 6, 30), viewModel.EditEnd);
        viewModel.EditEnd = new DateTime(2027, 6, 26);
        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.Succeeded, viewModel.Message);
        Assert.Contains(new RepositoriTahunAjaran().ListPeriods(database.Connection), p => p.End == new DateOnly(2027, 6, 26));
        Assert.Equal(2, changes);
    }

    [Fact]
    public void Promote_OnlyTickedStudentsMoveToTargetClass()
    {
        using var database = Prepare();
        var service = new AcademicPeriodService();
        var next = service.Prepare(database.Connection, Admin, Now, "2026/2027", "genap", new DateOnly(2027, 1, 4), new DateOnly(2027, 6, 30));
        var targetClass = new LayananKelas().Tambah(database.Connection, Admin, Now, next, "8A", 8, null);
        var viewModel = new AcademicPeriodViewModel(database.Connection, Admin, new PenyediaWaktuUji(Now))
        {
            SelectedAction = AcademicPeriodAction.Promote,
        };

        viewModel.SourcePeriod = viewModel.Periods.Single(p => p.Id == 1);
        viewModel.SourceClass = viewModel.SourceClasses.Single();
        Assert.Equal(5, viewModel.Students.Count);
        viewModel.Students.Single(s => s.Id == 1).Selected = false;
        viewModel.TargetPeriod = viewModel.TargetPeriods.Single();
        viewModel.TargetClass = viewModel.TargetClasses.Single(c => c.Id == targetClass);
        Assert.True(viewModel.SaveCommand.CanExecute(null));
        viewModel.SaveCommand.Execute(null);

        Assert.True(viewModel.Succeeded, viewModel.Message);
        var placed = new RepositoriSiswa().DaftarUntukTahunAjaran(database.Connection, next).Where(s => s.KelasNama == "8A").ToList();
        Assert.Equal(4, placed.Count);
        Assert.DoesNotContain(placed, s => s.Id == 1);
    }

    [Fact]
    public void Activate_RequiresConfirmation()
    {
        using var database = Prepare();
        var viewModel = new AcademicPeriodViewModel(database.Connection, Admin, new PenyediaWaktuUji(Now))
        {
            SelectedAction = AcademicPeriodAction.Activate,
        };
        Assert.Empty(viewModel.TargetPeriods);
        Assert.False(viewModel.SaveCommand.CanExecute(null));
    }
}
