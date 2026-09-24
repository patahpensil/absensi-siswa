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

    public byte[] TulisKejadian(KepalaLaporan kepala, IReadOnlyList<BarisKejadian> baris, bool tampilkanKelas)
    {
        var dokumen = Document.Create(container => container.Page(page =>
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

        return dokumen.GeneratePdf();
    }

    // PANDUAN-UI-UX.md §11: laporan bulanan lanskap.
    public byte[] TulisBulanan(KepalaLaporan kepala, LaporanBulanan laporan, JenisSubjekLaporan subjek)
    {
        var dokumen = Document.Create(container => container.Page(page =>
        {
            page.Size(PageSizes.A4.Landscape());
            // Bug ditemukan 2026-09-25: margin 24 + kolom tanggal 16pt
            // (TulisTabelBulanan) HANYA muat sampai 28 kolom (Februari
            // non-kabisat). Bulan APA PUN 29-31 hari (hampir semua bulan)
            // melempar QuestPDF.Drawing.Exceptions.DocumentLayoutException
            // -- turunan langsung System.Exception, BUKAN
            // InvalidOperationException/ArgumentException, jadi TIDAK
            // tertangkap LaporanViewModel.Ekspor dan menutup aplikasi.
            // margin 20 dipilih supaya bulan 31 hari (terpanjang) tetap
            // muat dengan sisa ruang (bukan pas-pasan di batas), tanpa
            // mengurangi kolom atau data apa pun.
            page.Margin(20);
            page.DefaultTextStyle(x => x.FontSize(7));
            page.Header().Element(c => TulisKepala(c, kepala));
            page.Footer().Element(c => TulisKaki(c, kepala));
            page.Content().Column(kolom =>
            {
                kolom.Spacing(8);
                kolom.Item().Element(c => TulisTabelBulanan(c, laporan, subjek));

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
            });
        }));

        return dokumen.GeneratePdf();
    }

    // PRD §11.3/§17.8: satu kartu per orang, satu halaman per orang supaya
    // bisa dicetak/dipisah sendiri-sendiri walau diminta sekaligus satu
    // kelas.
    public byte[] TulisKartuOrang(KepalaLaporan kepala, IReadOnlyList<KartuOrang> kartu, JenisSubjekLaporan subjek)
    {
        var dokumen = Document.Create(container =>
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

        return dokumen.GeneratePdf();
    }

    public byte[] TulisSemester(KepalaLaporan kepala, IReadOnlyList<BarisSemester> baris, JenisSubjekLaporan subjek)
    {
        var dokumen = Document.Create(container => container.Page(page =>
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

        return dokumen.GeneratePdf();
    }

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
    private static void TulisTabelBulanan(IContainer container, LaporanBulanan laporan, JenisSubjekLaporan subjek)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(kolom =>
            {
                kolom.ConstantColumn(90);
                kolom.ConstantColumn(55);
                if (subjek == JenisSubjekLaporan.Siswa)
                {
                    kolom.ConstantColumn(40);
                }

                // 14 (bukan 16 semula) -- lihat komentar margin di
                // TulisBulanan: pada margin 20 dan lebar tetap lainnya, 14
                // adalah lebar terbesar yang masih muat untuk Siswa (kolom
                // paling sempit, karena ada kolom Kelas) pada bulan 31 hari
                // terpanjang, dengan sisa ruang wajar -- bukan angka
                // sembarang.
                foreach (var _ in laporan.Kolom)
                {
                    kolom.ConstantColumn(14);
                }

                foreach (var _ in KolomRekap(subjek))
                {
                    kolom.ConstantColumn(20);
                }

                kolom.ConstantColumn(30);
            });

            table.Header(header =>
            {
                header.Cell().Element(SelKepala).Text("Nama");
                header.Cell().Element(SelKepala).Text("Identitas");
                if (subjek == JenisSubjekLaporan.Siswa)
                {
                    header.Cell().Element(SelKepala).Text("Kls");
                }

                foreach (var k in laporan.Kolom)
                {
                    header.Cell().Element(c => SelKepalaTanggal(c, k.Libur)).Text(k.Tanggal.Day.ToString());
                }

                foreach (var judul in KolomRekap(subjek))
                {
                    header.Cell().Element(SelKepala).Text(judul);
                }

                header.Cell().Element(SelKepala).Text("%");
            });

            foreach (var b in laporan.Baris)
            {
                table.Cell().Element(SelBadan).Text(b.Nama);
                table.Cell().Element(SelBadan).Text(b.Identitas);
                if (subjek == JenisSubjekLaporan.Siswa)
                {
                    table.Cell().Element(SelBadan).Text(b.Kelas ?? "-");
                }

                foreach (var k in laporan.Kolom)
                {
                    var kode = b.KodePerTanggal.TryGetValue(k.Tanggal, out var nilai) ? nilai : "";
                    table.Cell().Element(c => SelBadanTanggal(c, k.Libur)).AlignCenter().Text(kode);
                }

                foreach (var nilai in NilaiRekap(b.Rekap, subjek))
                {
                    table.Cell().Element(SelBadan).Text(nilai.ToString());
                }

                table.Cell().Element(SelBadan).Text($"{b.Rekap.Persentase:0.0}");
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
