"""
KalenderService: lapisan tipis yang menyambungkan aturan murni di
app.core.kalender ke basis data - pola yang sama dengan app.core.engine
dan app.core.koreksi_massal_service (lihat catatan penempatan di sana
soal kenapa berkas ini boleh mengimpor app.data).

muat_kalender() juga dipakai app.core.engine.MesinAbsensi langsung (bukan
lewat KalenderService) untuk pengecekan jenis hari saat memproses scan -
lihat catatan di engine.py.
"""

from __future__ import annotations

from datetime import date

from app.core.kalender import EntriKalender as EntriKalenderMurni
from app.core.kalender import Kalender
from app.core.koreksi_massal import AksesKoreksiMassalDitolak, pastikan_berwenang
from app.core.status import JenisHari, Peran
from app.data.models.kalender import EntriKalender
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kalender import KalenderRepository

__all__ = [
    "KalenderService",
    "EntriKalenderTidakDitemukan",
    "EntriKalenderSudahDibatalkan",
    "AksesKoreksiMassalDitolak",
    "muat_kalender",
]


class EntriKalenderTidakDitemukan(LookupError):
    pass


class EntriKalenderSudahDibatalkan(Exception):
    pass


def muat_kalender(kalender_repo: KalenderRepository, *, tahun_ajaran: str | None = None) -> Kalender:
    """Bentuk Kalender murni (app.core.kalender) dari seluruh entri aktif
    di basis data. Dipanggil ulang setiap kali - kalender cuma sekitar
    sepuluh entri per semester (SKILL.md), jadi tidak perlu di-cache."""
    baris = kalender_repo.daftar_aktif(tahun_ajaran=tahun_ajaran)
    return Kalender(entri=[_ke_entri_murni(b) for b in baris])


def _ke_entri_murni(baris: EntriKalender) -> EntriKalenderMurni:
    return EntriKalenderMurni(
        mulai=baris.mulai,
        selesai=baris.selesai,
        keterangan=baris.keterangan,
        jenis_siswa=JenisHari(baris.jenis_siswa),
        jenis_guru=JenisHari(baris.jenis_guru),
        tahun_ajaran=baris.tahun_ajaran,
    )


class KalenderService:
    def __init__(self, *, kalender_repo: KalenderRepository, audit_repo: AuditLogRepository) -> None:
        self._kalender_repo = kalender_repo
        self._audit_repo = audit_repo

    def jenis_hari_siswa(self, tanggal: date) -> JenisHari:
        return muat_kalender(self._kalender_repo).jenis_hari_siswa(tanggal)

    def jenis_hari_guru(self, tanggal: date) -> JenisHari:
        return muat_kalender(self._kalender_repo).jenis_hari_guru(tanggal)

    def pratinjau_dampak(self, entri_baru: EntriKalenderMurni, *, jumlah_orang: int) -> dict:
        """SKILL.md: "Perubahan ini memengaruhi 6 hari dan 6.000 baris
        absensi. Hitung ulang statusnya?" - wajib ditampilkan sebelum
        tambah() dipanggil."""
        return muat_kalender(self._kalender_repo).dampak_perubahan(entri_baru, jumlah_orang)

    def tambah(
        self,
        *,
        mulai: date,
        selesai: date,
        keterangan: str,
        jenis_siswa: JenisHari,
        jenis_guru: JenisHari,
        tahun_ajaran: str,
        id_pengguna: int,
        peran: Peran,
        alasan: str,
    ) -> EntriKalender:
        pastikan_berwenang(peran)
        entri = self._kalender_repo.tambah(
            mulai=mulai,
            selesai=selesai,
            keterangan=keterangan,
            jenis_siswa=jenis_siswa.value,
            jenis_guru=jenis_guru.value,
            tahun_ajaran=tahun_ajaran,
            id_pengguna=id_pengguna,
            alasan=alasan,
        )
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="tambah_kalender",
            nilai_sebelum=None,
            nilai_sesudah={
                "id": entri.id,
                "mulai": mulai.isoformat(),
                "selesai": selesai.isoformat(),
                "keterangan": keterangan,
                "jenis_siswa": jenis_siswa.value,
                "jenis_guru": jenis_guru.value,
                "tahun_ajaran": tahun_ajaran,
            },
            alasan=alasan,
        )
        return entri

    def daftar(self, *, tahun_ajaran: str | None = None, limit: int = 200) -> list[EntriKalender]:
        """Untuk layar Kalender - termasuk entri yang sudah dibatalkan
        (riwayat), beda dari muat_kalender()/daftar_aktif() yang dipakai
        untuk penentuan jenis hari sungguhan."""
        daftar = self._kalender_repo.daftar_terbaru(limit=limit)
        if tahun_ajaran is not None:
            daftar = [e for e in daftar if e.tahun_ajaran == tahun_ajaran]
        return daftar

    def salin_dari_tahun_sebelumnya(
        self,
        *,
        tahun_asal: str,
        tahun_tujuan: str,
        id_pengguna: int,
        peran: Peran,
        alasan: str,
    ) -> list[EntriKalender]:
        """"Salin dari tahun sebelumnya" (SKILL.md "Kalender akademik") -
        titik awal kalender tahun ajaran baru, MENYALIN DATA INTERNAL
        APLIKASI, bukan impor berkas. Tidak menghitung ulang dampak
        (tahun tujuan belum punya absensi apa pun) dan tidak mengganggu
        tahun yang sedang berjalan - baris tahun_asal tetap apa adanya.

        Satu entri log audit induk untuk seluruh operasi (bukan satu per
        baris disalin) - sama seperti semangat "Riwayat Tindakan Massal",
        walau kalender bukan bagian penyaring koreksi massal itu sendiri.
        """
        pastikan_berwenang(peran)
        sumber = [e for e in self._kalender_repo.daftar_aktif(tahun_ajaran=tahun_asal)]

        disalin: list[EntriKalender] = []
        for entri in sumber:
            baru = self._kalender_repo.tambah(
                mulai=entri.mulai,
                selesai=entri.selesai,
                keterangan=entri.keterangan,
                jenis_siswa=entri.jenis_siswa,
                jenis_guru=entri.jenis_guru,
                tahun_ajaran=tahun_tujuan,
                id_pengguna=id_pengguna,
                alasan=alasan,
            )
            disalin.append(baru)

        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="salin_kalender_tahun",
            nilai_sebelum={"tahun_asal": tahun_asal, "jumlah_entri_sumber": len(sumber)},
            nilai_sesudah={"tahun_tujuan": tahun_tujuan, "jumlah_entri_disalin": len(disalin)},
            alasan=alasan,
        )
        return disalin

    def batalkan(self, id_entri: int, *, id_pengguna: int, peran: Peran, alasan: str) -> EntriKalender:
        pastikan_berwenang(peran)
        entri = self._kalender_repo.cari_by_id(id_entri)
        if entri is None:
            raise EntriKalenderTidakDitemukan(id_entri)
        if entri.dibatalkan:
            raise EntriKalenderSudahDibatalkan(id_entri)

        self._kalender_repo.batalkan(entri, id_pengguna=id_pengguna)
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="batalkan_kalender",
            nilai_sebelum=None,
            nilai_sesudah={"id": id_entri},
            alasan=alasan,
        )
        return entri
