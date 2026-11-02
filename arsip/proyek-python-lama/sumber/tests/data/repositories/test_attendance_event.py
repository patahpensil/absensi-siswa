"""Membuktikan attendance_events immutable di lapisan repository.

Event scan mentah tidak pernah diubah atau dihapus, bahkan ketika hasil
pengolahannya di tabel attendance berubah - lihat skill proyek. Tes ini
memeriksa dua hal: repository-nya sendiri tidak menyediakan method update
maupun delete, dan baris lama benar-benar tidak berubah walau ada baris
baru untuk UID yang sama.
"""

import inspect
from datetime import datetime

from app.data.models.attendance import JenisEventScan
from app.data.models.enums import JenisPemilik
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.rfid.base import JalurAlat


def test_buat_dan_baca_peristiwa(session):
    repo = AttendanceEventRepository(session)
    peristiwa = repo.buat(uid_kartu="AABBCC", jenis_event=JenisEventScan.MASUK)

    assert peristiwa.id is not None
    dibaca = repo.cari_by_id(peristiwa.id)
    assert dibaca is not None
    assert dibaca.uid_kartu == "AABBCC"
    assert dibaca.jenis_event == JenisEventScan.MASUK


def test_repository_tidak_punya_method_update_atau_delete():
    method_publik = {
        nama
        for nama, _ in inspect.getmembers(AttendanceEventRepository, predicate=inspect.isfunction)
        if not nama.startswith("_")
    }

    kata_terlarang = ("update", "ubah", "perbarui", "edit", "delete", "hapus", "remove", "set_")
    method_mencurigakan = [
        nama for nama in method_publik if any(kata in nama.lower() for kata in kata_terlarang)
    ]
    assert method_mencurigakan == []

    # Daftar putih eksplisit: hanya create dan read yang boleh ada.
    assert method_publik == {
        "buat", "cari_by_id", "cari", "daftar_by_uid", "daftar_terbaru", "waktu_scan_terakhir",
        "waktu_terakhir_per_jalur",
    }


def test_baris_lama_tidak_berubah_walau_ada_baris_baru_untuk_uid_sama(session):
    repo = AttendanceEventRepository(session)
    pertama = repo.buat(uid_kartu="AABBCC", jenis_event=JenisEventScan.MASUK)
    waktu_pertama = pertama.waktu_scan
    id_pertama = pertama.id

    repo.buat(uid_kartu="AABBCC", jenis_event=JenisEventScan.DUPLICATE)

    dibaca_ulang = repo.cari_by_id(id_pertama)
    assert dibaca_ulang.waktu_scan == waktu_pertama
    assert dibaca_ulang.jenis_event == JenisEventScan.MASUK

    semua_untuk_uid = repo.daftar_by_uid("AABBCC")
    assert len(semua_untuk_uid) == 2


def test_waktu_terakhir_per_jalur_mengambil_yang_paling_baru(session):
    repo = AttendanceEventRepository(session)
    repo.buat(
        uid_kartu="A", jenis_event=JenisEventScan.MASUK, jalur=JalurAlat.PCSC,
        waktu_scan=datetime(2026, 9, 11, 7, 0),
    )
    repo.buat(
        uid_kartu="B", jenis_event=JenisEventScan.MASUK, jalur=JalurAlat.PCSC,
        waktu_scan=datetime(2026, 9, 11, 9, 0),
    )
    repo.buat(
        uid_kartu="C", jenis_event=JenisEventScan.MASUK, jalur=JalurAlat.SERIAL,
        waktu_scan=datetime(2026, 9, 11, 8, 0),
    )
    repo.buat(uid_kartu="D", jenis_event=JenisEventScan.MASUK)  # jalur=None, diabaikan

    peta = repo.waktu_terakhir_per_jalur()

    assert peta == {
        JalurAlat.PCSC: datetime(2026, 9, 11, 9, 0),
        JalurAlat.SERIAL: datetime(2026, 9, 11, 8, 0),
    }


