"""Ekspor laporan ke Excel/PDF berkop sekolah (PRD §14.2). PDF dipakai
sebagai medium arsip (§9.3, lihat app.server.laporan_service) - setiap
halaman mencantumkan kop sekolah dan tanggal+jam cetak lewat hook
header()/footer() fpdf2 (otomatis di SETIAP halaman, tidak perlu ditulis
manual per halaman).

NIK TIDAK PERNAH ditulis di sini (Aturan Global #6) - bukan disamarkan,
app.server.laporan_service memang tidak pernah menyertakan NIK di
HasilLaporan/RekapSiswa/BarisDetailHarian sama sekali, jadi modul ini
tidak punya nilai NIK untuk ditulis walau mau.
"""

from __future__ import annotations

import base64
from io import BytesIO

from fpdf import FPDF
from openpyxl import Workbook
from openpyxl.drawing.image import Image as ExcelImage
from openpyxl.drawing.spreadsheet_drawing import AnchorMarker, OneCellAnchor
from openpyxl.drawing.xdr import XDRPositiveSize2D
from openpyxl.styles import Alignment, Font
from openpyxl.utils import get_column_letter
from openpyxl.utils.units import pixels_to_EMU
from openpyxl.worksheet.worksheet import Worksheet

from app.config.pengaturan import IdentitasSekolah
from app.core.laporan import HasilLaporan
from app.core.status import StatusSiswa

#: Urutan kolom status tetap sama di Excel maupun PDF, mengikuti urutan
#: StatusSiswa (app.core.status) - satu sumber kosakata.
_LABEL_STATUS: dict[str, str] = {
    StatusSiswa.HADIR.value: "Hadir",
    StatusSiswa.TERLAMBAT.value: "Terlambat",
    StatusSiswa.ALPA.value: "Alpa",
    StatusSiswa.IZIN.value: "Izin",
    StatusSiswa.SAKIT.value: "Sakit",
    StatusSiswa.DISPENSASI.value: "Dispensasi",
}
_URUTAN_STATUS = list(_LABEL_STATUS)


def _decode_logo(logo_data_uri: str | None) -> bytes | None:
    if not logo_data_uri or "," not in logo_data_uri:
        return None
    try:
        return base64.b64decode(logo_data_uri.split(",", 1)[1])
    except (ValueError, TypeError):
        return None


def _format_waktu_cetak(waktu_cetak) -> str:
    return waktu_cetak.strftime("%d-%m-%Y %H:%M") + " WITA"


# ---------------------------------------------------------------------------
# PDF
# ---------------------------------------------------------------------------


class _PdfLaporan(FPDF):
    """Header/footer di sini berjalan OTOMATIS di setiap add_page() -
    inilah yang membuat kop + cap waktu cetak konsisten muncul di semua
    halaman (PRD §9.3), bukan logika yang ditulis ulang per halaman."""

    def __init__(self, *, identitas: IdentitasSekolah, waktu_cetak, dicetak_oleh: str) -> None:
        super().__init__(orientation="L", format="A4")
        self._identitas = identitas
        self._waktu_cetak = waktu_cetak
        self._dicetak_oleh = dicetak_oleh
        self._logo = _decode_logo(identitas.logo)
        self.set_auto_page_break(auto=True, margin=18)

    def header(self) -> None:
        y_awal = self.get_y()
        x_teks = 10
        if self._logo:
            self.image(BytesIO(self._logo), x=10, y=y_awal, h=16)
            x_teks = 28

        self.set_xy(x_teks, y_awal)
        self.set_font("Helvetica", "B", 13)
        self.cell(0, 6, self._identitas.nama or "Nama sekolah belum diisi di Pengaturan", new_x="LMARGIN", new_y="NEXT")

        self.set_x(x_teks)
        self.set_font("Helvetica", "", 9)
        keterangan = " - ".join(
            filter(None, [self._identitas.alamat, f"NPSN {self._identitas.npsn}" if self._identitas.npsn else None])
        )
        self.cell(0, 5, keterangan or "Alamat/NPSN belum diisi di Pengaturan", new_x="LMARGIN", new_y="NEXT")

        self.ln(3)
        self.set_draw_color(0, 0, 0)
        self.line(10, self.get_y(), self.w - 10, self.get_y())
        self.ln(4)

    def footer(self) -> None:
        self.set_y(-15)
        self.set_font("Helvetica", "I", 8)
        self.cell(
            0, 8, f"Dicetak {_format_waktu_cetak(self._waktu_cetak)} oleh {self._dicetak_oleh} - Halaman {self.page_no()}",
            align="C",
        )


