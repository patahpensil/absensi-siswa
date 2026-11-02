"""Skema request/response Pydantic untuk app/server/. Terpisah dari model
SQLAlchemy (app.data.models) dengan sengaja - skema HTTP tidak harus
persis sama bentuknya dengan baris database, dan tidak boleh membocorkan
kolom yang tidak semestinya (mis. password_hash).
"""

from __future__ import annotations

from datetime import date, datetime, time

from pydantic import BaseModel, Field, field_validator

from app.core.validasi_data_master import (
    normalisasi_nomor_whatsapp,
    samarkan_nik,
    validasi_nik,
    validasi_nisn,
)


class LoginRequest(BaseModel):
    username: str
    password: str


class LoginResponse(BaseModel):
    token: str
    peran: str
    wajib_ganti_password: bool
    kadaluarsa: datetime


class GantiPasswordRequest(BaseModel):
    password_lama: str
    password_baru: str = Field(min_length=8)


class ScanRequest(BaseModel):
    #: UID ternormalisasi (baku) - dipakai untuk pencarian kartu
    #: (RfidCardRepository.cari_by_uid). Normalisasi tetap wewenang klien
    #: (app.rfid.base.normalisasi_uid, PRD §12.3) - server TIDAK PERNAH
    #: menormalisasi ulang apa pun, hanya menerima dan mencocokkan.
    uid_kartu: str
    #: UID apa adanya dari alat, SEBELUM dinormalisasi - disimpan utuh ke
    #: attendance_events (PRD §17: "peristiwa_scan ... UID mentah, UID
    #: baku ..."). Wajib di kontrak HTTP karena setiap klien nyata selalu
    #: punya kedua nilai ini dari driver pembacanya (app.rfid.base.ScanEvent).
    uid_mentah: str
    waktu_scan: datetime
    #: Jalur alat pembaca (PCSC/SERIAL/HID/SIMULATOR) - disimpan ke
    #: attendance_events (PRD §17: "peristiwa_scan ... jalur ..."). Wajib
    #: di kontrak HTTP - setiap klien nyata selalu punya nilai ini dari
    #: ScanEvent.jalur (app.rfid.base), diteruskan router lewat
    #: JalurAlat(payload.jalur) (422 kalau nilainya tidak dikenal).
    jalur: str
    #: Identitas jalur/perangkat pengirim, murni informasional untuk
    #: sekarang - lihat catatan di app.server.routers.scan.
    station_id: str | None = None


class ScanResponse(BaseModel):
    jenis_event: str
    uid_kartu: str
    id_absensi: int | None
    status_masuk: str | None
    status_final: str | None
    jam_masuk: datetime | None
    jam_pulang: datetime | None
    # Identitas tampilan untuk layar absensi (app/ui/lobi) - PRD §12.5 mewajibkan
    # nama tampil di layar, dan klien tidak boleh membaca app/data langsung
    # (SKILL.md, prinsip arsitektur) - response ini satu-satunya sumber sah.
    # Tidak ada field foto: sumber foto siswa belum ditetapkan (SKILL.md "Hal
    # yang belum diputuskan").
    id_pemilik: int | None = None
    jenis_pemilik: str | None = None
    nama: str | None = None


class PenyaringSchema(BaseModel):
    tanggal_mulai: date
    tanggal_selesai: date
    lingkup: str
    tingkat: int | None = None
    id_kelas: int | None = None
    id_siswa_terpilih: list[int] = Field(default_factory=list)
    #: Kosong/None berarti bawaan - semua kecuali status manual. Lihat
    #: app.core.koreksi_massal.PenyaringKoreksiMassal.status_efektif().
    status_termasuk: list[str] | None = None


class PratinjauResponse(BaseModel):
    jumlah_baris: int
    perlu_snapshot: bool
    perlu_konfirmasi_ketik: bool


class JalankanKoreksiMassalRequest(BaseModel):
    penyaring: PenyaringSchema
    jenis_tindakan: str
    status_baru: str | None = None
    alasan: str = Field(min_length=1)
    snapshot_sudah_diambil: bool = False
    konfirmasi_ketik: str | None = None


class BatalkanKoreksiMassalRequest(BaseModel):
    alasan: str = Field(min_length=1)


