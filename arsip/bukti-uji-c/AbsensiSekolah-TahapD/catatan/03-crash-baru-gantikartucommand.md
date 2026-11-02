# Temuan Baru — Crash NullReferenceException di GantiKartuCommand (BUKAN D-1, BUKAN D-2)

## Kapan terjadi

2026-09-21 19:09:14, PID 7020, saat mempersiapkan D2.18 (data realistis untuk
Laporan) — tepat setelah mengimpor 2 siswa fiktif (Kelas "7A" baru dibuat
lebih dulu) dan berpindah/merender ulang layar Siswa.

## Bukti log (`logs/20260921.log`)

Tiga entri berurutan, SEMUA timestamp identik `19:09:14` (beda dari D-2 yang
punya jeda ~2,5 menit antara catatan pertama dan kedua):

```
[FATAL] [DispatcherUnhandledException]  <- entri 1
[FATAL] [DispatcherUnhandledException]  <- entri 2 (exception KEDUA masuk
                                             selagi entri 1 masih diproses —
                                             flag _sedangMenanganiGalatFatal
                                             mencegah dialog KEDUA muncul,
                                             tapi PencatatGalat tetap mencatat
                                             keduanya, sesuai rancangan)
[FATAL] [AppDomain.UnhandledException (IsTerminating=True)]  <- entri 3
```

Isi ketiganya sama:
```
System.NullReferenceException: Object reference not set to an instance of an object.
   HResult=-2147467261, Source=Absensi.App, TargetSite=<.ctor>b__24_0
   at Absensi.App.MasterData.SiswaViewModel.<>c.<.ctor>b__24_0(SiswaDaftar s)
   at System.Windows.Controls.Primitives.ButtonBase.UpdateCanExecute()
   at System.Windows.FrameworkElement.OnPropertyChanged(...)
   at System.Windows.DependencyObject.NotifyPropertyChange(...)
   ... at System.Windows.Data.BindingExpression.Activate(Object item)
   ... at MS.Internal.Data.DataBindEngine.OnLayoutUpdated(...)
   ... at System.Windows.ContextLayoutManager.UpdateLayout()
```

## Bukti Event Viewer + DUMP (berbeda dari D-2 — kali ini WER penuh aktif)

- "Application Error": 1 event baru, `0xe0434352`, PID `0x1b6c` (=7020 heksadesimal? — dicek: 0x1b6c = 7020 desimal, cocok)
- "Windows Error Reporting": 2 event baru (APPCRASH lengkap dengan Fault bucket)
- **Dump tercipta**: `C:\AbsensiSekolah-TahapD\dumps\Absensi.App.exe.7020.dmp`
  - Ukuran: **677.026.712 byte (~646 MiB)**
  - SHA-256: `d1d711bf172f72a76e724621743ee28dd09db1ea5d461d1f0a98695a91ee238c`
  - **TIDAK dipindahkan ke repository** — tetap di folder uji di luar repo.

**Beda penting dari D-2:** D-2 TIDAK menghasilkan dump/Application-Error/WER
(dijelaskan sebelumnya: `Handled=false` diikuti dismissal manual yang
menuntun ke terminasi ".NET Runtime" bersih). Kali ini dump LENGKAP
tercipta — kemungkinan karena tidak ada interaksi manual yang menunda
penutupan MessageBox (crash ini terjadi otomatis saat automasi UI berjalan,
tidak ada `{ENTER}` yang saya kirim untuk membubarkan dialog galatnya
sebelum proses berhenti sendiri lewat jalur WER penuh).

## Root cause — KEYAKINAN TINGGI

**File:** `src/Absensi.App/MasterData/SiswaViewModel.cs`, baris **73**:
```csharp
GantiKartuCommand = new RelayCommand<SiswaDaftar>(BukaDialogGantiKartu, s => s.PunyaKartu);
```

`RelayCommand<T>.CanExecute` (`Mvvm/RelayCommand.cs`): `_bisaEksekusi?.Invoke((T)parameter!)`
— untuk `T` bertipe referensi (`SiswaDaftar`), `(SiswaDaftar)null!` TIDAK
melempar (cast referensi ke null valid), sehingga predikat `s => s.PunyaKartu`
dipanggil dengan `s = null` → `NullReferenceException` pada akses
`s.PunyaKartu`.

**Pemicu:** WPF `ButtonBase.UpdateCanExecute()` bisa memanggil `CanExecute`
dengan `CommandParameter` bernilai `null` secara TRANSIEN selama re-layout
(mis. tepat setelah `ItemsSource` DataGrid berubah — di sini setelah impor 2
siswa baru, grid Siswa merender ulang) — sebelum binding `CommandParameter="{Binding}"`
per baris sempat "settle" ke instance `SiswaDaftar` yang benar.

**Bandingkan dengan command sejenis di file yang sama:** `NonaktifkanCommand`/
`UnggahFotoCommand`/`EditCommand` (baris 63-66) TIDAK punya predikat
CanExecute custom (default selalu `true`) — HANYA `GantiKartuCommand` yang
punya lambda `s => s.PunyaKartu`, dan HANYA itu yang crash. Ini pola yang
sangat spesifik dan mudah diverifikasi lagi kalau diperlukan.

## Klasifikasi

**BUKAN D-1** (bukan stack overflow, tidak ada tanda rekursi). **BUKAN D-2**
(kelas/exception/method sama sekali berbeda). Ini **bug ketiga, baru
ditemukan**, terpisah dari cakupan investigasi awal — dicatat apa adanya,
TIDAK diperbaiki (sesuai arahan Tahap D: tidak memperbaiki kode).

## Tindakan setelah temuan ini

Sesuai arahan ("kalau crash terjadi, amankan dump lalu hentikan pengujian
TERKAIT"): pengujian di layar Siswa (grid dengan tombol "Ganti Kartu")
DIHENTIKAN untuk sisa sesi Tahap D ini. D2.18 (data realistis untuk Laporan)
dilanjutkan TANPA membuka ulang grid Siswa — cukup memakai data yang sudah
berhasil diimpor sebelum crash ini (2 siswa fiktif tersimpan valid di DB,
dikonfirmasi lewat log tidak ada rollback). Skenario Kelompok 3 sisanya
(D3.4-8, D3.10) akan memakai layar Guru (bukan Siswa) untuk menghindari
memicu ulang bug ini secara tidak sengaja.
