from datetime import date, time

from app.config.pengaturan import Pengaturan
from app.config.seed import seed_jadwal_siswa, seed_pengaturan
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.schedule import ScheduleRepository
from app.data.repositories.setting import SettingRepository
from app.data.repositories.user import UserRepository


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def test_seed_jadwal_siswa_mengisi_senin_sampai_sabtu_dengan_jumat_beda(session):
    repo = ScheduleRepository(session)
    seed_jadwal_siswa(repo, berlaku_mulai=date(2026, 1, 1))

    daftar = repo.daftar(jenis_pemilik=JenisPemilik.SISWA)
    assert len(daftar) == 6
    assert {j.hari for j in daftar} == {1, 2, 3, 4, 5, 6}

    jumat = next(j for j in daftar if j.hari == 5)
    senin = next(j for j in daftar if j.hari == 1)
    # jam_pulang adalah AKHIR jendela pulang (sekaligus cutoff ALPA pulang) -
    # lihat SKILL.md "Jendela scan pulang": 13:20-15:00, Jumat 11:00-14:00.
    assert jumat.pulang_mulai == time(11, 0)
    assert jumat.jam_pulang == time(14, 0)
    assert senin.pulang_mulai == time(13, 20)
    assert senin.jam_pulang == time(15, 0)
    for j in daftar:
        assert j.jam_masuk == time(7, 15)
        assert j.batas_terlambat == time(7, 20, 59)
        assert j.cutoff_alpa == time(7, 30)


def test_seed_pengaturan_mengisi_parameter_waktu_global(session):
    id_admin = _buat_admin(session)
    pengaturan = Pengaturan(SettingRepository(session))
    seed_pengaturan(pengaturan, id_pengguna=id_admin, alasan="Seed uji")

    assert pengaturan.cutoff_alpa() == time(7, 30)
    assert pengaturan.waktu_kirim_notif_alpa() == time(9, 15)
    assert pengaturan.batas_terlambat_guru() == time(9, 30)
    assert pengaturan.ambang_tahan_konversi_pulang == 0.30
    assert pengaturan.jeda_kirim_alpa_pulang_menit == 60