class TindakanMassalResponse(BaseModel):
    id: int
    id_pengguna: int
    jenis_tindakan: str
    status_baru: str | None
    penyaring: dict
    jumlah_baris: int
    alasan: str
    dibatalkan: bool
    waktu: datetime
    waktu_dibatalkan: datetime | None
    dibatalkan_oleh: int | None


# ---------------------------------------------------------------------------
# Data master - siswa, guru, kelas, kartu RFID (PRD §11, §12)
# ---------------------------------------------------------------------------


class _SiswaFieldsBersama(BaseModel):
    """Field yang sama untuk buat dan ubah siswa - TIDAK termasuk nisn,
    yang merupakan kunci utama dan tidak pernah diubah setelah dibuat."""

    nama_lengkap: str = Field(min_length=1)
    jenis_kelamin: str
    tempat_lahir: str = Field(min_length=1)
    tanggal_lahir: date
    alamat: str = Field(min_length=1)
    id_kelas: int | None = None
    nipd: str | None = None
    nik: str | None = None
    #: PRD §11.1: nama salah satu orang tua wajib.
    nama_ortu: str = Field(min_length=1)
    #: PRD §11.1: nomor telepon/WA boleh dikosongkan, dirapikan otomatis.
    nomor_whatsapp_ortu: str | None = None

    @field_validator("jenis_kelamin")
    @classmethod
    def _jenis_kelamin_valid(cls, v: str) -> str:
        if v not in ("L", "P"):
            raise ValueError("Jenis kelamin harus L atau P")
        return v

    @field_validator("nik")
    @classmethod
    def _nik_valid(cls, v: str | None) -> str | None:
        if v is not None:
            validasi_nik(v)
        return v

    @field_validator("nomor_whatsapp_ortu")
    @classmethod
    def _nomor_whatsapp_dirapikan(cls, v: str | None) -> str | None:
        if v is None or v.strip() == "":
            return None
        return normalisasi_nomor_whatsapp(v)


class SiswaBuatRequest(_SiswaFieldsBersama):
    nisn: str

    @field_validator("nisn")
    @classmethod
    def _nisn_valid(cls, v: str) -> str:
        validasi_nisn(v)
        return v


class SiswaUbahRequest(_SiswaFieldsBersama):
    pass


class SiswaRingkasResponse(BaseModel):
    """Untuk layar daftar/tabel - NIK tersamar (PRD §11.1)."""

    id: int
    nisn: str
    nipd: str | None
    nama_lengkap: str
    jenis_kelamin: str
    tempat_lahir: str
    tanggal_lahir: date
    nik_tersamar: str | None
    alamat: str
    id_kelas: int | None
    nama_kelas: str | None
    status_siswa: str
    aktif: bool
    nama_ortu: str | None
    nomor_whatsapp_ortu: str | None


class SiswaDetailResponse(SiswaRingkasResponse):
    """Untuk layar edit satu baris - NIK utuh (PRD §11.1: "utuh hanya
    ketika baris sedang diedit"). Endpoint ini sendiri sudah dibatasi
    kepala sekolah/admin (§10), jadi tidak ada peran lain yang bisa
    memanggilnya."""

    nik: str | None


class GuruBuatRequest(BaseModel):
    nama_lengkap: str = Field(min_length=1)
    nip: str | None = None
    pangkat_jabatan: str | None = None


class GuruUbahRequest(GuruBuatRequest):
    pass


class GuruResponse(BaseModel):
    id: int
    nama_lengkap: str
    nip: str | None
    pangkat_jabatan: str | None
    aktif: bool


class KelasBuatRequest(BaseModel):
    tingkat: int
    nama_kelas: str = Field(min_length=1)
    tahun_ajaran: str = Field(min_length=1)
    wali_kelas_id: int | None = None


class KelasUbahRequest(KelasBuatRequest):
    pass


class KelasResponse(BaseModel):
    id: int
    tingkat: int
    nama_kelas: str
    tahun_ajaran: str
    wali_kelas_id: int | None
    nama_wali_kelas: str | None
    aktif: bool


