"""Tes murni app.ui.lobi.tampilan.petakan(): pemetaan HasilSubmit ->
KartuTampilan untuk TEPAT 5 nilai JenisEventScan yang benar-benar
dikembalikan app/server/routers/scan.py, plus kasus gagal terkirim
(jaringan/server) - lihat catatan verifikasi LANGKAH 0 di tampilan.py.
"""

from app.data.models.attendance import JenisEventScan
from app.rfid.base import JalurAlat
from app.ui.lobi.klien_scan import HasilSubmit
from app.ui.lobi.tampilan import WarnaStatus, petakan


def _hasil(**kwargs) -> HasilSubmit:
    dasar = dict(uid_kartu="AABBCC", jalur=JalurAlat.SIMULATOR, terkirim=True)
    dasar.update(kwargs)
    return HasilSubmit(**dasar)


def test_masuk_hadir_hijau_senyap():
    tampilan = petakan(_hasil(jenis_event=JenisEventScan.MASUK, status_masuk="HADIR", nama="Budi"))
    assert tampilan.warna == WarnaStatus.HIJAU
    assert tampilan.berbunyi is False
    assert tampilan.tandai_jalur_gagal is False
    assert tampilan.nama_tampil == "Budi"
    assert tampilan.keterangan == "Hadir"
    assert tampilan.inisial == "B"


def test_masuk_terlambat_kuning_senyap():
    tampilan = petakan(_hasil(jenis_event=JenisEventScan.MASUK, status_masuk="TERLAMBAT", nama="Siti Aminah"))
    assert tampilan.warna == WarnaStatus.KUNING
    assert tampilan.berbunyi is False
    assert tampilan.keterangan == "Terlambat"
    assert tampilan.inisial == "SA"


def test_pulang_hijau_senyap():
    tampilan = petakan(_hasil(jenis_event=JenisEventScan.PULANG, status_final="HADIR", nama="Budi"))
    assert tampilan.warna == WarnaStatus.HIJAU
    assert tampilan.berbunyi is False
    assert tampilan.keterangan == "Absen pulang tercatat"


def test_duplicate_netral_tidak_berbunyi_dan_tidak_menandai_jalur():
    tampilan = petakan(_hasil(jenis_event=JenisEventScan.DUPLICATE, nama="Budi"))
    assert tampilan.warna == WarnaStatus.NETRAL
    assert tampilan.berbunyi is False
    assert tampilan.tandai_jalur_gagal is False
    assert tampilan.keterangan == "Sudah tercatat"


def test_di_luar_periode_netral_tidak_berbunyi():
    tampilan = petakan(_hasil(jenis_event=JenisEventScan.DI_LUAR_PERIODE, nama="Budi"))
    assert tampilan.warna == WarnaStatus.NETRAL
    assert tampilan.berbunyi is False
    assert tampilan.tandai_jalur_gagal is False
    assert tampilan.keterangan == "Di luar jam absen"


def test_unknown_card_merah_berbunyi_dan_tandai_jalur_non_blok():
    tampilan = petakan(_hasil(jenis_event=JenisEventScan.UNKNOWN_CARD, uid_kartu="ZZ99", nama=None))
    assert tampilan.warna == WarnaStatus.MERAH
    assert tampilan.berbunyi is True
    assert tampilan.tandai_jalur_gagal is True
    assert tampilan.nama_tampil == "Kartu tidak dikenal"
    assert "ZZ99" in tampilan.keterangan


def test_diantrekan_netral_senyap_tak_menandai_jalur():
    """Offline (jaringan/server tak terjangkau) SEKARANG masuk antrean
    lokal (§21) - ini bekerja sesuai desain, bukan kegagalan alat, jadi
    TIDAK boleh berbunyi maupun menandai jalur seperti kartu tak dikenal."""
    hasil = HasilSubmit(uid_kartu="AABBCC", jalur=JalurAlat.SIMULATOR, terkirim=False, diantrekan=True)
    tampilan = petakan(hasil)
    assert tampilan.warna == WarnaStatus.NETRAL
    assert tampilan.berbunyi is False
    assert tampilan.tandai_jalur_gagal is False
    assert tampilan.nama_tampil == "Tercatat"


def test_gagal_menyimpan_lokal_merah_berbunyi_dan_tandai_jalur():
    """Beda dari diantrekan: ini kegagalan yang LEBIH serius - tap gagal
    tersimpan bahkan di antrean lokal (mis. berkas antrean tak bisa
    ditulis), bukan sekadar menunggu koneksi. terkirim=False DAN
    diantrekan=False (bawaan) - tetap alarm (§12.5)."""
    hasil = HasilSubmit(
        uid_kartu="AABBCC", jalur=JalurAlat.SIMULATOR, terkirim=False, pesan_galat="Gagal menyimpan lokal: disk penuh"
    )
    tampilan = petakan(hasil)
    assert tampilan.warna == WarnaStatus.MERAH
    assert tampilan.berbunyi is True
    assert tampilan.tandai_jalur_gagal is True
    assert tampilan.keterangan == "Gagal menyimpan lokal: disk penuh"


def test_tanpa_nama_pakai_fallback_dan_tanpa_inisial():
    tampilan = petakan(_hasil(jenis_event=JenisEventScan.MASUK, status_masuk="HADIR", nama=None))
    assert tampilan.nama_tampil == "Tanpa nama"
    assert tampilan.inisial is None
