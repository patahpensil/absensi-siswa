"""Tes integrasi endpoint Laporan (PRD §14.2, §9.3): rekap/detail,
ekspor Excel/PDF berkop, arsip PDF otomatis, Koreksi Setelah Laporan
Terbit, lingkup peran (kepsek/admin bebas, wali kelas kelasnya sendiri,
operator DITOLAK), dan NIK tidak pernah muncul di respons. Tidak
menyentuh app.core.engine/mesin_absensi.
"""

from datetime import date

from fastapi.testclient import TestClient

from app.data.models.core import JenisKelamin
from app.data.models.enums import JenisPemilik
from app.data.models.system import PeranPengguna
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.koreksi_satuan import KoreksiSatuanRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.user import UserRepository
from app.server.app import buat_app
from app.server.security import hash_password

TANGGAL_MULAI = "2026-09-01"
TANGGAL_SELESAI = "2026-09-05"


def _client(session_factory, tmp_path) -> TestClient:
    return TestClient(buat_app(session_factory, laporan_dir=tmp_path))


def _buat_user(session, *, username: str, password: str, peran: PeranPengguna, **kwargs) -> int:
    return UserRepository(session).tambah(
        username=username, password_hash=hash_password(password), peran=peran, **kwargs
    ).id


def _login(client: TestClient, *, username: str, password: str) -> str:
    resp = client.post("/auth/login", json={"username": username, "password": password})
    assert resp.status_code == 200, resp.text
    return resp.json()["token"]


def _header(token: str) -> dict:
    return {"Authorization": f"Bearer {token}"}


def _token_admin(client: TestClient, session) -> str:
    _buat_user(session, username="admin", password="rahasia123", peran=PeranPengguna.ADMIN)
    return _login(client, username="admin", password="rahasia123")


def _buat_siswa(session, *, id_kelas: int | None, nisn: str, nama: str, nik: str | None = None) -> int:
    return StudentRepository(session).tambah(
        nisn=nisn, nama_lengkap=nama, id_kelas=id_kelas, jenis_kelamin=JenisKelamin.L,
        tempat_lahir="Pancarijang", tanggal_lahir=date(2012, 1, 1), alamat="Jl. Contoh No. 1", nik=nik,
    ).id


def _siapkan_dua_kelas(session, *, nik_a: str | None = None) -> dict:
    kelas_a = KelasRepository(session).tambah(tingkat=7, nama_kelas="7A", tahun_ajaran="2026/2027").id
    kelas_b = KelasRepository(session).tambah(tingkat=7, nama_kelas="7B", tahun_ajaran="2026/2027").id
    siswa_a = _buat_siswa(session, id_kelas=kelas_a, nisn="1000000001", nama="Budi Santoso", nik=nik_a)
    siswa_b = _buat_siswa(session, id_kelas=kelas_b, nisn="1000000002", nama="Siti Aminah")
    return {"kelas_a": kelas_a, "kelas_b": kelas_b, "siswa_a": siswa_a, "siswa_b": siswa_b}


def _isi_absensi(session, *, id_siswa: int, tanggal: date, status: str) -> int:
    return AttendanceRepository(session).tambah(
        id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=tanggal, status_masuk=status
    ).id


def _params(*, id_kelas: int | None = None, id_siswa: int | None = None) -> dict:
    params = {"tanggal_mulai": TANGGAL_MULAI, "tanggal_selesai": TANGGAL_SELESAI}
    if id_kelas is not None:
        params["id_kelas"] = id_kelas
    if id_siswa is not None:
        params["id_siswa"] = id_siswa
    return params


# ---------------------------------------------------------------------------
# Lingkup peran (PRD §10) - operator TIDAK berwenang sama sekali
# ---------------------------------------------------------------------------


