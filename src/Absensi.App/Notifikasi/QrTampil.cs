namespace Absensi.App.Notifikasi;

// Data untuk satu kartu QR yang ditampilkan/dicetak — nama+NISN/NIP dicetak
// di bawah QR supaya operator tahu punya siapa (PRD §16.6 langkah 5: "QR
// dibagikan ke orang tua lewat surat edaran").
public sealed record QrTampil(string Nama, string NisnAtauNip, string Url);