class KartuTerbitkanRequest(BaseModel):
    #: UID baku (ternormalisasi klien) dan UID mentah dari alat (PRD §17) -
    #: sama seperti ScanRequest, server tidak pernah menormalisasi ulang.
    uid_kartu: str = Field(min_length=1)
    uid_mentah: str = Field(min_length=1)
    id_pemilik: int
    jenis_pemilik: str
    tanggal_terbit: date | None = None

    @field_validator("jenis_pemilik")
    @classmethod
    def _jenis_pemilik_valid(cls, v: str) -> str:
        if v not in ("SISWA", "GURU"):
            raise ValueError("jenis_pemilik harus SISWA atau GURU")
        return v


class KartuNonaktifkanRequest(BaseModel):
    alasan: str | None = None


class KartuCetakUlangRequest(BaseModel):
    uid_kartu_baru: str = Field(min_length=1)
    uid_mentah_baru: str = Field(min_length=1)
    alasan: str | None = None


class KartuResponse(BaseModel):
    id: int
    uid_kartu: str
    id_pemilik: int
    jenis_pemilik: str
    status: str
    tanggal_terbit: date


class KartuCekResponse(BaseModel):
    """Layar Uji Baca Kartu (§18) dan verifikasi Wizard Ganti Jalur
    (§12.4) - murni pencarian, tidak pernah membentuk absensi."""

    uid: str
    terdaftar: bool
    status: str | None = None
    nama_pemilik: str | None = None


# ---------------------------------------------------------------------------
# Kalender akademik (PRD §8) - dua jalur siswa/guru
# ---------------------------------------------------------------------------


class EntriKalenderRequest(BaseModel):
    mulai: date
    selesai: date
    keterangan: str = Field(min_length=1)
    jenis_siswa: str
    jenis_guru: str
    tahun_ajaran: str = Field(min_length=1)


class PratinjauDampakKalenderRequest(BaseModel):
    mulai: date
    selesai: date
    jenis_siswa: str
    jenis_guru: str


class PratinjauDampakKalenderResponse(BaseModel):
    jumlah_hari: int
    perkiraan_baris: int


class TambahKalenderRequest(EntriKalenderRequest):
    alasan: str = Field(min_length=1)


class BatalkanKalenderRequest(BaseModel):
    alasan: str = Field(min_length=1)


class SalinTahunKalenderRequest(BaseModel):
    tahun_asal: str = Field(min_length=1)
    tahun_tujuan: str = Field(min_length=1)
    alasan: str = Field(min_length=1)


class EntriKalenderResponse(BaseModel):
    id: int
    mulai: date
    selesai: date
    keterangan: str
    jenis_siswa: str
    jenis_guru: str
    tahun_ajaran: str
    alasan: str
    dibatalkan: bool
    waktu_dibuat: datetime


class JenisHariResponse(BaseModel):
    tanggal: date
    jenis_siswa: str
    jenis_guru: str


# ---------------------------------------------------------------------------
# Pengaturan (PRD §4, §14.2) - jadwal per hari, ambang, saklar modul,
# identitas sekolah
# ---------------------------------------------------------------------------


class JadwalBaruRequest(BaseModel):
    """Tambah VERSI baru jadwal (PRD §4: wajib tanggal mulai berlaku,
    tidak pernah menimpa baris lama)."""

    jenis_pemilik: str
    hari: int = Field(ge=1, le=7)
    jam_masuk: time
    batas_terlambat: time
    jam_pulang: time
    berlaku_mulai: date
    #: Hanya berlaku untuk jenis SISWA - diabaikan untuk GURU (guru tidak
    #: mengenal ALPA/jendela pulang terpisah).
    cutoff_alpa: time | None = None
    pulang_mulai: time | None = None
    berlaku_sampai: date | None = None

    @field_validator("jenis_pemilik")
    @classmethod
    def _jenis_pemilik_valid(cls, v: str) -> str:
        if v not in ("SISWA", "GURU"):
            raise ValueError("jenis_pemilik harus SISWA atau GURU")
        return v


class JadwalResponse(BaseModel):
    id: int
    jenis_pemilik: str
    hari: int
    jam_masuk: time
    batas_terlambat: time
    jam_pulang: time
    cutoff_alpa: time | None = None
    pulang_mulai: time | None = None
    berlaku_mulai: date
    berlaku_sampai: date | None