def test_operator_ditolak_403(session_factory, session, tmp_path):
    _buat_user(session, username="operator", password="rahasia123", peran=PeranPengguna.OPERATOR)
    client = _client(session_factory, tmp_path)
    token = _login(client, username="operator", password="rahasia123")

    resp = client.get("/laporan", params=_params(), headers=_header(token))
    assert resp.status_code == 403


def test_wali_kelas_lihat_kelas_lain_ditolak_403(session_factory, session, tmp_path):
    data = _siapkan_dua_kelas(session)
    _buat_user(
        session, username="wali", password="rahasia123", peran=PeranPengguna.WALI_KELAS,
        id_kelas_diampu=data["kelas_a"],
    )
    client = _client(session_factory, tmp_path)
    token = _login(client, username="wali", password="rahasia123")

    resp = client.get("/laporan", params=_params(id_kelas=data["kelas_b"]), headers=_header(token))
    assert resp.status_code == 403


def test_wali_kelas_tanpa_saring_dibatasi_ke_kelasnya_sendiri(session_factory, session, tmp_path):
    data = _siapkan_dua_kelas(session)
    _isi_absensi(session, id_siswa=data["siswa_a"], tanggal=date(2026, 9, 1), status="HADIR")
    _isi_absensi(session, id_siswa=data["siswa_b"], tanggal=date(2026, 9, 1), status="HADIR")
    _buat_user(
        session, username="wali", password="rahasia123", peran=PeranPengguna.WALI_KELAS,
        id_kelas_diampu=data["kelas_a"],
    )
    client = _client(session_factory, tmp_path)
    token = _login(client, username="wali", password="rahasia123")

    resp = client.get("/laporan", params=_params(), headers=_header(token))
    assert resp.status_code == 200, resp.text
    hasil = resp.json()
    assert hasil["id_kelas"] == data["kelas_a"]
    assert [r["id_siswa"] for r in hasil["rekap"]] == [data["siswa_a"]]


# ---------------------------------------------------------------------------
# Agregasi
# ---------------------------------------------------------------------------


def test_rekap_tally_per_status(session_factory, session, tmp_path):
    data = _siapkan_dua_kelas(session)
    _isi_absensi(session, id_siswa=data["siswa_a"], tanggal=date(2026, 9, 1), status="HADIR")
    _isi_absensi(session, id_siswa=data["siswa_a"], tanggal=date(2026, 9, 2), status="TERLAMBAT")
    _isi_absensi(session, id_siswa=data["siswa_a"], tanggal=date(2026, 9, 3), status="ALPA")

    client = _client(session_factory, tmp_path)
    token = _token_admin(client, session)

    resp = client.get("/laporan", params=_params(id_kelas=data["kelas_a"]), headers=_header(token))
    assert resp.status_code == 200, resp.text
    baris = resp.json()["rekap"][0]
    assert baris["jumlah"]["HADIR"] == 1
    assert baris["jumlah"]["TERLAMBAT"] == 1
    assert baris["jumlah"]["ALPA"] == 1
    assert baris["jumlah_hari_tercatat"] == 3


def test_detail_per_siswa_log_harian(session_factory, session, tmp_path):
    data = _siapkan_dua_kelas(session)
    _isi_absensi(session, id_siswa=data["siswa_a"], tanggal=date(2026, 9, 1), status="HADIR")
    _isi_absensi(session, id_siswa=data["siswa_a"], tanggal=date(2026, 9, 2), status="ALPA")

    client = _client(session_factory, tmp_path)
    token = _token_admin(client, session)

    resp = client.get("/laporan", params=_params(id_siswa=data["siswa_a"]), headers=_header(token))
    assert resp.status_code == 200, resp.text
    hasil = resp.json()
    assert hasil["rekap"] is None
    assert hasil["detail"]["nama_lengkap"] == "Budi Santoso"
    assert [b["status_final"] for b in hasil["detail"]["baris"]] == ["HADIR", "ALPA"]


