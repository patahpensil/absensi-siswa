"""Membuktikan aturan inti app/config/:

- default dipakai kalau kunci belum ada di settings
- nilai dari DB menang atas default
- perubahan tercatat di audit log
- alasan tetap wajib di lapisan ini (jalur yang sama dengan koreksi absensi)

Tidak menyentuh logika status atau cutoff - ini murni akses konfigurasi.
"""

from datetime import time

import pytest

from app.config.keys import KunciPengaturan
from app.config.pengaturan import IdentitasSekolah, Pengaturan
from app.data.models.system import PeranPengguna
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.setting import SettingRepository
from app.data.repositories.user import UserRepository


def _buat_admin(session) -> int:
    return UserRepository(session).tambah(username="admin_uji", password_hash="x", peran=PeranPengguna.ADMIN).id


def test_default_terpakai_saat_kunci_kosong(session):
    pengaturan = Pengaturan(SettingRepository(session))

    # Jam sekolah: belum di-seed, jadi None - placeholder jelas, bukan
    # angka yang kelihatan benar padahal cuma tebakan.
    assert pengaturan.jam_masuk() is None
    assert pengaturan.batas_terlambat() is None
    assert pengaturan.cutoff_alpa() is None
    assert pengaturan.waktu_kirim_notif_alpa() is None
    assert pengaturan.jam_pulang() is None

    # Parameter yang sudah punya kesepakatan bawaan tetap punya default nyata.
    assert pengaturan.jendela_scan_berulang_detik() == 10.0
    assert pengaturan.modul_whatsapp_aktif() is False
    assert pengaturan.batas_percobaan_notifikasi() == 3
    assert pengaturan.mode_uji_whatsapp_aktif() is False
    assert pengaturan.nomor_admin_mode_uji() is None


def test_nilai_dari_db_menang_atas_default(session):
    id_admin = _buat_admin(session)
    pengaturan = Pengaturan(SettingRepository(session))

    pengaturan.atur_jam_masuk(time(7, 15), id_pengguna=id_admin, alasan="Seed jadwal awal dari sekolah")
    pengaturan.atur_jendela_scan_berulang_detik(15.0, id_pengguna=id_admin, alasan="Uji override jendela scan")
    pengaturan.atur_modul_whatsapp_aktif(True, id_pengguna=id_admin, alasan="Uji aktifkan modul WhatsApp")
    pengaturan.atur_batas_percobaan_notifikasi(5, id_pengguna=id_admin, alasan="Uji override batas percobaan")
    pengaturan.atur_mode_uji_whatsapp_aktif(True, id_pengguna=id_admin, alasan="Uji aktifkan mode uji")
    pengaturan.atur_nomor_admin_mode_uji("081234567890", id_pengguna=id_admin, alasan="Uji isi nomor admin")

    assert pengaturan.jam_masuk() == time(7, 15)
    assert pengaturan.jendela_scan_berulang_detik() == 15.0
    assert pengaturan.modul_whatsapp_aktif() is True
    assert pengaturan.batas_percobaan_notifikasi() == 5
    assert pengaturan.mode_uji_whatsapp_aktif() is True
    # Dirapikan ke format baku (+62...) lewat app.core.validasi_data_master,
    # bukan disimpan apa adanya.
    assert pengaturan.nomor_admin_mode_uji() == "+6281234567890"

    # Kunci lain yang belum diatur tetap pakai default masing-masing.
    assert pengaturan.jam_pulang() is None
    assert pengaturan.batas_terlambat() is None


def test_perubahan_pengaturan_tercatat_di_audit_log(session):
    id_admin = _buat_admin(session)
    pengaturan = Pengaturan(SettingRepository(session))
    audit_repo = AuditLogRepository(session)

    pengaturan.atur_batas_terlambat(
        time(7, 21), id_pengguna=id_admin, alasan="Seed batas terlambat sesuai kesepakatan sekolah"
    )

    entri = audit_repo.daftar_terbaru(limit=1)[0]
    assert entri.aksi == "ubah_pengaturan"
    assert entri.nilai_sebelum == {"kunci": KunciPengaturan.BATAS_TERLAMBAT, "nilai": None}
    assert entri.nilai_sesudah == {"kunci": KunciPengaturan.BATAS_TERLAMBAT, "nilai": "07:21:00"}
    assert entri.alasan.startswith("Seed batas terlambat")


def test_atur_tanpa_alasan_ditolak_dan_nilai_tidak_berubah(session):
    id_admin = _buat_admin(session)
    pengaturan = Pengaturan(SettingRepository(session))

    with pytest.raises(ValueError):
        pengaturan.atur_jam_pulang(time(13, 30), id_pengguna=id_admin, alasan="")

    assert pengaturan.jam_pulang() is None


def test_identitas_sekolah_kosong_sebelum_diisi(session):
    pengaturan = Pengaturan(SettingRepository(session))

    identitas = pengaturan.identitas_sekolah()

    assert identitas == IdentitasSekolah()


def test_atur_identitas_sekolah_lalu_dibaca_kembali(session):
    id_admin = _buat_admin(session)
    pengaturan = Pengaturan(SettingRepository(session))

    pengaturan.atur_identitas_sekolah(
        IdentitasSekolah(
            nama="UPT SMP Negeri 1 Pancarijang",
            npsn="12345678",
            alamat="Jl. Pendidikan No. 1, Pancarijang",
            nama_kepala_sekolah="Rahmat Perdana, S.Kom.",
            nip_kepala_sekolah="19830121 200901 1 007",
        ),
        id_pengguna=id_admin,
        alasan="Seed identitas sekolah",
    )

    identitas = pengaturan.identitas_sekolah()
    assert identitas.nama == "UPT SMP Negeri 1 Pancarijang"
    assert identitas.npsn == "12345678"
    assert identitas.nama_kepala_sekolah == "Rahmat Perdana, S.Kom."
    assert identitas.logo is None  # tidak ikut diisi, tidak ikut ditulis


def test_atur_identitas_sekolah_field_none_tidak_menghapus_nilai_lama(session):
    id_admin = _buat_admin(session)
    pengaturan = Pengaturan(SettingRepository(session))
    pengaturan.atur_identitas_sekolah(
        IdentitasSekolah(nama="UPT SMP Negeri 1 Pancarijang"), id_pengguna=id_admin, alasan="Isi nama dulu"
    )

    pengaturan.atur_identitas_sekolah(
        IdentitasSekolah(npsn="12345678"), id_pengguna=id_admin, alasan="Tambah NPSN belakangan"
    )

    identitas = pengaturan.identitas_sekolah()
    assert identitas.nama == "UPT SMP Negeri 1 Pancarijang"  # tidak hilang
    assert identitas.npsn == "12345678"
