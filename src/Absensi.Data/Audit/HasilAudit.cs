namespace Absensi.Data.Audit;

public sealed record HasilAudit(IReadOnlyList<BarisAuditGabungan> Baris, IReadOnlyList<ArsipTidakBisaDibaca> ArsipBermasalah);