def test_siswa_tidak_ditemukan_404(session_factory, session, tmp_path):
    client = _client(session_factory, tmp_path)
    token = _token_admin(client, session)
    resp = client.get("/laporan", params=_params(id_siswa=999999), headers=_header(token))
    assert resp.status_code == 404


# ---------------------------------------------------------------------------
# NIK tidak pernah muncul (Aturan Global #6, PRD §11.1)
# ---------------------------------------------------------------------------


def test_nik_tidak_pernah_muncul_di_respons(session_factory, session, tmp_path):
    nik_rahasia = "9876543210123456"
    data = _siapkan_dua_kelas(session, nik_a=nik_rahasia)
    _isi_absensi(session, id_siswa=data["siswa_a"], tanggal=date(2026, 9, 1), status="HADIR")

    client = _client(session_factory, tmp_path)
    token = _token_admin(client, session)

    resp_rekap = client.get("/laporan", params=_params(id_kelas=data["kelas_a"]), headers=_header(token))
    assert nik_rahasia not in resp_rekap.text

    resp_detail = client.get("/laporan", params=_params(id_siswa=data["siswa_a"]), headers=_header(token))
    assert nik_rahasia not in resp_detail.text


# ---------------------------------------------------------------------------
# Ekspor + arsip otomatis (PRD §9.3)
# ---------------------------------------------------------------------------


def test_ekspor_excel_mengembalikan_xlsx_tanpa_mengarsipkan(session_factory, session, tmp_path):
    data = _siapkan_dua_kelas(session)
    _isi_absensi(session, id_siswa=data["siswa_a"], tanggal=date(2026, 9, 1), status="HADIR")

    client = _client(session_factory, tmp_path)
    token = _token_admin(client, session)

    resp = client.get("/laporan/ekspor.xlsx", params=_params(id_kelas=data["kelas_a"]), headers=_header(token))
    assert resp.status_code == 200
    assert resp.content[:2] == b"PK"  # tanda-tangan berkas zip/xlsx

    arsip = client.get("/laporan/arsip", headers=_header(token))
    assert arsip.json() == []  # Excel TIDAK diarsipkan


def test_ekspor_pdf_mengarsipkan_otomatis_dengan_cap_waktu(session_factory, session, tmp_path):
    data = _siapkan_dua_kelas(session)
    _isi_absensi(session, id_siswa=data["siswa_a"], tanggal=date(2026, 9, 1), status="HADIR")

    client = _client(session_factory, tmp_path)
    token = _token_admin(client, session)

    resp = client.get("/laporan/ekspor.pdf", params=_params(id_kelas=data["kelas_a"]), headers=_header(token))
    assert resp.status_code == 200
    assert resp.content[:5] == b"%PDF-"

    arsip = client.get("/laporan/arsip", headers=_header(token))
    assert arsip.status_code == 200
    daftar = arsip.json()
    assert len(daftar) == 1
    assert daftar[0]["id_kelas"] == data["kelas_a"]
    assert daftar[0]["waktu_cetak"] is not None

    # Berkas fisik benar-benar tersimpan di laporan_dir (tmp_path).
    unduh = client.get(f"/laporan/arsip/{daftar[0]['id']}/unduh", headers=_header(token))
    assert unduh.status_code == 200
    assert unduh.content[:5] == b"%PDF-"


