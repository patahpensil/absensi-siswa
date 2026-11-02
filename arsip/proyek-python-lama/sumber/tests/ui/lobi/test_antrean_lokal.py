"""Tes murni AntreanLokal: simpan-sebelum-forward, FIFO by waktu_scan,
tidak menghapus sebelum ack (tandai_terkirim eksplisit), hitung PENDING
benar, dan UID mentah tersimpan byte-persis (PRD §17). SQLite sementara
(:memory:) - tanpa Qt, tanpa server.
"""

from datetime import datetime

from app.rfid.base import JalurAlat
from app.ui.lobi.antrean_lokal import AntreanLokal


def _antrean() -> AntreanLokal:
    return AntreanLokal(":memory:")


def test_enqueue_tersimpan_sebagai_pending():
    antrean = _antrean()
    id_item = antrean.enqueue(
        uid_kartu="AABBCC", uid_mentah="AABBCC", waktu_scan=datetime(2026, 9, 10, 7, 10),
        jalur=JalurAlat.SIMULATOR, station_id="LOBI-1",
    )

    assert id_item is not None
    assert antrean.hitung_pending() == 1
    pending = antrean.daftar_pending()
    assert len(pending) == 1
    assert pending[0].uid_kartu == "AABBCC"
    assert pending[0].jalur == JalurAlat.SIMULATOR
    assert pending[0].station_id == "LOBI-1"


def test_uid_mentah_tersimpan_byte_persis_meski_kotor():
    """PRD §17: uid_mentah "kotor" (huruf kecil, separator, beda dari
    uid_kartu baku) harus tersimpan APA ADANYA - tidak dinormalisasi ulang
    di lapisan antrean."""
    antrean = _antrean()
    antrean.enqueue(
        uid_kartu="04A2B3C4", uid_mentah="04:a2 b3-c4", waktu_scan=datetime(2026, 9, 10, 7, 10),
        jalur=JalurAlat.SIMULATOR, station_id="LOBI-1",
    )

    pending = antrean.daftar_pending()
    assert pending[0].uid_kartu == "04A2B3C4"  # baku, dipakai lookup
    assert pending[0].uid_mentah == "04:a2 b3-c4"  # mentah, apa adanya


def test_tidak_menyimpan_pii_apa_pun():
    """Skema hanya punya kolom uid_kartu/uid_mentah/waktu_scan/jalur/
    station_id/status - tidak ada kolom nama/NIK di tabel sama sekali
    (jaminan struktural, bukan sekadar tidak diisi). UID (baku maupun
    mentah) bukan data pribadi - tidak mengidentifikasi ORANG, hanya
    kartu fisik."""
    antrean = _antrean()
    kolom = {baris[1] for baris in antrean._koneksi.execute("PRAGMA table_info(antrean_scan)").fetchall()}
    assert kolom == {"id", "uid_kartu", "uid_mentah", "waktu_scan", "jalur", "station_id", "status", "dibuat_pada"}


def test_fifo_by_waktu_scan_bukan_urutan_dimasukkan():
    antrean = _antrean()
    # Masukkan yang waktu_scan-nya LEBIH BARU dulu, lalu yang lebih lama -
    # daftar_pending() harus tetap mengembalikan yang lebih lama duluan.
    antrean.enqueue(
        uid_kartu="BARU", uid_mentah="BARU", waktu_scan=datetime(2026, 9, 10, 7, 15),
        jalur=JalurAlat.SIMULATOR, station_id="LOBI-1",
    )
    antrean.enqueue(
        uid_kartu="LAMA", uid_mentah="LAMA", waktu_scan=datetime(2026, 9, 10, 7, 10),
        jalur=JalurAlat.SIMULATOR, station_id="LOBI-1",
    )

    pending = antrean.daftar_pending()

    assert [p.uid_kartu for p in pending] == ["LAMA", "BARU"]


def test_tandai_terkirim_tidak_lagi_muncul_di_pending():
    antrean = _antrean()
    id_item = antrean.enqueue(
        uid_kartu="AABBCC", uid_mentah="AABBCC", waktu_scan=datetime(2026, 9, 10, 7, 10),
        jalur=JalurAlat.SIMULATOR, station_id="LOBI-1",
    )

    antrean.tandai_terkirim(id_item)

    assert antrean.hitung_pending() == 0
    assert antrean.daftar_pending() == []


def test_item_tetap_pending_sampai_eksplisit_ditandai_terkirim():
    """Tidak ada mekanisme lain yang menghapus/mengubah status item selain
    tandai_terkirim() - memanggil daftar_pending() berulang tidak mengubah
    apa pun (bukan "ambil lalu buang")."""
    antrean = _antrean()
    antrean.enqueue(
        uid_kartu="AABBCC", uid_mentah="AABBCC", waktu_scan=datetime(2026, 9, 10, 7, 10),
        jalur=JalurAlat.SIMULATOR, station_id="LOBI-1",
    )

    antrean.daftar_pending()
    antrean.daftar_pending()

    assert antrean.hitung_pending() == 1


def test_hitung_pending_menghitung_lebih_dari_satu_dan_mengecualikan_terkirim():
    antrean = _antrean()
    id1 = antrean.enqueue(
        uid_kartu="A", uid_mentah="A", waktu_scan=datetime(2026, 9, 10, 7, 10),
        jalur=JalurAlat.SIMULATOR, station_id="LOBI-1",
    )
    antrean.enqueue(
        uid_kartu="B", uid_mentah="B", waktu_scan=datetime(2026, 9, 10, 7, 11),
        jalur=JalurAlat.SIMULATOR, station_id="LOBI-1",
    )
    antrean.enqueue(
        uid_kartu="C", uid_mentah="C", waktu_scan=datetime(2026, 9, 10, 7, 12),
        jalur=JalurAlat.SIMULATOR, station_id="LOBI-1",
    )

    antrean.tandai_terkirim(id1)

    assert antrean.hitung_pending() == 2
    assert {p.uid_kartu for p in antrean.daftar_pending()} == {"B", "C"}


def test_daftar_pending_menghormati_limit():
    antrean = _antrean()
    for i in range(5):
        antrean.enqueue(
            uid_kartu=f"UID{i}", uid_mentah=f"UID{i}", waktu_scan=datetime(2026, 9, 10, 7, i),
            jalur=JalurAlat.SIMULATOR, station_id="LOBI-1",
        )

    assert len(antrean.daftar_pending(limit=2)) == 2