def _tulis_tabel_rekap_pdf(pdf: _PdfLaporan, hasil: HasilLaporan) -> None:
    kolom = ["Nama Siswa", "Kelas"] + [_LABEL_STATUS[s] for s in _URUTAN_STATUS] + ["Jumlah Hari"]
    lebar = [70, 30] + [22] * len(_URUTAN_STATUS) + [24]

    pdf.set_font("Helvetica", "B", 9)
    for judul, w in zip(kolom, lebar):
        pdf.cell(w, 7, judul, border=1, align="C")
    pdf.ln()

    pdf.set_font("Helvetica", "", 9)
    for baris in hasil.rekap or []:
        nilai = (
            [baris.nama_lengkap, baris.nama_kelas or "-"]
            + [str(baris.jumlah.get(s, 0)) for s in _URUTAN_STATUS]
            + [str(baris.jumlah_hari_tercatat)]
        )
        for teks, w in zip(nilai, lebar):
            pdf.cell(w, 6, str(teks), border=1)
        pdf.ln()


def _tulis_tabel_detail_pdf(pdf: _PdfLaporan, hasil: HasilLaporan) -> None:
    detail = hasil.detail
    assert detail is not None

    pdf.set_font("Helvetica", "B", 11)
    pdf.cell(0, 7, f"{detail.nama_lengkap} - NISN {detail.nisn} - Kelas {detail.nama_kelas or '-'}", new_x="LMARGIN", new_y="NEXT")
    pdf.ln(2)

    kolom = ["Tanggal", "Status Masuk", "Status Final", "Keterangan"]
    lebar = [40, 45, 45, 90]
    pdf.set_font("Helvetica", "B", 9)
    for judul, w in zip(kolom, lebar):
        pdf.cell(w, 7, judul, border=1, align="C")
    pdf.ln()

    pdf.set_font("Helvetica", "", 9)
    for baris in detail.baris:
        nilai = [
            baris.tanggal.isoformat(),
            _LABEL_STATUS.get(baris.status_masuk or "", baris.status_masuk or "-"),
            _LABEL_STATUS.get(baris.status_final or "", baris.status_final or "-"),
            baris.alasan_final or "-",
        ]
        for teks, w in zip(nilai, lebar):
            pdf.cell(w, 6, str(teks), border=1)
        pdf.ln()


def buat_pdf(hasil: HasilLaporan, *, identitas: IdentitasSekolah, waktu_cetak, dicetak_oleh: str) -> bytes:
    pdf = _PdfLaporan(identitas=identitas, waktu_cetak=waktu_cetak, dicetak_oleh=dicetak_oleh)
    pdf.add_page()
    pdf.set_font("Helvetica", "B", 12)
    pdf.cell(0, 8, hasil.label, new_x="LMARGIN", new_y="NEXT")
    pdf.ln(2)

    if hasil.detail is not None:
        _tulis_tabel_detail_pdf(pdf, hasil)
    else:
        _tulis_tabel_rekap_pdf(pdf, hasil)

    keluaran = pdf.output()
    return bytes(keluaran)


# ---------------------------------------------------------------------------
# Excel
# ---------------------------------------------------------------------------