class AmbangResponse(BaseModel):
    jendela_scan_berulang_detik: float
    batas_terlambat_guru: time
    ambang_tahan_konversi_pulang: float
    #: Dipakai scheduler untuk menjadwalkan cron cutoff ALPA/notifikasi -
    #: BUKAN sumber klasifikasi (itu school_schedules). None berarti
    #: scheduler belum bisa dijalankan (lihat app/scheduler/scheduler.py).
    cutoff_alpa: time | None
    waktu_kirim_notif_alpa: time | None


class AmbangUbahRequest(BaseModel):
    jendela_scan_berulang_detik: float | None = None
    batas_terlambat_guru: time | None = None
    ambang_tahan_konversi_pulang: float | None = None
    cutoff_alpa: time | None = None
    waktu_kirim_notif_alpa: time | None = None
    alasan: str = Field(min_length=1)


class SaklarResponse(BaseModel):
    modul_whatsapp_aktif: bool
    modul_absen_pulang: bool


class SaklarUbahRequest(BaseModel):
    modul_whatsapp_aktif: bool | None = None
    modul_absen_pulang: bool | None = None
    alasan: str = Field(min_length=1)


class IdentitasSekolahResponse(BaseModel):
    nama: str | None
    npsn: str | None
    alamat: str | None
    logo: str | None
    nama_kepala_sekolah: str | None
    nip_kepala_sekolah: str | None


class IdentitasSekolahUbahRequest(BaseModel):
    nama: str | None = None
    npsn: str | None = None
    alamat: str | None = None
    logo: str | None = None
    nama_kepala_sekolah: str | None = None
    nip_kepala_sekolah: str | None = None
    alasan: str = Field(min_length=1)


# ---------------------------------------------------------------------------
# Manajemen akun & peran (PRD §10)
# ---------------------------------------------------------------------------

_PERAN_VALID = ("KEPALA_SEKOLAH", "ADMIN", "WALI_KELAS", "OPERATOR")


class PenggunaBuatRequest(BaseModel):
    username: str = Field(min_length=1)
    password_awal: str = Field(min_length=8)
    peran: str
    #: Hanya berlaku untuk peran WALI_KELAS.
    id_kelas_diampu: int | None = None

    @field_validator("peran")
    @classmethod
    def _peran_valid(cls, v: str) -> str:
        if v not in _PERAN_VALID:
            raise ValueError(f"peran harus salah satu dari {_PERAN_VALID}")
        return v


class PenggunaResponse(BaseModel):
    id: int
    username: str
    peran: str
    id_kelas_diampu: int | None
    aktif: bool
    wajib_ganti_password: bool
    waktu_login_terakhir: datetime | None


class ResetPasswordAkunRequest(BaseModel):
    password_baru: str = Field(min_length=8)


class AturKelasDiampuRequest(BaseModel):
    id_kelas_diampu: int | None = None


# ---------------------------------------------------------------------------
# Riwayat Scan (PRD §7.4, §10) - baca-saja
# ---------------------------------------------------------------------------


class RiwayatScanResponse(BaseModel):
    id: int
    waktu_scan: datetime
    uid_kartu: str
    jenis_event: str
    label: str
    id_pemilik: int | None
    jenis_pemilik: str | None
    nama: str | None
    #: PRD §17. Baris lama (sebelum kolom ini ada) tetap None - jujur,
    #: bukan ditebak. Baris baru terisi sejak jalur disambungkan ke
    #: attendance_events (lihat app.server.routers.scan).
    jalur: str | None = None


# ---------------------------------------------------------------------------
# Koreksi Satuan (PRD §9.1, §10) - siswa saja, lihat app/core/koreksi_satuan.py
# ---------------------------------------------------------------------------

_STATUS_SISWA_VALID = ("HADIR", "TERLAMBAT", "ALPA", "IZIN", "SAKIT", "DISPENSASI")


class StatusSaatIniResponse(BaseModel):
    """Untuk konfirmasi di layar sebelum benar-benar mengoreksi."""

    id_siswa: int
    nama_lengkap: str
    id_kelas: int | None
    id_absensi: int | None
    status_masuk_sekarang: str | None
    status_final_sekarang: str | None


