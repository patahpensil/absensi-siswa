using Absensi.Data;
using Absensi.Data.Gerbang;
using Absensi.Data.Repositori;
using Absensi.Domain.Kehadiran;

namespace Absensi.Web.Layanan;

// The TV receives presentation data only. SQLite and attendance decisions stay
// in the authenticated controller window, so two windows never write two DB copies.
public sealed class GateController(StatusAplikasi state)
{
    private readonly GateAccess access = new();
    private readonly List<GateCard> cards = new();
    public bool HasSession { get; private set; }
    public bool Receiving { get; private set; }
    public bool DisplayOpen { get; private set; }
    public string? Error { get; set; }

    public void Authorize() => access.EnsureAuthorized(state.Koneksi!, state.Akun);

    public void OpenDisplay()
    {
        Authorize();
        if (!DisplayOpen) access.Record(state.Koneksi!, state.Akun, state.Jam.Sekarang, GateAction.OpenDisplay);
        HasSession = true;
        DisplayOpen = true;
    }

    public void SetReceiving(bool receiving)
    {
        Authorize();
        HasSession = true;
        if (Receiving == receiving) return;
        access.Record(state.Koneksi!, state.Akun, state.Jam.Sekarang,
            receiving ? GateAction.StartReceiving : GateAction.StopReceiving);
        Receiving = receiving;
    }

    public bool ConfirmPassword(string password) => access.VerifyPassword(state.Koneksi!, state.Akun, password);

    public void CloseDisplay()
    {
        Authorize();
        if (DisplayOpen) access.Record(state.Koneksi!, state.Akun, state.Jam.Sekarang, GateAction.CloseDisplay);
        DisplayOpen = false;
    }

    public void LoseSession()
    {
        HasSession = false;
        Receiving = false;
        DisplayOpen = false;
        cards.Clear();
    }

    public async Task<bool> ProcessAsync(string uid)
    {
        Authorize();
        if (!Receiving || string.IsNullOrWhiteSpace(uid) || uid.Length > 64) return false;
        uid = uid.Trim();
        var owner = new RepositoriKartu().CariAktifBerdasarkanUid(state.Koneksi!, uid);
        var now = state.Jam.Sekarang;
        var decision = new PemrosesTap().Proses(state.Koneksi!, uid, now, false);
        var teacher = owner?.PemilikTipe == JenisPemilikKartu.Guru;
        var identity = "—";
        var name = "Kartu tidak dikenal";
        string? photo = null;
        if (owner is not null && teacher)
        {
            var person = new RepositoriGuru().DaftarLengkap(state.Koneksi!).First(g => g.Id == owner.PemilikId);
            name = person.Nama;
            identity = person.Nip;
            photo = state.PhotoUrl($"{identity}.png");
        }
        else if (owner is not null)
        {
            var person = WorkspaceView.Students(state).FirstOrDefault(s => s.Id == owner.PemilikId);
            name = person?.Nama ?? "Siswa tidak aktif";
            identity = person?.Nisn ?? "—";
            photo = state.PhotoUrl($"{identity}.png");
        }
        var rejected = decision.Tindakan == JenisTindakanTap.Ditolak;
        var title = decision.Tindakan switch
        {
            JenisTindakanTap.IsiJamMasuk => decision.StatusSiswaBaru?.ToString() ?? decision.StatusGuruBaru?.ToString() ?? "Masuk",
            JenisTindakanTap.IsiJamPulang => "Pulang tercatat",
            JenisTindakanTap.TidakBerubah => $"Status tetap {decision.StatusSiswaBaru?.ToString() ?? decision.StatusGuruBaru?.ToString()}",
            JenisTindakanTap.Diabaikan => "Sudah tercatat",
            _ => "Ditolak",
        };
        var message = decision.Alasan switch
        {
            AlasanTap.KartuTidakTerdaftar => "Kartu belum terdaftar. Hubungi admin/TU.",
            AlasanTap.JedaTapTerlaluCepat => "Jeda tap belum mencapai 15 detik.",
            AlasanTap.PemilikTidakAktif => "Pemilik kartu tidak aktif.",
            AlasanTap.TanggalLibur => "Hari libur. Absensi tidak diubah.",
            AlasanTap.BelumPunyaCatatanMasuk => "Belum ada catatan masuk. Hubungi petugas untuk koreksi.",
            AlasanTap.LewatBatasTapMasuk => "Batas tap masuk sudah lewat. Hubungi petugas untuk koreksi.",
            AlasanTap.StatusSudahDitetapkanDiMuka => "Status resmi tidak diubah. Hubungi petugas untuk koreksi.",
            AlasanTap.JadwalBelumDiatur => "Jadwal sekolah belum lengkap.",
            _ => "",
        };
        var tone = rejected ? "danger" : decision.StatusSiswaBaru == StatusAbsensiSiswa.Terlambat ? "warning" : "positive";
        await state.SimpanAsync();
        cards.Insert(0, new GateCard(Guid.NewGuid().ToString("N"), teacher ? "GURU" : owner is null ? "KARTU" : "SISWA",
            name, identity, photo, title, message, now.ToString("HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture), tone, DateTime.UtcNow));
        if (cards.Count > 8) cards.RemoveRange(8, cards.Count - 8);
        return true;
    }

    public GateSnapshot Snapshot()
    {
        Authorize();
        var date = DateOnly.FromDateTime(state.Jam.Sekarang);
        var students = WorkspaceView.Attendance(state);
        var teachers = new RepositoriAbsensi().DaftarGuruUntukNotifikasi(state.Koneksi!, date);
        return new GateSnapshot(state.NamaSekolah, state.TahunAjaran, state.Jam.Sekarang.ToString("dd/MM/yyyy HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
            Receiving, state.StorageFailed, students.Count(x => x.Status == StatusAbsensiSiswa.Hadir),
            students.Count(x => x.Status == StatusAbsensiSiswa.Terlambat),
            students.Count(x => x.JamMasuk is null && x.Status == StatusAbsensiSiswa.Alpa),
            teachers.Count(x => x.Status is StatusAbsensiGuru.Masuk or StatusAbsensiGuru.Hadir or StatusAbsensiGuru.TidakLengkap),
            cards.Where(x => DateTime.UtcNow - x.CreatedUtc < TimeSpan.FromSeconds(8)).Take(4).ToArray(), cards.Take(4).ToArray());
    }
}

public sealed record GateCard(string Id, string Kind, string Name, string Identity, string? Photo, string Status,
    string Message, string Time, string Tone, DateTime CreatedUtc);
public sealed record GateSnapshot(string School, string AcademicYear, string Clock, bool Receiving, bool StorageFailed,
    int Present, int Late, int NotTapped, int Teachers, GateCard[] Cards, GateCard[] Recent);
