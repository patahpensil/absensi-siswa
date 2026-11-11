using Absensi.Domain.Notifikasi;

namespace Absensi.Data.Repositori;

// Satu baris `outbox` (docs/SKEMA-DATABASE.md §8).
public sealed record OutboxBaris(
    int Id,
    DateOnly Tanggal,
    JenisSubjekOutbox SubjekTipe,
    int SubjekId,
    JenisNotifikasi Jenis,
    string ChatId,
    string Isi,
    string Status,
    int Percobaan,
    DateTime DibuatPada,
    DateTime? TerkirimPada,
    string? Galat,
    NotificationChannel Channel = NotificationChannel.Telegram);
