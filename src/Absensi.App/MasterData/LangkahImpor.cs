namespace Absensi.App.MasterData;

// docs/PRD.md §14.5: "Kemajuan langkah: Analisis → Validasi → Preview →
// Impor." Urutan enum ini dipakai sebagai urutan progres (perbandingan
// >= pada enum bekerja berdasar urutan deklarasi).
public enum LangkahImpor
{
    Analisis,
    Validasi,
    Preview,
    Impor,
}
