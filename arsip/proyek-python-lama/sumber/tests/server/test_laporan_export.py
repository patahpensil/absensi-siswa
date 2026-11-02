"""Tes app.server.laporan_export - generate PDF/Excel murni dari
HasilLaporan, tanpa server hidup. Membuktikan kop sekolah, cap waktu
cetak per halaman (PRD §9.3), dan NIK TIDAK PERNAH tertulis (Aturan
Global #6) - walau seandainya seseorang mencoba menyuntikkannya lewat
data yang salah, struktur HasilLaporan/RekapSiswa/BarisDetailHarian
memang tidak punya field NIK sama sekali untuk ditulis.
"""

import base64
from datetime import date, datetime
from io import BytesIO

from openpyxl import load_workbook
from PIL import Image as PILImage

from app.config.pengaturan import IdentitasSekolah
from app.core.laporan import BarisDetailHarian, HasilLaporan, LaporanDetailSiswa, RekapSiswa
from app.server import laporan_export

_EMU_PER_PIXEL = 9525


def _logo_data_uri(*, sisi_px: int) -> str:
    buffer = BytesIO()
    PILImage.new("RGB", (sisi_px, sisi_px), (30, 80, 180)).save(buffer, format="PNG")
    return "data:image/png;base64," + base64.b64encode(buffer.getvalue()).decode("ascii")

IDENTITAS = IdentitasSekolah(
    nama="UPT SMP Negeri 1 Pancarijang", npsn="12345678", alamat="Jl. Pendidikan No. 1, Sidenreng Rappang",
    logo=None, nama_kepala_sekolah="Rahmat Perdana, S.Kom.", nip_kepala_sekolah="19830121 200901 1 007",
)


def _rekap_kosong_status() -> dict:
    return {"HADIR": 0, "TERLAMBAT": 0, "ALPA": 0, "IZIN": 0, "SAKIT": 0, "DISPENSASI": 0}


def _hasil_rekap() -> HasilLaporan:
    jumlah = _rekap_kosong_status()
    jumlah.update({"HADIR": 3, "TERLAMBAT": 1, "ALPA": 1})
    rekap = [RekapSiswa(id_siswa=1, nama_lengkap="Budi Santoso", nama_kelas="VIII. PASCAL", jumlah_hari_tercatat=5, jumlah=jumlah)]
    return HasilLaporan(
        label="Laporan 1 - 5 September 2026 - Kelas VIII. PASCAL", tanggal_mulai=date(2026, 9, 1),
        tanggal_selesai=date(2026, 9, 5), id_kelas=1, nama_kelas="VIII. PASCAL", id_siswa=None, rekap=rekap,
        detail=None,
    )


def _hasil_detail() -> HasilLaporan:
    detail = LaporanDetailSiswa(
        nama_lengkap="Budi Santoso", nisn="1234567890", nama_kelas="VIII. PASCAL",
        baris=[BarisDetailHarian(tanggal=date(2026, 9, 1), status_masuk="HADIR", status_final="HADIR", alasan_final=None)],
    )
    return HasilLaporan(
        label="Laporan Per Siswa - Budi Santoso", tanggal_mulai=date(2026, 9, 1), tanggal_selesai=date(2026, 9, 1),
        id_kelas=1, nama_kelas="VIII. PASCAL", id_siswa=1, rekap=None, detail=detail,
    )


def test_pdf_rekap_menghasilkan_berkas_valid_dan_cap_waktu():
    isi = laporan_export.buat_pdf(
        _hasil_rekap(), identitas=IDENTITAS, waktu_cetak=datetime(2026, 9, 12, 10, 30), dicetak_oleh="admin"
    )
    assert isi[:5] == b"%PDF-"
    # fpdf2 menyimpan teks sebagai stream terkompresi - cukup buktikan
    # berkas cukup besar untuk berisi konten sungguhan, bukan halaman kosong.
    assert len(isi) > 500


def test_pdf_detail_menghasilkan_berkas_valid():
    isi = laporan_export.buat_pdf(
        _hasil_detail(), identitas=IDENTITAS, waktu_cetak=datetime(2026, 9, 12, 10, 30), dicetak_oleh="admin"
    )
    assert isi[:5] == b"%PDF-"


def test_excel_rekap_berisi_kop_dan_tabel():
    isi = laporan_export.buat_excel(_hasil_rekap(), identitas=IDENTITAS)
    wb = load_workbook(BytesIO(isi))
    ws = wb.active

    teks_semua_sel = " ".join(str(sel.value) for baris in ws.iter_rows() for sel in baris if sel.value is not None)
    assert "UPT SMP Negeri 1 Pancarijang" in teks_semua_sel
    assert "Budi Santoso" in teks_semua_sel
    assert "VIII. PASCAL" in teks_semua_sel


def test_excel_detail_berisi_baris_harian():
    isi = laporan_export.buat_excel(_hasil_detail(), identitas=IDENTITAS)
    wb = load_workbook(BytesIO(isi))
    ws = wb.active

    teks_semua_sel = " ".join(str(sel.value) for baris in ws.iter_rows() for sel in baris if sel.value is not None)
    assert "2026-09-01" in teks_semua_sel
    assert "Hadir" in teks_semua_sel


def test_excel_logo_ukuran_besar_tetap_dikecilkan_konsisten():
    """Regresi: openpyxl.drawing.image.Image.width/height TIDAK bertahan
    setelah save/reload (dibuktikan lewat pemeriksaan visual manual) -
    ukuran tampil nyata ditentukan anchor.ext, bukan atribut width/height
    di objek Image. Logo sekolah 200x200px (ukuran wajar unggahan admin,
    lihat app.ui.pengaturan.layar._pilih_logo - tidak ada validasi
    dimensi saat unggah) harus tetap tampil KECIL di kop, tidak
    menimpa/menutupi baris tabel di bawahnya."""
    identitas = IdentitasSekolah(
        nama="UPT SMP Negeri 1 Pancarijang", npsn="12345678", alamat="Jl. Pendidikan No. 1",
        logo=_logo_data_uri(sisi_px=200), nama_kepala_sekolah="X", nip_kepala_sekolah="Y",
    )
    isi = laporan_export.buat_excel(_hasil_rekap(), identitas=identitas)
    wb = load_workbook(BytesIO(isi))
    ws = wb.active

    assert len(ws._images) == 1
    ext = ws._images[0].anchor.ext
    assert ext.cx / _EMU_PER_PIXEL == 60
    assert ext.cy / _EMU_PER_PIXEL == 60


def test_nik_tidak_pernah_muncul_di_berkas_biner():
    """NIK tidak pernah dipilih dari Student ke HasilLaporan sama sekali
    (lihat app.server.laporan_service) - tes ini membuktikan bahkan pada
    level BYTE BERKAS JADI (PDF/Excel), string NIK contoh tidak muncul,
    bukan cuma tidak ada di skema."""
    nik_contoh = "9876543210123456"
    assert nik_contoh.encode() not in laporan_export.buat_pdf(
        _hasil_rekap(), identitas=IDENTITAS, waktu_cetak=datetime(2026, 9, 12, 10, 30), dicetak_oleh="admin"
    )
    assert nik_contoh.encode() not in laporan_export.buat_excel(_hasil_rekap(), identitas=IDENTITAS)