def test_wali_kelas_tidak_bisa_lihat_arsip_kelas_lain(session_factory, session, tmp_path):
    data = _siapkan_dua_kelas(session)
    _isi_absensi(session, id_siswa=data["siswa_b"], tanggal=date(2026, 9, 1), status="HADIR")

    client = _client(session_factory, tmp_path)
    token_admin = _token_admin(client, session)
    resp = client.get("/laporan/ekspor.pdf", params=_params(id_kelas=data["kelas_b"]), headers=_header(token_admin))
    assert resp.status_code == 200
    # Ambil id arsip lewat admin dulu (wali kelas 7A tidak boleh melihatnya).
    arsip_admin = client.get("/laporan/arsip", headers=_header(token_admin)).json()
    id_arsip = arsip_admin[0]["id"]

    _buat_user(
        session, username="wali_a", password="rahasia123", peran=PeranPengguna.WALI_KELAS,
        id_kelas_diampu=data["kelas_a"],
    )
    token_wali = _login(client, username="wali_a", password="rahasia123")

    daftar_wali = client.get("/laporan/arsip", headers=_header(token_wali))
    assert daftar_wali.json() == []

    unduh = client.get(f"/laporan/arsip/{id_arsip}/unduh", headers=_header(token_wali))
    assert unduh.status_code == 404  # tidak muncul di daftarnya - dianggap tidak ada, bukan 403 yang membocorkan ID valid

    koreksi_setelah = client.get(f"/laporan/arsip/{id_arsip}/koreksi-setelah-terbit", headers=_header(token_wali))
    assert koreksi_setelah.status_code == 403


# ---------------------------------------------------------------------------
# Koreksi Setelah Laporan Terbit (PRD §9.3)
# ---------------------------------------------------------------------------


def test_koreksi_sebelum_terbit_tidak_muncul_koreksi_setelah_terbit_muncul(session_factory, session, tmp_path):
    data = _siapkan_dua_kelas(session)
    id_absensi_lama = _isi_absensi(session, id_siswa=data["siswa_a"], tanggal=date(2026, 9, 1), status="ALPA")

    client = _client(session_factory, tmp_path)
    token = _token_admin(client, session)
    id_admin_session = UserRepository(session).cari_by_username("admin").id

    # Koreksi TERJADI SEBELUM laporan dicetak - tidak boleh muncul.
    KoreksiSatuanRepository(session).catat(
        id_absensi=id_absensi_lama, id_pengguna=id_admin_session, nilai_sebelum={"status_final": "ALPA"},
        nilai_sesudah={"status_final": "SAKIT"}, alasan="Sebelum laporan terbit",
    )
    KoreksiSatuanRepository(session).commit()

    resp_pdf = client.get("/laporan/ekspor.pdf", params=_params(id_kelas=data["kelas_a"]), headers=_header(token))
    assert resp_pdf.status_code == 200
    id_arsip = client.get("/laporan/arsip", headers=_header(token)).json()[0]["id"]

    koreksi_awal = client.get(f"/laporan/arsip/{id_arsip}/koreksi-setelah-terbit", headers=_header(token))
    assert koreksi_awal.json() == []

    # Koreksi BARU, SETELAH laporan dicetak - harus muncul.
    id_absensi_baru = _isi_absensi(session, id_siswa=data["siswa_a"], tanggal=date(2026, 9, 2), status="ALPA")
    KoreksiSatuanRepository(session).catat(
        id_absensi=id_absensi_baru, id_pengguna=id_admin_session, nilai_sebelum={"status_final": "ALPA"},
        nilai_sesudah={"status_final": "IZIN"}, alasan="Surat dokter menyusul",
    )
    KoreksiSatuanRepository(session).commit()

    koreksi_setelah = client.get(f"/laporan/arsip/{id_arsip}/koreksi-setelah-terbit", headers=_header(token))
    assert koreksi_setelah.status_code == 200
    daftar = koreksi_setelah.json()
    assert len(daftar) == 1
    assert daftar[0]["sumber"] == "SATUAN"
    assert daftar[0]["nama_siswa"] == "Budi Santoso"
    assert daftar[0]["nilai_sesudah"] == {"status_final": "IZIN"}


def test_arsip_tidak_ditemukan_404(session_factory, session, tmp_path):
    client = _client(session_factory, tmp_path)
    token = _token_admin(client, session)
    resp = client.get("/laporan/arsip/999999/koreksi-setelah-terbit", headers=_header(token))
    assert resp.status_code == 404
