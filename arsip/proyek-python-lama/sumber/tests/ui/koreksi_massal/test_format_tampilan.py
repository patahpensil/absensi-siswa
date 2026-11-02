"""Tes format_tampilan murni Koreksi Massal - tanpa Qt."""

from app.ui.koreksi_massal.format_tampilan import format_cakupan, format_jenis_tindakan


def test_format_cakupan_sekolah_satu_tanggal():
    cakupan = format_cakupan(
        {
            "tanggal_mulai": "2026-09-08",
            "tanggal_selesai": "2026-09-08",
            "lingkup": "SEKOLAH",
            "tingkat": None,
            "id_kelas": None,
            "id_siswa_terpilih": [],
            "status_termasuk": ["ALPA", "TERLAMBAT"],
        }
    )
    assert cakupan == "2026-09-08 - Seluruh sekolah - Status: ALPA, TERLAMBAT"


def test_format_cakupan_rentang_tingkat():
    cakupan = format_cakupan(
        {
            "tanggal_mulai": "2026-09-01",
            "tanggal_selesai": "2026-09-05",
            "lingkup": "TINGKAT",
            "tingkat": 8,
            "id_kelas": None,
            "id_siswa_terpilih": [],
            "status_termasuk": ["ALPA"],
        }
    )
    assert cakupan == "2026-09-01 s/d 2026-09-05 - Tingkat 8 - Status: ALPA"


def test_format_cakupan_kelas():
    cakupan = format_cakupan(
        {
            "tanggal_mulai": "2026-09-01",
            "tanggal_selesai": "2026-09-01",
            "lingkup": "KELAS",
            "id_kelas": 3,
            "tingkat": None,
            "id_siswa_terpilih": [],
            "status_termasuk": [],
        }
    )
    assert cakupan == "2026-09-01 - Kelas ID 3 - Status: -"


def test_format_cakupan_siswa_terpilih():
    cakupan = format_cakupan(
        {
            "tanggal_mulai": "2026-09-01",
            "tanggal_selesai": "2026-09-01",
            "lingkup": "SISWA_TERPILIH",
            "id_siswa_terpilih": [3, 5, 9],
            "tingkat": None,
            "id_kelas": None,
            "status_termasuk": ["ALPA"],
        }
    )
    assert cakupan == "2026-09-01 - 3 siswa terpilih - Status: ALPA"


def test_format_jenis_tindakan_ubah_status():
    assert format_jenis_tindakan("UBAH_STATUS", "SAKIT") == "Ubah Status -> SAKIT"


def test_format_jenis_tindakan_tanpa_status_baru():
    assert format_jenis_tindakan("TANDAI_IZIN_PULANG_CEPAT", None) == "Tandai Izin Pulang Cepat"
    assert format_jenis_tindakan("BATALKAN_KONVERSI_ALPA_PULANG", None) == "Batalkan Konversi Alpa Pulang"
