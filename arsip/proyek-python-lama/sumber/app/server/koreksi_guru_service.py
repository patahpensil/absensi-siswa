"""Layanan Koreksi Guru (PRD §6): input status manual DINAS_LUAR/IZIN/
SAKIT/CUTI satu baris attendance guru, dengan alasan wajib, lampiran
opsional, dan jejak nilai lama/baru. Lapisan integrasi DB (sama seperti
app.server.koreksi_satuan_service).

TIDAK menyentuh app.core.engine/mesin_absensi - status_masuk ("kenyataan"
hasil scan+toleransi 09:30) tidak pernah disentuh, hanya status_final
yang dikoreksi, persis seperti koreksi satuan siswa.

Lingkup peran: HANYA kepala sekolah/admin (app.core.koreksi_guru.
PERAN_KOREKSI_GURU) - TIDAK ada pengecualian wali kelas seperti koreksi
satuan siswa (guru bukan tanggung jawab wali kelas). Penyimpanan berkas
lampiran didelegasikan ke app.server.lampiran_storage - service ini
hanya menerima path relatif yang SUDAH tersimpan (pemanggil/router yang
menulis berkasnya ke disk), supaya lapisan ini tetap bisa diuji tanpa
menyentuh filesystem sungguhan.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date

from app.core.koreksi_guru import pastikan_berwenang, pastikan_status_manual, terapkan_koreksi_guru
from app.core.status import AlasanFinal, Peran, StatusGuru
from app.data.models.enums import JenisPemilik
from app.data.models.koreksi_guru import KoreksiGuru
from app.data.models.system import User
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.koreksi_guru import KoreksiGuruRepository
from app.data.repositories.teacher import TeacherRepository


class GuruTidakDitemukan(LookupError):
    pass


class TidakAdaPerubahan(Exception):
    """status_baru sama dengan status_final yang sedang berlaku - tidak
    ada apa pun untuk dikoreksi (app.core.koreksi_guru.terapkan_koreksi_guru
    mengembalikan None)."""


class KoreksiTidakDitemukan(LookupError):
    pass


class KoreksiSudahDibatalkan(Exception):
    pass


@dataclass(frozen=True)
class StatusSaatIniGuru:
    """Untuk konfirmasi di layar sebelum benar-benar mengoreksi (PRD
    §6: admin harus tahu persis apa yang akan diubah)."""

    id_guru: int
    nama_lengkap: str
    id_absensi: int | None
    status_masuk_sekarang: str | None
    status_final_sekarang: str | None


class KoreksiGuruService:
    def __init__(
        self,
        *,
        attendance_repo: AttendanceRepository,
        koreksi_repo: KoreksiGuruRepository,
        teacher_repo: TeacherRepository,
        audit_repo: AuditLogRepository,
    ) -> None:
        self._attendance_repo = attendance_repo
        self._koreksi_repo = koreksi_repo
        self._teacher_repo = teacher_repo
        self._audit_repo = audit_repo

    def status_saat_ini(self, *, pelaku: User, id_guru: int, tanggal: date) -> StatusSaatIniGuru:
        pastikan_berwenang(Peran(pelaku.peran.value))
        guru = self._teacher_repo.cari_by_id(id_guru)
        if guru is None:
            raise GuruTidakDitemukan(id_guru)

        absensi = self._attendance_repo.cari_by_pemilik_tanggal(
            id_pemilik=id_guru, jenis_pemilik=JenisPemilik.GURU, tanggal=tanggal
        )
        return StatusSaatIniGuru(
            id_guru=guru.id,
            nama_lengkap=guru.nama_lengkap,
            id_absensi=absensi.id if absensi is not None else None,
            status_masuk_sekarang=absensi.status_masuk if absensi is not None else None,
            status_final_sekarang=absensi.status_final if absensi is not None else None,
        )

    def koreksi(
        self,
        *,
        pelaku: User,
        id_guru: int,
        tanggal: date,
        status_baru: StatusGuru,
        alasan: str,
        lampiran: str | None = None,
    ) -> KoreksiGuru:
        peran = Peran(pelaku.peran.value)
        pastikan_berwenang(peran)
        pastikan_status_manual(status_baru)

        guru = self._teacher_repo.cari_by_id(id_guru)
        if guru is None:
            raise GuruTidakDitemukan(id_guru)

        absensi = self._attendance_repo.cari_by_pemilik_tanggal(
            id_pemilik=id_guru, jenis_pemilik=JenisPemilik.GURU, tanggal=tanggal
        )
        if absensi is None:
            # Belum pernah scan sama sekali - input manual DINAS_LUAR/
            # IZIN/SAKIT/CUTI (PRD §6). status_masuk TETAP None (bukan
            # "kenyataan" yang dikarang) - hanya status_final yang diisi,
            # lewat koreksi() di bawah, bukan di sini.
            absensi = self._attendance_repo.tambah(
                id_pemilik=id_guru, jenis_pemilik=JenisPemilik.GURU, tanggal=tanggal, status_masuk=None
            )

        status_final_sekarang = StatusGuru(absensi.status_final) if absensi.status_final else None
        alasan_final_sekarang = AlasanFinal(absensi.alasan_final) if absensi.alasan_final else None
        perubahan = terapkan_koreksi_guru(
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
            lampiran=lampiran,
        )
        self._koreksi_repo.commit()
        return koreksi

    def batalkan(self, *, pelaku: User, id_koreksi: int, alasan: str) -> KoreksiGuru:
        pastikan_berwenang(Peran(pelaku.peran.value))

        koreksi = self._koreksi_repo.cari_by_id(id_koreksi)
        if koreksi is None:
            raise KoreksiTidakDitemukan(id_koreksi)
        if koreksi.dibatalkan:
            raise KoreksiSudahDibatalkan(id_koreksi)

        absensi = self._attendance_repo.cari_by_id(koreksi.id_absensi)
        self._attendance_repo.koreksi(
            absensi.id, perubahan=dict(koreksi.nilai_sebelum), id_pengguna=pelaku.id, alasan=alasan
        )
        self._koreksi_repo.tandai_dibatalkan(koreksi, id_pengguna=pelaku.id)
        self._audit_repo.catat(
            id_pengguna=pelaku.id,
            aksi="batalkan_koreksi_guru",
            nilai_sebelum=None,
            nilai_sesudah={"id_koreksi": id_koreksi, "id_absensi": absensi.id},
            alasan=alasan,
        )
        self._koreksi_repo.commit()
        return koreksi

    def daftar_riwayat(self, *, pelaku: User, id_guru: int, tanggal: date) -> list[KoreksiGuru]:
        pastikan_berwenang(Peran(pelaku.peran.value))
        guru = self._teacher_repo.cari_by_id(id_guru)
        if guru is None:
            raise GuruTidakDitemukan(id_guru)

        absensi = self._attendance_repo.cari_by_pemilik_tanggal(
            id_pemilik=id_guru, jenis_pemilik=JenisPemilik.GURU, tanggal=tanggal
        )
        if absensi is None:
            return []
        return self._koreksi_repo.daftar_by_absensi(absensi.id)
