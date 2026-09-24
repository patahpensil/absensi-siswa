using Absensi.Domain.Laporan;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Absensi.Laporan;

// RENCANA-TUGAS.md Tugas 12, PANDUAN-UI-UX.md §11 ("Hasil Cetak"). Lisensi
// QuestPDF Community diset sekali per proses lewat static constructor —
// dipanggil otomatis begitu kelas ini dipakai pertama kali, baik dari
// Absensi.App maupun dari pengujian.
public sealed class PenulisPdfLaporan
{
    static PenulisPdfLaporan()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] TulisKejadian(KepalaLaporan kepala, IReadOnlyList<BarisKejadian> baris, bool tampilkanKelas) =>
        BangunDokumenKejadian(kepala, baris, tampilkanKelas).GeneratePdf();

    // Fitur Cetak (terpisah dari Ekspor PDF, instruksi eksplisit pemilik
    // proyek 2026-09-25): satu gambar PNG per halaman, dibangun dari
    // DOKUMEN QuestPDF YANG SAMA PERSIS dengan TulisKejadian di atas —
    // supaya tata letak, pemisahan baris antar halaman, dan pengulangan
    // kepala/kepala tabel di tiap halaman (keduanya sudah ditangani
    // QuestPDF sendiri lewat page.Header()/table.Header()) identik dengan
    // yang sudah diuji lewat Ekspor PDF, tanpa logika pagination baru yang
    // terpisah dan berisiko tidak konsisten. dpi rendah dipakai di tes
    // (kecepatan); default 200 cukup tajam untuk cetak dokumen teks/tabel.
    public IReadOnlyList<byte[]> GambarKejadian(KepalaLaporan kepala, IReadOnlyList<BarisKejadian> baris, bool tampilkanKelas, int dpi = 200) =>
        GambarHalaman(BangunDokumenKejadian(kepala, baris, tampilkanKelas), dpi);

    private static Document BangunDokumenKejadian(KepalaLaporan kepala, IReadOnlyList<BarisKejadian> baris, bool tampilkanKelas) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(30);
            page.DefaultTextStyle(x => x.FontSize(9));
            page.Header().Element(c => TulisKepala(c, kepala));
            page.Footer().Element(c => TulisKaki(c, kepala));
            page.Content().Column(kolom =>
            {
                kolom.Spacing(10);
                kolom.Item().Element(c => TulisTabelKejadian(c, baris, tampilkanKelas));
                kolom.Item().Element(TulisTandaTangan);
            });
        }));

    // PANDUAN-UI-UX.md §11: laporan bulanan lanskap.
    public byte[] TulisBulanan(KepalaLaporan kepala, LaporanBulanan laporan, JenisSubjekLaporan subjek) =>
        BangunDokumenBulanan(kepala, laporan, subjek).GeneratePdf();

    public IReadOnlyList<byte[]> GambarBulanan(KepalaLaporan kepala, LaporanBulanan laporan, JenisSubjekLaporan subjek, int dpi = 200) =>
        GambarHalaman(BangunDokumenBulanan(kepala, laporan, subjek), dpi);

    // Lebar tetap tabel Bulanan (poin), dipakai bersama oleh
    // KolomPerHalamanBulanan (hitung berapa tanggal muat per halaman) dan
    // TulisTabelBulanan (ColumnsDefinition sungguhan) -- HARUS tetap sama
    // supaya perhitungan dan tata letak sungguhan tidak pernah menyimpang.
    private const double LebarKontenBulanan = 794; // A4 lanskap (842) - 2 x margin (24)
    private const float LebarNamaBulanan = 95;
    private const float LebarIdentitasBulanan = 60;
    private const float LebarKelasBulanan = 40;
    private const float LebarTanggalBulanan = 22;
    private const float LebarRekapBulanan = 30;
    private const float LebarPersenBulanan = 36;

    // Bug ditemukan 2026-09-25 (bukan bug fitur Cetak -- Ekspor PDF yang
    // sudah dipakai sejak awal): tabel Bulanan SATU HALAMAN dengan margin
    // 24 + kolom tanggal 16pt HANYA muat sampai 28 kolom (Februari
    // non-kabisat); bulan APA PUN 29-31 hari (hampir semua bulan) melempar
    // DocumentLayoutException tak tertangkap yang menutup aplikasi.
    // Perbaikan PERTAMA (margin 20 + kolom 14pt, commit ba68604) memang
    // tidak lagi crash, TAPI membuat tabelnya sangat kecil/sulit dibaca --
    // dilaporkan lewat UAT pemilik proyek 2026-09-25. Perbaikan KEDUA ini:
    // font dan lebar kolom dikembalikan ke ukuran yang nyaman dibaca
    // (lihat konstanta Lebar*Bulanan di atas), dan kalau satu bulan penuh
    // tidak muat pada lebar itu, TANGGAL DIBAGI ke beberapa halaman
    // (KolomPerHalamanBulanan/BagiKolomTanggalBulanan) -- bukan
    // mengecilkan huruf/kolom lagi. Nama/Identitas/Kelas dan kepala tabel
    // BERULANG di setiap halaman (page.Header() + table.Header() per
    // halaman); rekap bulanan (H/S/I/.../%) HANYA di halaman TERAKHIR
    // (angka itu ringkasan SATU bulan penuh, mengulanginya di tiap
    // potongan tanggal akan menyiratkan seolah-olah rekap itu milik
    // potongan itu saja -- keliru).
    private static Document BangunDokumenBulanan(KepalaLaporan kepala, LaporanBulanan laporan, JenisSubjekLaporan subjek)
    {
        var potongan = BagiKolomTanggalBulanan(laporan.Kolom, KolomPerHalamanBulanan(subjek));

        return Document.Create(container =>
        {
            for (var i = 0; i < potongan.Count; i++)
            {
                var kolomHalamanIni = potongan[i];
                var halamanTerakhir = i == potongan.Count - 1;

                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(24);
                    page.DefaultTextStyle(x => x.FontSize(9));
                    page.Header().Element(c => TulisKepala(c, kepala));
                    page.Footer().Element(c => TulisKaki(c, kepala));
                    page.Content().Column(kolom =>
                    {
                        kolom.Spacing(8);

                        if (potongan.Count > 1)
                        {
                            kolom.Item().Text(
                                $"Tanggal {kolomHalamanIni[0].Tanggal:d/M} - {kolomHalamanIni[^1].Tanggal:d/M} (halaman {i + 1} dari {potongan.Count})")
                                .SemiBold();
                        }

                        kolom.Item().Element(c => TulisTabelBulanan(c, laporan, kolomHalamanIni, subjek, sertakanRekap: halamanTerakhir));

                        if (halamanTerakhir)
                        {
                            var alasanLibur = laporan.Kolom.Where(k => k.Libur && !string.IsNullOrWhiteSpace(k.Alasan)).ToList();
                            if (alasanLibur.Count > 0)
                            {
                                kolom.Item().Text(text =>
                                {
                                    text.Span("Catatan libur mendadak: ").SemiBold();
                                    text.Span(string.Join("; ", alasanLibur.Select(k => $"{k.Tanggal:d/M} — {k.Alasan}")));
                                });
                            }

                            kolom.Item().Element(TulisTandaTangan);
                        }
                    });
                });
            }
        });
    }

    // Berapa kolom tanggal muat di satu halaman YANG JUGA memuat blok
    // rekap+% (dipakai SERAGAM untuk semua potongan, bukan cuma yang
    // terakhir, supaya lebar tabel konsisten di seluruh laporan --
    // sedikit "boros" ruang di halaman non-terakhir, tapi hasilnya lebih
    // mudah dibaca/dibandingkan antar halaman daripada lebar yang
    // berubah-ubah).
    internal static int KolomPerHalamanBulanan(JenisSubjekLaporan subjek)
    {
        var identitas = LebarNamaBulanan + LebarIdentitasBulanan + (subjek == JenisSubjekLaporan.Siswa ? LebarKelasBulanan : 0);
        var blokRekap = (KolomRekap(subjek).Length * LebarRekapBulanan) + LebarPersenBulanan;
        var tersedia = LebarKontenBulanan - identitas - blokRekap;
        return Math.Max(1, (int)(tersedia / LebarTanggalBulanan));
    }

    internal static IReadOnlyList<IReadOnlyList<KolomTanggalBulanan>> BagiKolomTanggalBulanan(IReadOnlyList<KolomTanggalBulanan> kolom, int ukuranHalaman)
    {
        var hasil = new List<IReadOnlyList<KolomTanggalBulanan>>();
        for (var i = 0; i < kolom.Count; i += ukuranHalaman)
        {
            hasil.Add(kolom.Skip(i).Take(ukuranHalaman).ToList());
        }

        return hasil.Count == 0 ? [kolom] : hasil;
    }

    // PRD §11.3/§17.8: satu kartu per orang, satu halaman per orang supaya
    // bisa dicetak/dipisah sendiri-sendiri walau diminta sekaligus satu
    // kelas.
    public byte[] TulisKartuOrang(KepalaLaporan kepala, IReadOnlyList<KartuOrang> kartu, JenisSubjekLaporan subjek) =>
        BangunDokumenKartuOrang(kepala, kartu, subjek).GeneratePdf();

    public IReadOnlyList<byte[]> GambarKartuOrang(KepalaLaporan kepala, IReadOnlyList<KartuOrang> kartu, JenisSubjekLaporan subjek, int dpi = 200) =>
        GambarHalaman(BangunDokumenKartuOrang(kepala, kartu, subjek), dpi);

    private static Document BangunDokumenKartuOrang(KepalaLaporan kepala, IReadOnlyList<KartuOrang> kartu, JenisSubjekLaporan subjek) =>
        Document.Create(container =>
        {
            foreach (var orang in kartu)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(9));
                    page.Header().Element(c => TulisKepala(c, kepala));
                    page.Footer().Element(c => TulisKaki(c, kepala));
                    page.Content().Column(kolom =>
                    {
                        kolom.Spacing(10);
                        kolom.Item().Element(c => TulisIdentitasOrang(c, orang, subjek));
                        kolom.Item().Element(c => TulisRekap(c, orang.Rekap, subjek));
                        kolom.Item().Text("Rincian hari tidak hadir dan terlambat").SemiBold();
                        kolom.Item().Element(c => TulisTabelRincian(c, orang.Rincian));
                        kolom.Item().Element(TulisTandaTangan);
                    });
                });
            }
        });

    public byte[] TulisSemester(KepalaLaporan kepala, IReadOnlyList<BarisSemester> baris, JenisSubjekLaporan subjek) =>
        BangunDokumenSemester(kepala, baris, subjek).GeneratePdf();

    public IReadOnlyList<byte[]> GambarSemester(KepalaLaporan kepala, IReadOnlyList<BarisSemester> baris, JenisSubjekLaporan subjek, int dpi = 200) =>
        GambarHalaman(BangunDokumenSemester(kepala, baris, subjek), dpi);

    private static Document BangunDokumenSemester(KepalaLaporan kepala, IReadOnlyList<BarisSemester> baris, JenisSubjekLaporan subjek) =>
        Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            page.Margin(30);
            page.DefaultTextStyle(x => x.FontSize(9));
            page.Header().Element(c => TulisKepala(c, kepala));
            page.Footer().Element(c => TulisKaki(c, kepala));
            page.Content().Column(kolom =>
            {
                kolom.Spacing(10);
                kolom.Item().Element(c => TulisTabelSemester(c, baris, subjek));
                kolom.Item().Element(TulisTandaTangan);
            });
        }));

    private static IReadOnlyList<byte[]> GambarHalaman(Document dokumen, int dpi) =>
        dokumen.GenerateImages(new ImageGenerationSettings { ImageFormat = ImageFormat.Png, RasterDpi = dpi }).ToList();

    private static void TulisKepala(IContainer container, KepalaLaporan kepala)
    {
        container.Column(kolom =>
        {
            kolom.Item().Row(baris =>
            {
                if (kepala.LogoPath is { } path && File.Exists(path))
                {
                    baris.ConstantItem(40).Height(40).Image(path).FitArea();
                }

                baris.RelativeItem().PaddingLeft(8).Column(identitas =>
                {
                    identitas.Item().Text(kepala.NamaSekolah).Bold().FontSize(13);
                    if (!string.IsNullOrWhiteSpace(kepala.Npsn))
                    {
                        identitas.Item().Text($"NPSN: {kepala.Npsn}").FontSize(9);
                    }

                    identitas.Item().Text(kepala.Judul).SemiBold().FontSize(11);
                    identitas.Item().Text(kepala.RentangTeks).FontSize(9);
                });
            });
            kolom.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Darken1);
        });
    }

    private static void TulisKaki(IContainer container, KepalaLaporan kepala)
    {
        container.PaddingTop(4).Row(baris =>
        {
            baris.RelativeItem().Text($"Dicetak {kepala.DicetakPada:d MMMM yyyy HH:mm} oleh {kepala.DicetakOleh}").FontSize(7);
            baris.RelativeItem().AlignRight().DefaultTextStyle(x => x.FontSize(7)).Text(text =>
            {
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
        });
    }

    private static void TulisTandaTangan(IContainer container)
    {
        container.PaddingTop(20).AlignRight().Width(200).Column(kolom =>
        {
            kolom.Item().Text("Mengetahui,").FontSize(9);
            kolom.Item().Text("Kepala Sekolah").FontSize(9);
            kolom.Item().PaddingTop(50).LineHorizontal(0.5f).LineColor(Colors.Grey.Darken1);
        });
    }

    private static void TulisTabelKejadian(IContainer container, IReadOnlyList<BarisKejadian> baris, bool tampilkanKelas)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(kolom =>
            {
                kolom.RelativeColumn(2);
                kolom.RelativeColumn(4);
                kolom.RelativeColumn(3);
                if (tampilkanKelas)
                {
                    kolom.RelativeColumn(2);
                }

                kolom.RelativeColumn(1);
                kolom.RelativeColumn(2);
                kolom.RelativeColumn(2);
                kolom.RelativeColumn(5);
            });

            table.Header(header =>
            {
                header.Cell().Element(SelKepala).Text("Tanggal");
                header.Cell().Element(SelKepala).Text("Nama");
                header.Cell().Element(SelKepala).Text("Identitas");
                if (tampilkanKelas)
                {
                    header.Cell().Element(SelKepala).Text("Kelas");
                }

                header.Cell().Element(SelKepala).Text("Status");
                header.Cell().Element(SelKepala).Text("Jam masuk");
                header.Cell().Element(SelKepala).Text("Jam pulang");
                header.Cell().Element(SelKepala).Text("Keterangan");
            });

            foreach (var b in baris)
            {
                table.Cell().Element(SelBadan).Text(b.Tanggal.ToString("dd/MM/yyyy"));
                table.Cell().Element(SelBadan).Text(b.Nama);
                table.Cell().Element(SelBadan).Text(b.Identitas);
                if (tampilkanKelas)
                {
                    table.Cell().Element(SelBadan).Text(b.Kelas ?? "-");
                }

                table.Cell().Element(SelBadan).Text(b.StatusHuruf);
                table.Cell().Element(SelBadan).Text(b.JamMasuk?.ToString("HH:mm") ?? "-");
                table.Cell().Element(SelBadan).Text(b.JamPulang?.ToString("HH:mm") ?? "-");
                table.Cell().Element(SelBadan).Text(b.Keterangan ?? "-");
            }
        });
    }

    // Kolom hari libur diarsir abu-abu muda (PANDUAN-UI-UX.md §11).
    // kolomHalaman: SUBSET laporan.Kolom untuk halaman ini saja (lihat
    // BagiKolomTanggalBulanan) -- Nama/Identitas/Kelas dan kode kehadiran
    // dari laporan.Baris tetap dari laporan LENGKAP (tidak pernah
    // terpotong per orang, cuma kolom tanggalnya yang dipecah).
    // sertakanRekap: hanya true di halaman TERAKHIR (lihat komentar
    // BangunDokumenBulanan soal kenapa rekap tidak diulang tiap halaman).
    private static void TulisTabelBulanan(IContainer container, LaporanBulanan laporan, IReadOnlyList<KolomTanggalBulanan> kolomHalaman, JenisSubjekLaporan subjek, bool sertakanRekap)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(kolom =>
            {
                kolom.ConstantColumn(LebarNamaBulanan);
                kolom.ConstantColumn(LebarIdentitasBulanan);
                if (subjek == JenisSubjekLaporan.Siswa)
                {
                    kolom.ConstantColumn(LebarKelasBulanan);
                }

                foreach (var _ in kolomHalaman)
                {
                    kolom.ConstantColumn(LebarTanggalBulanan);
                }

                if (sertakanRekap)
                {
                    foreach (var _ in KolomRekap(subjek))
                    {
                        kolom.ConstantColumn(LebarRekapBulanan);
                    }

                    kolom.ConstantColumn(LebarPersenBulanan);
                }
            });

            table.Header(header =>
            {
                header.Cell().Element(SelKepala).Text("Nama");
                header.Cell().Element(SelKepala).Text("Identitas");
                if (subjek == JenisSubjekLaporan.Siswa)
                {
                    header.Cell().Element(SelKepala).Text("Kls");
                }

                foreach (var k in kolomHalaman)
                {
                    header.Cell().Element(c => SelKepalaTanggal(c, k.Libur)).Text(k.Tanggal.Day.ToString());
                }

                if (sertakanRekap)
                {
                    foreach (var judul in KolomRekap(subjek))
                    {
                        header.Cell().Element(SelKepala).Text(judul);
                    }

                    header.Cell().Element(SelKepala).Text("%");
                }
            });

            foreach (var b in laporan.Baris)
            {
                table.Cell().Element(SelBadan).Text(b.Nama);
                table.Cell().Element(SelBadan).Text(b.Identitas);
                if (subjek == JenisSubjekLaporan.Siswa)
                {
                    table.Cell().Element(SelBadan).Text(b.Kelas ?? "-");
                }

                foreach (var k in kolomHalaman)
                {
                    var kode = b.KodePerTanggal.TryGetValue(k.Tanggal, out var nilai) ? nilai : "";
                    table.Cell().Element(c => SelBadanTanggal(c, k.Libur)).AlignCenter().Text(kode);
                }

                if (sertakanRekap)
                {
                    foreach (var nilai in NilaiRekap(b.Rekap, subjek))
                    {
                        table.Cell().Element(SelBadan).Text(nilai.ToString());
                    }

                    table.Cell().Element(SelBadan).Text($"{b.Rekap.Persentase:0.0}");
                }
            }
        });
    }

    // Format kolom rekap guru — Hadir · Tidak Lengkap · Sakit · Izin ·
    // Dinas Luar · Cuti · Alpa (dikonfirmasi pemilik proyek 2026-09-18,
    // PRD §17.3) — sengaja BEDA urutan dari siswa, bukan siswa+tambahan,
    // karena guru tidak punya Terlambat/Bolos. Dipakai bersama oleh grid
    // bulanan, kartu per orang, dan tabel semester supaya ketiganya
    // konsisten satu sama lain.
    private static string[] KolomRekap(JenisSubjekLaporan subjek) => subjek == JenisSubjekLaporan.Guru
        ? ["H", "L", "S", "I", "DL", "C", "A"]
        : ["H", "S", "I", "A", "T", "Bolos"];

    private static int[] NilaiRekap(RekapKehadiran rekap, JenisSubjekLaporan subjek) => subjek == JenisSubjekLaporan.Guru
        ? [rekap.Hadir, rekap.TidakLengkap, rekap.Sakit, rekap.Izin, rekap.DinasLuar, rekap.Cuti, rekap.Alpa]
        : [rekap.Hadir, rekap.Sakit, rekap.Izin, rekap.Alpa, rekap.Terlambat, rekap.Bolos];

    private static void TulisIdentitasOrang(IContainer container, KartuOrang orang, JenisSubjekLaporan subjek)
    {
        container.Row(baris =>
        {
            if (orang.FotoPath is { } path && File.Exists(path))
            {
                baris.ConstantItem(70).Height(90).Border(1).BorderColor(Colors.Grey.Lighten1).Image(path).FitArea();
            }

            baris.RelativeItem().PaddingLeft(10).Column(identitas =>
            {
                identitas.Item().Text(orang.Nama).Bold().FontSize(13);
                identitas.Item().Text($"{(subjek == JenisSubjekLaporan.Siswa ? "NISN" : "NIP")}: {orang.Identitas}");
                if (orang.Kelas is not null)
                {
                    identitas.Item().Text($"Kelas: {orang.Kelas}");
                }
            });
        });
    }

    private static void TulisRekap(IContainer container, RekapKehadiran rekap, JenisSubjekLaporan subjek)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(kolom =>
            {
                // +1: kolom "Kehadiran" (%), selalu ditambahkan setelah label rekap.
                for (var i = 0; i < LabelPenuhRekap(subjek).Length + 1; i++)
                {
                    kolom.RelativeColumn();
                }
            });

            table.Header(header =>
            {
                foreach (var judul in LabelPenuhRekap(subjek))
                {
                    header.Cell().Element(SelKepala).Text(judul);
                }

                header.Cell().Element(SelKepala).Text("Kehadiran");
            });

            foreach (var nilai in NilaiRekap(rekap, subjek))
            {
                table.Cell().Element(SelBadan).Text(nilai.ToString());
            }

            table.Cell().Element(SelBadan).Text($"{rekap.Persentase:0.0}% ({rekap.HariEfektif} hari efektif)");
        });
    }

    // Versi kata penuh KolomRekap, dipakai kartu per orang dan tabel
    // semester (cukup ruang, tidak seperti grid bulanan yang harus muat
    // satu kolom per tanggal juga).
    private static string[] LabelPenuhRekap(JenisSubjekLaporan subjek) => subjek == JenisSubjekLaporan.Guru
        ? ["Hadir", "Tidak lengkap", "Sakit", "Izin", "Dinas luar", "Cuti", "Alpa"]
        : ["Hadir", "Sakit", "Izin", "Alpa", "Terlambat", "Bolos"];

    private static void TulisTabelRincian(IContainer container, IReadOnlyList<RincianKejadian> rincian)
    {
        if (rincian.Count == 0)
        {
            container.Text("Tidak ada catatan tidak hadir atau terlambat pada rentang ini.").Italic();
            return;
        }

        container.Table(table =>
        {
            table.ColumnsDefinition(kolom =>
            {
                kolom.RelativeColumn(2);
                kolom.RelativeColumn(1);
                kolom.RelativeColumn(4);
            });

            table.Header(header =>
            {
                header.Cell().Element(SelKepala).Text("Tanggal");
                header.Cell().Element(SelKepala).Text("Status");
                header.Cell().Element(SelKepala).Text("Keterangan");
            });

            foreach (var r in rincian)
            {
                table.Cell().Element(SelBadan).Text(r.Tanggal.ToString("dd/MM/yyyy"));
                table.Cell().Element(SelBadan).Text(r.StatusHuruf);
                table.Cell().Element(SelBadan).Text(r.Keterangan ?? "-");
            }
        });
    }

    private static void TulisTabelSemester(IContainer container, IReadOnlyList<BarisSemester> baris, JenisSubjekLaporan subjek)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(kolom =>
            {
                kolom.RelativeColumn(4);
                kolom.RelativeColumn(3);
                if (subjek == JenisSubjekLaporan.Siswa)
                {
                    kolom.RelativeColumn(2);
                }

                foreach (var _ in KolomRekap(subjek))
                {
                    kolom.RelativeColumn(1);
                }

                kolom.RelativeColumn(2);
            });

            table.Header(header =>
            {
                header.Cell().Element(SelKepala).Text("Nama");
                header.Cell().Element(SelKepala).Text("Identitas");
                if (subjek == JenisSubjekLaporan.Siswa)
                {
                    header.Cell().Element(SelKepala).Text("Kelas");
                }

                foreach (var judul in KolomRekap(subjek))
                {
                    header.Cell().Element(SelKepala).Text(judul);
                }

                header.Cell().Element(SelKepala).Text("% Kehadiran");
            });

            foreach (var b in baris)
            {
                table.Cell().Element(SelBadan).Text(b.Nama);
                table.Cell().Element(SelBadan).Text(b.Identitas);
                if (subjek == JenisSubjekLaporan.Siswa)
                {
                    table.Cell().Element(SelBadan).Text(b.Kelas ?? "-");
                }

                foreach (var nilai in NilaiRekap(b.Rekap, subjek))
                {
                    table.Cell().Element(SelBadan).Text(nilai.ToString());
                }

                table.Cell().Element(SelBadan).Text($"{b.Rekap.Persentase:0.0}%");
            }
        });
    }

    private static IContainer SelKepala(IContainer container) => container
        .DefaultTextStyle(x => x.SemiBold())
        .PaddingVertical(3).PaddingHorizontal(2)
        .BorderBottom(1).BorderColor(Colors.Grey.Darken2);

    private static IContainer SelKepalaTanggal(IContainer container, bool libur) => container
        .Background(libur ? Colors.Grey.Lighten2 : Colors.White)
        .DefaultTextStyle(x => x.SemiBold())
        .PaddingVertical(3)
        .AlignCenter()
        .BorderBottom(1).BorderColor(Colors.Grey.Darken2);

    private static IContainer SelBadan(IContainer container) => container
        .PaddingVertical(2).PaddingHorizontal(2)
        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);

    private static IContainer SelBadanTanggal(IContainer container, bool libur) => container
        .Background(libur ? Colors.Grey.Lighten2 : Colors.White)
        .PaddingVertical(2)
        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2);
}
