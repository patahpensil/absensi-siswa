"""Tes IzinDaruratAlatRepository (PRD §12.4) - penyimpanan izin darurat
alat dan jejak audit yang menyertainya (PRD §15.2) - dan
TelemetriLobiRepository (PRD §14.1, status operasional Dashboard)."""

from datetime import datetime, timedelta

from app.data.models.system import PeranPengguna
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.perangkat import IzinDaruratAlatRepository, TelemetriLobiRepository
from app.data.repositories.user import UserRepository

MULAI = datetime(2026, 9, 11, 8, 0, 0)


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def test_buat_izin_darurat_tersimpan_dan_teraudit(session):
    id_admin = _buat_admin(session)
    repo = IzinDaruratAlatRepository(session)

    izin = repo.buat(
        station_id="LOBI-3",
        id_pengguna_pemberi=id_admin,
        waktu_mulai=MULAI,
        waktu_berakhir=MULAI + timedelta(hours=24),
        alasan="Reader lobi 3 rusak, dipasang serial cadangan sementara",
    )

    assert izin.id is not None
    assert izin.dicabut is False

    log_terbaru = AuditLogRepository(session).daftar_terbaru(limit=1)[0]
    assert log_terbaru.aksi == "izin_darurat_alat_diberikan"
    assert log_terbaru.alasan.startswith("Reader lobi 3 rusak")


def test_cari_aktif_by_station_hanya_mengembalikan_yang_belum_kedaluwarsa(session):
    id_admin = _buat_admin(session)
    repo = IzinDaruratAlatRepository(session)
    repo.buat(
        station_id="LOBI-1",
        id_pengguna_pemberi=id_admin,
        waktu_mulai=MULAI,
        waktu_berakhir=MULAI + timedelta(hours=1),
        alasan="Uji",
    )

    aktif = repo.cari_aktif_by_station("LOBI-1", sekarang=MULAI + timedelta(minutes=30))
    assert aktif is not None

    kedaluwarsa = repo.cari_aktif_by_station("LOBI-1", sekarang=MULAI + timedelta(hours=2))
    assert kedaluwarsa is None

    lain = repo.cari_aktif_by_station("LOBI-2", sekarang=MULAI + timedelta(minutes=30))
    assert lain is None


def test_perpanjang_mengubah_waktu_berakhir_dan_teraudit(session):
    id_admin = _buat_admin(session)
    repo = IzinDaruratAlatRepository(session)
    izin = repo.buat(
        station_id="LOBI-1",
        id_pengguna_pemberi=id_admin,
        waktu_mulai=MULAI,
        waktu_berakhir=MULAI + timedelta(hours=1),
        alasan="Uji",
    )

    waktu_baru = MULAI + timedelta(hours=24)
    repo.perpanjang(izin, waktu_berakhir_baru=waktu_baru, id_pengguna=id_admin, alasan="Perpanjangan uji")

    assert izin.waktu_berakhir == waktu_baru
    # daftar_terbaru() hanya mengurutkan waktu.desc() tanpa tiebreak id -
    # dua audit log dalam tes yang sama bisa tercatat mikrodetik yang
    # sama persis (resolusi jam Windows), jadi cari lewat aksi, bukan [0].
    aksi = {log.aksi for log in AuditLogRepository(session).daftar_terbaru(limit=5)}
    assert "izin_darurat_alat_diperpanjang" in aksi


def test_cabut_menandai_dicabut_dan_tidak_lagi_aktif(session):
    id_admin = _buat_admin(session)
    repo = IzinDaruratAlatRepository(session)
    izin = repo.buat(
        station_id="LOBI-1",
        id_pengguna_pemberi=id_admin,
        waktu_mulai=MULAI,
        waktu_berakhir=MULAI + timedelta(hours=24),
        alasan="Uji",
    )

    repo.cabut(izin, id_pengguna=id_admin, sekarang=MULAI + timedelta(hours=1), alasan="Alat sudah diperbaiki")

    assert izin.dicabut is True
    assert izin.id_pengguna_pencabut == id_admin
    assert repo.cari_aktif_by_station("LOBI-1", sekarang=MULAI + timedelta(hours=2)) is None

    aksi = {log.aksi for log in AuditLogRepository(session).daftar_terbaru(limit=5)}
    assert "izin_darurat_alat_dicabut" in aksi


def test_daftar_aktif_mengecualikan_dicabut_dan_kedaluwarsa(session):
    id_admin = _buat_admin(session)
    repo = IzinDaruratAlatRepository(session)
    izin_aktif = repo.buat(
        station_id="LOBI-1",
        id_pengguna_pemberi=id_admin,
        waktu_mulai=MULAI,
        waktu_berakhir=MULAI + timedelta(hours=24),
        alasan="Uji aktif",
    )
    repo.buat(
        station_id="LOBI-2",
        id_pengguna_pemberi=id_admin,
        waktu_mulai=MULAI,
        waktu_berakhir=MULAI + timedelta(minutes=1),
        alasan="Uji kedaluwarsa",
    )
    izin_dicabut = repo.buat(
        station_id="LOBI-3",
        id_pengguna_pemberi=id_admin,
        waktu_mulai=MULAI,
        waktu_berakhir=MULAI + timedelta(hours=24),
        alasan="Uji dicabut",
    )
    repo.cabut(izin_dicabut, id_pengguna=id_admin, sekarang=MULAI + timedelta(minutes=5), alasan="Batal")

    aktif = repo.daftar_aktif(sekarang=MULAI + timedelta(hours=1))
    assert [i.id for i in aktif] == [izin_aktif.id]


# ---------------------------------------------------------------------------
# TelemetriLobiRepository (PRD §14.1)
# ---------------------------------------------------------------------------


def test_telemetri_lapor_pertama_kali_membuat_baris(session):
    id_admin = _buat_admin(session)
    repo = TelemetriLobiRepository(session)

    baris = repo.lapor(station_id="LOBI-1", jumlah_pending=3, id_pengguna=id_admin)

    assert baris.station_id == "LOBI-1"
    assert baris.jumlah_pending == 3
    assert repo.daftar() == [baris]


def test_telemetri_lapor_kedua_menimpa_bukan_menambah_baris(session):
    id_admin = _buat_admin(session)
    repo = TelemetriLobiRepository(session)
    repo.lapor(station_id="LOBI-1", jumlah_pending=3, id_pengguna=id_admin)

    repo.lapor(station_id="LOBI-1", jumlah_pending=0, id_pengguna=id_admin)

    daftar = repo.daftar()
    assert len(daftar) == 1
    assert daftar[0].jumlah_pending == 0


def test_telemetri_daftar_mengembalikan_seluruh_station(session):
    id_admin = _buat_admin(session)
    repo = TelemetriLobiRepository(session)
    repo.lapor(station_id="LOBI-1", jumlah_pending=1, id_pengguna=id_admin)
    repo.lapor(station_id="LOBI-2", jumlah_pending=2, id_pengguna=id_admin)

    daftar = repo.daftar()

    assert {b.station_id for b in daftar} == {"LOBI-1", "LOBI-2"}