def test_cari_menyaring_rentang_waktu(session):
    repo = AttendanceEventRepository(session)
    repo.buat(uid_kartu="A", jenis_event=JenisEventScan.MASUK, waktu_scan=datetime(2026, 9, 1, 7, 0))
    repo.buat(uid_kartu="B", jenis_event=JenisEventScan.MASUK, waktu_scan=datetime(2026, 9, 5, 7, 0))
    repo.buat(uid_kartu="C", jenis_event=JenisEventScan.MASUK, waktu_scan=datetime(2026, 9, 10, 7, 0))

    hasil = repo.cari(sejak=datetime(2026, 9, 2), sampai=datetime(2026, 9, 9))

    assert [e.uid_kartu for e in hasil] == ["B"]


def test_cari_menyaring_id_pemilik_dan_jenis_pemilik(session):
    repo = AttendanceEventRepository(session)
    repo.buat(uid_kartu="A", jenis_event=JenisEventScan.MASUK, id_pemilik=1, jenis_pemilik=JenisPemilik.SISWA)
    repo.buat(uid_kartu="B", jenis_event=JenisEventScan.MASUK, id_pemilik=1, jenis_pemilik=JenisPemilik.GURU)
    repo.buat(uid_kartu="C", jenis_event=JenisEventScan.MASUK, id_pemilik=2, jenis_pemilik=JenisPemilik.SISWA)

    hasil = repo.cari(id_pemilik=1, jenis_pemilik=JenisPemilik.SISWA)

    assert [e.uid_kartu for e in hasil] == ["A"]


def test_cari_menyaring_ids_pemilik_siswa_untuk_lingkup_kelas(session):
    repo = AttendanceEventRepository(session)
    repo.buat(uid_kartu="A", jenis_event=JenisEventScan.MASUK, id_pemilik=1, jenis_pemilik=JenisPemilik.SISWA)
    repo.buat(uid_kartu="B", jenis_event=JenisEventScan.MASUK, id_pemilik=2, jenis_pemilik=JenisPemilik.SISWA)
    repo.buat(uid_kartu="C", jenis_event=JenisEventScan.MASUK, id_pemilik=1, jenis_pemilik=JenisPemilik.GURU)

    hasil = repo.cari(ids_pemilik_siswa=[1])

    # Guru dengan id_pemilik sama (1) TIDAK ikut - ids_pemilik_siswa
    # selalu memaksa jenis_pemilik SISWA.
    assert [e.uid_kartu for e in hasil] == ["A"]


def test_uid_mentah_bawaan_none_dan_bisa_diisi_apa_adanya(session):
    """uid_mentah opsional (bawaan None) - pemanggil lama seperti
    test_buat_dan_baca_peristiwa di atas tetap jalan tanpa perubahan.
    Saat diisi, disimpan apa adanya (PRD §17) - tidak ada normalisasi di
    lapisan repository."""
    repo = AttendanceEventRepository(session)

    tanpa_mentah = repo.buat(uid_kartu="AABBCC", jenis_event=JenisEventScan.MASUK)
    assert tanpa_mentah.uid_mentah is None

    dengan_mentah = repo.buat(uid_kartu="DDEEFF", jenis_event=JenisEventScan.MASUK, uid_mentah="dd:ee-ff")
    assert dengan_mentah.uid_mentah == "dd:ee-ff"


def test_jalur_bawaan_none_dan_bisa_diisi(session):
    """jalur opsional (bawaan None, PRD §17) - pemanggil lama seperti
    test_buat_dan_baca_peristiwa di atas tetap jalan tanpa perubahan."""
    repo = AttendanceEventRepository(session)

    tanpa_jalur = repo.buat(uid_kartu="AABBCC", jenis_event=JenisEventScan.MASUK)
    assert tanpa_jalur.jalur is None

    dengan_jalur = repo.buat(uid_kartu="DDEEFF", jenis_event=JenisEventScan.MASUK, jalur=JalurAlat.PCSC)
    assert dengan_jalur.jalur == JalurAlat.PCSC
