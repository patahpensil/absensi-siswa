# Absensi Siswa RFID

Aplikasi desktop standalone untuk absensi siswa dan guru menggunakan kartu RFID, dengan notifikasi otomatis lewat Telegram. Dibangun untuk UPT SMP Negeri 1 Pancarijang, dirancang agar dapat dipasang di sekolah lain.

Baca `CLAUDE.md` dan `docs/PRD.md` sebelum mengerjakan tugas apa pun.

## Struktur

```
/src
  /Absensi.Domain    - logika penentuan status kehadiran, tanpa UI dan tanpa database
  /Absensi.Data      - akses SQLite, migrasi, repositori
  /Absensi.Telegram  - pemanggilan Bot API, outbox
  /Absensi.Laporan   - keluaran Excel dan PDF
  /Absensi.App       - antarmuka WPF
/tests
  /Absensi.Domain.Tests - pengujian aturan status dan jadwal
/docs                - PRD, skema database, panduan UI/UX
```

## Tumpukan Teknologi

- .NET 8 (C#)
- WPF untuk antarmuka
- SQLite lewat Microsoft.Data.Sqlite
- xUnit untuk pengujian

## Menjalankan

```
dotnet build
dotnet test
```
