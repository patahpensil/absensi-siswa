using Absensi.Domain.Laporan;

namespace Absensi.App.Laporan;

// Pembungkus tampilan untuk ComboBox layar Laporan — Id null pada OpsiKelas
// berarti "Semua kelas". Bukan DTO domain, sengaja tinggal di App karena
// cuma dipakai mengikat ComboBox.
public sealed record OpsiKelas(int? Id, string Nama);

public sealed record OpsiOrang(int Id, string Nama);

// Label Indonesia untuk ComboBox Jenis/Subjek — enum ToString() saja tidak
// cukup rapi untuk JenisLaporan.PerOrang ("PerOrang" tanpa spasi).
public sealed record OpsiJenisLaporan(JenisLaporan Nilai, string Label);

public sealed record OpsiSubjekLaporan(JenisSubjekLaporan Nilai, string Label);