class KoreksiSatuanBuatRequest(BaseModel):
    id_siswa: int
    tanggal: date
    status_baru: str
    alasan: str = Field(min_length=1)

    @field_validator("status_baru")
    @classmethod
    def _status_baru_valid(cls, v: str) -> str:
        if v not in _STATUS_SISWA_VALID:
            raise ValueError(f"status_baru harus salah satu dari {_STATUS_SISWA_VALID}")
        return v


class BatalkanKoreksiSatuanRequest(BaseModel):
    alasan: str = Field(min_length=1)


class KoreksiSatuanResponse(BaseModel):
    id: int
    id_absensi: int
    id_pengguna: int
    waktu: datetime
    nilai_sebelum: dict
    nilai_sesudah: dict
    alasan: str
    dibatalkan: bool


# ---------------------------------------------------------------------------
# Koreksi Guru (PRD §6) - status manual DINAS_LUAR/IZIN/SAKIT/CUTI + lampiran,
# lihat app/core/koreksi_guru.py. POST /koreksi-guru memakai Form (multipart)
# supaya lampiran opsional bisa ikut dalam satu permintaan - lihat
# app/server/routers/koreksi_guru.py, bukan skema BaseModel di sini.
# ---------------------------------------------------------------------------


class StatusSaatIniGuruResponse(BaseModel):
    """Untuk konfirmasi di layar sebelum benar-benar mengoreksi."""

    id_guru: int
    nama_lengkap: str
    id_absensi: int | None
    status_masuk_sekarang: str | None
    status_final_sekarang: str | None


class BatalkanKoreksiGuruRequest(BaseModel):
    alasan: str = Field(min_length=1)


class KoreksiGuruResponse(BaseModel):
    id: int
    id_absensi: int
    id_pengguna: int
    waktu: datetime
    nilai_sebelum: dict
    nilai_sesudah: dict
    alasan: str
    lampiran: str | None
    dibatalkan: bool


# ---------------------------------------------------------------------------
# Notifikasi (PRD §13, menu §18 NOTIFIKASI) - template, antrean, mode uji.
# Koneksi WhatsApp nyata tetap mati (Fase 2) - lihat app.server.notifikasi_service.
# ---------------------------------------------------------------------------


class TemplateNotifikasiResponse(BaseModel):
    jenis: str
    konten: str | None
    versi: int | None
    aktif: bool


class TemplateNotifikasiUbahRequest(BaseModel):
    konten: str = Field(min_length=1)
    alasan: str = Field(min_length=1)


class AntreanNotifikasiResponse(BaseModel):
    id: int
    id_absensi: int
    nomor_tujuan: str
    isi_pesan: str
    jenis: str
    status: str
    kunci_idempotensi: str
    waktu_kejadian: datetime
    waktu_dijadwalkan: datetime | None
    waktu_kirim: datetime | None
    jumlah_percobaan: int
    pesan_galat: str | None


class ModeUjiResponse(BaseModel):
    aktif: bool
    nomor_admin: str | None


class ModeUjiUbahRequest(BaseModel):
    aktif: bool | None = None
    nomor_admin: str | None = None
    alasan: str = Field(min_length=1)

    @field_validator("nomor_admin")
    @classmethod
    def _nomor_admin_dirapikan(cls, v: str | None) -> str | None:
        if v is None or v.strip() == "":
            return None
        return normalisasi_nomor_whatsapp(v)


# ---------------------------------------------------------------------------
# Laporan (PRD §14.2, §9.3) - NIK TIDAK PERNAH muncul di skema ini (Aturan
# Global #6). Lihat app.server.laporan_service untuk bentuk REKAP vs DETAIL.
# ---------------------------------------------------------------------------


class RekapSiswaResponse(BaseModel):
    id_siswa: int
    nama_lengkap: str
    nama_kelas: str | None
    jumlah_hari_tercatat: int
    jumlah: dict[str, int]


class BarisDetailHarianResponse(BaseModel):
    tanggal: date
    status_masuk: str | None
    status_final: str | None
    alasan_final: str | None


class LaporanDetailSiswaResponse(BaseModel):
    nama_lengkap: str
    nisn: str
    nama_kelas: str | None
    baris: list[BarisDetailHarianResponse]