def _tulis_kop_excel(ws: Worksheet, *, identitas: IdentitasSekolah, label: str, lebar_kolom: int) -> int:
    """Kop sekolah di baris-baris atas, kembalikan nomor baris berikutnya
    yang masih kosong (tempat tabel data mulai)."""
    baris = 1
    logo = _decode_logo(identitas.logo)
    if logo:
        try:
            gambar = ExcelImage(BytesIO(logo))
            # openpyxl.drawing.image.Image.width/height TIDAK bertahan
            # setelah save/reload (diverifikasi langsung - logo tampil di
            # ukuran ASLI penuh, bukan yang di-resize, kalau cuma
            # menyetel .width/.height) - ukuran nyata ditentukan
            # anchor.ext, jadi dibuat eksplisit di sini.
            sisi_px = 60
            gambar.anchor = OneCellAnchor(
                _from=AnchorMarker(col=0, row=0, colOff=0, rowOff=0),
                ext=XDRPositiveSize2D(pixels_to_EMU(sisi_px), pixels_to_EMU(sisi_px)),
            )
            ws.add_image(gambar)
            # Baris 1-2 dibuat cukup tinggi menampung logo (satuan openpyxl
            # adalah "points", 1 px bawaan layar ~= 0.75 pt) - tanpa ini
            # logo akan melubangi baris kop teks di bawahnya.
            ws.row_dimensions[1].height = sisi_px * 0.375
            ws.row_dimensions[2].height = sisi_px * 0.375
        except Exception:
            pass  # gambar rusak/format tak didukung - kop teks tetap jalan tanpa logo

    ws.cell(row=baris, column=2, value=identitas.nama or "Nama sekolah belum diisi di Pengaturan").font = Font(
        bold=True, size=13
    )
    baris += 1
    keterangan = " - ".join(
        filter(None, [identitas.alamat, f"NPSN {identitas.npsn}" if identitas.npsn else None])
    )
    ws.cell(row=baris, column=2, value=keterangan or "Alamat/NPSN belum diisi di Pengaturan")
    baris += 2

    ws.cell(row=baris, column=1, value=label).font = Font(bold=True, size=12)
    baris += 2
    return baris


def _tulis_tabel_rekap_excel(ws: Worksheet, hasil: HasilLaporan, baris_mulai: int) -> None:
    kolom = ["Nama Siswa", "Kelas"] + [_LABEL_STATUS[s] for s in _URUTAN_STATUS] + ["Jumlah Hari"]
    for i, judul in enumerate(kolom, start=1):
        sel = ws.cell(row=baris_mulai, column=i, value=judul)
        sel.font = Font(bold=True)
        sel.alignment = Alignment(horizontal="center")

    for offset, baris in enumerate(hasil.rekap or [], start=1):
        r = baris_mulai + offset
        ws.cell(row=r, column=1, value=baris.nama_lengkap)
        ws.cell(row=r, column=2, value=baris.nama_kelas or "-")
        for i, s in enumerate(_URUTAN_STATUS, start=3):
            ws.cell(row=r, column=i, value=baris.jumlah.get(s, 0))
        ws.cell(row=r, column=3 + len(_URUTAN_STATUS), value=baris.jumlah_hari_tercatat)

    for i in range(1, len(kolom) + 1):
        ws.column_dimensions[get_column_letter(i)].width = 18


def _tulis_tabel_detail_excel(ws: Worksheet, hasil: HasilLaporan, baris_mulai: int) -> None:
    detail = hasil.detail
    assert detail is not None
    ws.cell(row=baris_mulai, column=1, value=f"{detail.nama_lengkap} - NISN {detail.nisn} - Kelas {detail.nama_kelas or '-'}").font = Font(
        bold=True
    )
    baris_mulai += 2

    kolom = ["Tanggal", "Status Masuk", "Status Final", "Keterangan"]
    for i, judul in enumerate(kolom, start=1):
        sel = ws.cell(row=baris_mulai, column=i, value=judul)
        sel.font = Font(bold=True)
        sel.alignment = Alignment(horizontal="center")

    for offset, baris in enumerate(detail.baris, start=1):
        r = baris_mulai + offset
        ws.cell(row=r, column=1, value=baris.tanggal.isoformat())
        ws.cell(row=r, column=2, value=_LABEL_STATUS.get(baris.status_masuk or "", baris.status_masuk or "-"))
        ws.cell(row=r, column=3, value=_LABEL_STATUS.get(baris.status_final or "", baris.status_final or "-"))
        ws.cell(row=r, column=4, value=baris.alasan_final or "-")

    for i in range(1, len(kolom) + 1):
        ws.column_dimensions[get_column_letter(i)].width = 22


def buat_excel(hasil: HasilLaporan, *, identitas: IdentitasSekolah) -> bytes:
    wb = Workbook()
    ws = wb.active
    ws.title = "Laporan"

    baris_tabel = _tulis_kop_excel(ws, identitas=identitas, label=hasil.label, lebar_kolom=8)
    if hasil.detail is not None:
        _tulis_tabel_detail_excel(ws, hasil, baris_tabel)
    else:
        _tulis_tabel_rekap_excel(ws, hasil, baris_tabel)

    buffer = BytesIO()
    wb.save(buffer)
    return buffer.getvalue()
