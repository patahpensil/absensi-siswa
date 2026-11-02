"""Layanan Koreksi Satuan (PRD §9.1, §10): koreksi status_final satu
baris attendance siswa, dengan alasan wajib dan jejak nilai lama/baru -
juga jalur untuk input manual IZIN/SAKIT/DISPENSASI. Lapisan integrasi
DB (sama seperti app.server.pengguna_service dan app.core.koreksi_massal_service).

TIDAK menyentuh app.core.engine/mesin_absensi - status_masuk ("kenyataan"
hasil scan) tidak pernah disentuh, hanya status_final ("aturan resmi
laporan") yang dikoreksi, persis seperti koreksi massal.

Lingkup peran (PRD §10): kepala sekolah/admin bebas semua kelas; wali
kelas HANYA kelasnya sendiri, ditegakkan lewat
app.core.akses_kelas.pastikan_akses_kelas() - PENYAMBUNGAN YANG DIMINTA
SLICE INI (primitif itu dibuat di slice Manajemen Akun & Peran, sudah
punya satu pemanggil lain di RiwayatScanService sejak slice Riwayat
Scan - lihat README "UTANG KOREKSI SATUAN", ditutup slice ini). Operator
ditolak sepenuhnya lewat app.core.koreksi_satuan.pastikan_berwenang().
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date

from app.core.akses_kelas import pastikan_akses_kelas
from app.core.koreksi_satuan import pastikan_berwenang, terapkan_koreksi_satuan
from app.core.status import AlasanFinal, Peran, StatusSiswa
from app.data.models.enums import JenisPemilik
from app.data.models.koreksi_satuan import KoreksiSatuan
from app.data.models.system import User
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.koreksi_satuan import KoreksiSatuanRepository
from app.data.repositories.student import StudentRepository


class SiswaTidakDitemukan(LookupError):
    pass


class TidakAdaPerubahan(Exception):
    """status_baru sama dengan status_final yang sedang berlaku - tidak
    ada apa pun untuk dikoreksi (app.core.koreksi_satuan.terapkan_koreksi_satuan
    mengembalikan None)."""


class KoreksiTidakDitemukan(LookupError):
    pass


class KoreksiSudahDibatalkan(Exception):
    pass


@dataclass(frozen=True)
class StatusSaatIni:
    """Untuk konfirmasi di layar sebelum benar-benar mengoreksi (PRD
    §9.1: admin harus tahu persis apa yang akan diubah)."""

    id_siswa: int
    nama_lengkap: str
    id_kelas: int | None
    id_absensi: int | None
    status_masuk_sekarang: str | None
    status_final_sekarang: str | None


class KoreksiSatuanService:
    def __init__(
        self,
        *,
        attendance_repo: AttendanceRepository,
        koreksi_repo: KoreksiSatuanRepository,
        student_repo: StudentRepository,
        audit_repo: AuditLogRepository,
    ) -> None:
        self._attendance_repo = attendance_repo
        self._koreksi_repo = koreksi_repo
        self._student_repo = student_repo
        self._audit_repo = audit_repo

    def _pastikan_lingkup(self, *, pelaku: User, id_kelas_siswa: int | None) -> None:
        peran = Peran(pelaku.peran.value)
        pastikan_berwenang(peran)
        if peran == Peran.WALI_KELAS:
            pastikan_akses_kelas(
                peran=peran, id_kelas_diampu=pelaku.id_kelas_diampu, id_kelas_target=id_kelas_siswa
            )

    def status_saat_ini(self, *, pelaku: User, id_siswa: int, tanggal: date) -> StatusSaatIni:
        siswa = self._student_repo.cari_by_id(id_siswa)
        if siswa is None:
            raise SiswaTidakDitemukan(id_siswa)
        self._pastikan_lingkup(pelaku=pelaku, id_kelas_siswa=siswa.id_kelas)

        absensi = self._attendance_repo.cari_by_pemilik_tanggal(
            id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=tanggal
        )
        return StatusSaatIni(
            id_siswa=siswa.id,
            nama_lengkap=siswa.nama_lengkap,
            id_kelas=siswa.id_kelas,
            id_absensi=absensi.id if absensi is not None else None,
            status_masuk_sekarang=absensi.status_masuk if absensi is not None else None,
            status_final_sekarang=absensi.status_final if absensi is not None else None,
        )

    def koreksi(
        self, *, pelaku: User, id_siswa: int, tanggal: date, status_baru: StatusSiswa, alasan: str
    ) -> KoreksiSatuan:
        siswa = self._student_repo.cari_by_id(id_siswa)
        if siswa is None:
            raise SiswaTidakDitemukan(id_siswa)
        self._pastikan_lingkup(pelaku=pelaku, id_kelas_siswa=siswa.id_kelas)

        absensi = self._attendance_repo.cari_by_pemilik_tanggal(
            id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=tanggal
        )
        if absensi is None:
            # Belum pernah scan sama sekali - input manual IZIN/SAKIT/
            # DISPENSASI (PRD §10). status_masuk TETAP None (bukan
            # "kenyataan" yang dikarang) - hanya status_final yang diisi,
            # lewat koreksi() di bawah, bukan di sini.
            absensi = self._attendance_repo.tambah(
                id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=tanggal, status_masuk=None
            )

        status_final_sekarang = StatusSiswa(absensi.status_final) if absensi.status_final else None
        alasan_final_sekarang = AlasanFinal(absensi.alasan_final) if absensi.alasan_final else None
        perubahan = terapkan_koreksi_satuan(
            status_final_sekarang=status_final_sekarang,
            alasan_final_sekarang=alasan_final_sekarang,
            status_baru=status_baru,
        )
        if perubahan is None:
            raise TidakAdaPerubahan(f"Status sudah {status_baru.value}, tidak ada yang dikoreksi")

        self._attendance_repo.koreksi(
            absensi.id, perubahan=dict(perubahan.nilai_sesudah), id_pengguna=pelaku.id, alasan=alasan
        )
        koreksi = self._koreksi_repo.catat(
            id_absensi=absensi.id,
            id_pengguna=pelaku.id,
            nilai_sebelum=dict(perubahan.nilai_sebelum),
            nilai_sesudah=dict(perubahan.nilai_sesudah),
            alasan=alasan,
        )
        self._koreksi_repo.commit()
        return koreksi

    def batalkan(self, *, pelaku: User, id_koreksi: int, alasan: str) -> KoreksiSatuan:
        koreksi = self._koreksi_repo.cari_by_id(id_koreksi)
        if koreksi is None:
            raise KoreksiTidakDitemukan(id_koreksi)
        if koreksi.dibatalkan:
            raise KoreksiSudahDibatalkan(id_koreksi)

        absensi = self._attendance_repo.cari_by_id(koreksi.id_absensi)
        siswa = self._student_repo.cari_by_id(absensi.id_pemilik)
        self._pastikan_lingkup(pelaku=pelaku, id_kelas_siswa=siswa.id_kelas if siswa is not None else None)

        self._attendance_repo.koreksi(
            absensi.id, perubahan=dict(koreksi.nilai_sebelum), id_pengguna=pelaku.id, alasan=alasan
        )
        self._koreksi_repo.tandai_dibatalkan(koreksi, id_pengguna=pelaku.id)
        self._audit_repo.catat(
            id_pengguna=pelaku.id,
            aksi="batalkan_koreksi_satuan",
            nilai_sebelum=None,
            nilai_sesudah={"id_koreksi": id_koreksi, "id_absensi": absensi.id},
            alasan=alasan,
        )
        self._koreksi_repo.commit()
        return koreksi

    def daftar_riwayat(self, *, pelaku: User, id_siswa: int, tanggal: date) -> list[KoreksiSatuan]:
        siswa = self._student_repo.cari_by_id(id_siswa)
        if siswa is None:
            raise SiswaTidakDitemukan(id_siswa)
        self._pastikan_lingkup(pelaku=pelaku, id_kelas_siswa=siswa.id_kelas)

        absensi = self._attendance_repo.cari_by_pemilik_tanggal(
            id_pemilik=id_siswa, jenis_pemilik=JenisPemilik.SISWA, tanggal=tanggal
        )
        if absensi is None:
            return []
        return self._koreksi_repo.daftar_by_absensi(absensi.id)
