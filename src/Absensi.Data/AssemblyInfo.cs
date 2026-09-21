using System.Runtime.CompilerServices;

// A-12 (Master Bug & UI/UX Register): satu-satunya alasan atribut ini ada
// -- ImporSiswaExcel.KolomBaku/ImporGuruExcel.KolomBaku sengaja internal
// (bukan public), dan test template di Absensi.Data.Tests perlu
// membandingkan header hasil PembuatTemplateExcel ke KolomBaku sebagai
// sumber kebenaran, tanpa menduplikasinya sebagai literal baru.
[assembly: InternalsVisibleTo("Absensi.Data.Tests")]