class HasilLaporanResponse(BaseModel):
    label: str
    tanggal_mulai: date
    tanggal_selesai: date
    id_kelas: int | None
    nama_kelas: str | None
    id_siswa: int | None
    rekap: list[RekapSiswaResponse] | None
    detail: LaporanDetailSiswaResponse | None


class LaporanArsipResponse(BaseModel):
    id: int
    id_pengguna: int
    label: str
    tanggal_mulai: date
    tanggal_selesai: date
    id_kelas: int | None
    id_siswa: int | None
    path_pdf: str
    waktu_cetak: datetime


class KoreksiSetelahTerbitResponse(BaseModel):
    sumber: str
    id_absensi: int
    nama_siswa: str
    tanggal: date
    waktu_koreksi: datetime
    id_pengguna: int
    nilai_sebelum: dict
    nilai_sesudah: dict
    alasan: str


# ---------------------------------------------------------------------------
# Perangkat (PRD §12.4): izin darurat alat dan wizard ganti jalur
# ---------------------------------------------------------------------------


class IzinDaruratBuatRequest(BaseModel):
    station_id: str = Field(min_length=1)
    jam: float = Field(gt=0)
    alasan: str = Field(min_length=1)


class IzinDaruratPerpanjangRequest(BaseModel):
    jam: float = Field(gt=0)
    alasan: str = Field(min_length=1)


class IzinDaruratCabutRequest(BaseModel):
    alasan: str = Field(min_length=1)


class IzinDaruratResponse(BaseModel):
    id: int
    station_id: str
    id_pengguna_pemberi: int
    waktu_mulai: datetime
    waktu_berakhir: datetime
    alasan: str
    dicabut: bool


class IzinDaruratAktifResponse(BaseModel):
    """Dipakai lobi (bukan hanya layar admin) untuk menyalakan banner MODE
    DARURAT - sengaja tanpa field beralasan/pemberi supaya klien lobi
    (peran Operator) tidak melihat lebih dari yang perlu."""

    aktif: bool
    station_id: str
    waktu_berakhir: datetime | None = None


class GantiJalurRequest(BaseModel):
    jalur_baru: str = Field(min_length=1)
    uid_kartu_verifikasi: list[str] = Field(min_length=3, max_length=3)
    alasan: str = Field(min_length=1)


class GantiJalurResponse(BaseModel):
    jalur_lama: str
    jalur_baru: str


class TelemetriLobiRequest(BaseModel):
    station_id: str = Field(min_length=1)
    jumlah_pending: int = Field(ge=0)


class TelemetriLobiResponse(BaseModel):
    station_id: str
    jumlah_pending: int
    waktu_lapor: datetime


# ---------------------------------------------------------------------------
# Dashboard (PRD §14.1)
# ---------------------------------------------------------------------------


class RingkasanHariIniResponse(BaseModel):
    tanggal: date
    per_status: dict[str, int]
    total_tercatat: int


class TitikGrafikResponse(BaseModel):
    periode: str
    per_status: dict[str, int]


class TitikTrenResponse(BaseModel):
    tanggal: date
    jumlah: int


class PerbandinganKelasResponse(BaseModel):
    id_kelas: int | None
    nama_kelas: str
    per_status: dict[str, int]
    total_tercatat: int


class SiswaAlpaTerbanyakResponse(BaseModel):
    id_siswa: int
    nama_lengkap: str
    nama_kelas: str
    jumlah_alpa: int


class StatusJalurResponse(BaseModel):
    jalur: str
    waktu_terakhir_baca: datetime
    diam: bool


class StatusKlienResponse(BaseModel):
    username: str
    peran: str
    terakhir_aktif: datetime
    diam: bool


class StatusAntreanLobiResponse(BaseModel):
    station_id: str
    jumlah_pending: int
    waktu_lapor: datetime
    kedaluwarsa: bool


class StatusOperasionalResponse(BaseModel):
    jalur: list[StatusJalurResponse]
    klien: list[StatusKlienResponse]
    antrean_lobi: list[StatusAntreanLobiResponse]
    #: None berarti "belum tersedia" (Slice 14/Pencadangan belum
    #: dibangun) - TIDAK PERNAH dipalsukan.
    pencadangan_terakhir: datetime | None
