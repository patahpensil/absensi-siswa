from datetime import date, time

from app.data.models.core import StatusKartu
from app.data.models.enums import JenisPemilik
from app.data.repositories.rfid_card import RfidCardRepository
from app.data.repositories.schedule import ScheduleRepository


def test_cari_by_uid_menemukan_kartu_aktif(session):
    repo = RfidCardRepository(session)
    repo.terbitkan(
        uid_kartu="04A1B2C3",
        id_pemilik=1,
        jenis_pemilik=JenisPemilik.SISWA,
        tanggal_terbit=date(2026, 1, 1),
    )

    ditemukan = repo.cari_by_uid("04A1B2C3")
    assert ditemukan is not None
    assert ditemukan.status == StatusKartu.AKTIF

    assert repo.cari_by_uid("TIDAK_ADA") is None


def test_jadwal_berlaku_memilih_baris_sesuai_tanggal_bukan_baris_terbaru(session):
    repo = ScheduleRepository(session)
    repo.tambah(
        jenis_pemilik=JenisPemilik.SISWA,
        hari=1,
        jam_masuk=time(7, 15),
        batas_terlambat=time(7, 21),
        jam_pulang=time(13, 30),
        berlaku_mulai=date(2026, 1, 1),
        berlaku_sampai=date(2026, 6, 30),
    )
    repo.tambah(
        jenis_pemilik=JenisPemilik.SISWA,
        hari=1,
        jam_masuk=time(7, 0),
        batas_terlambat=time(7, 10),
        jam_pulang=time(13, 0),
        berlaku_mulai=date(2026, 7, 1),
        berlaku_sampai=None,
    )

    jadwal_lama = repo.jadwal_berlaku(jenis_pemilik=JenisPemilik.SISWA, hari=1, tanggal=date(2026, 3, 1))
    jadwal_baru = repo.jadwal_berlaku(jenis_pemilik=JenisPemilik.SISWA, hari=1, tanggal=date(2026, 8, 1))

    assert jadwal_lama.jam_masuk == time(7, 15)
    assert jadwal_baru.jam_masuk == time(7, 0)
