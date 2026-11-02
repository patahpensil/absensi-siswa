"""
KoreksiMassalService: lapisan tipis yang menyambungkan aturan murni di
app.core.koreksi_massal ke basis data - sama seperti app.core.engine
menyambungkan app.core.mesin_absensi.

Catatan penempatan: berkas ini ikut mengimpor app.data (repository, model)
sama seperti app.core.engine, walau SKILL.md menulis "app/core/ tidak
pernah mengimpor dari app/ui/ maupun app/data/". Kelas MesinAbsensi di
engine.py sudah lebih dulu memakai pola yang sama (lapisan integrasi murni
yang menyambungkan aturan app.core.mesin_absensi ke repository) - berkas
ini mengikuti presisi yang sudah ada itu, bukan pola baru. Aturan yang
benar-benar murni tanpa basis data tetap di app.core.koreksi_massal.
"""

from __future__ import annotations

from app.config.pengaturan import Pengaturan
from app.core.koreksi_massal import (
    JenisTindakanMassal,
    PenyaringKoreksiMassal,
    Pratinjau,
    buat_pratinjau,
    pastikan_berwenang,
    terapkan_tindakan,
)
from app.core.status import AlasanFinal, Peran, StatusSiswa
from app.data.models.koreksi_massal import TindakanMassal
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.mass_correction import (
    MassCorrectionRepository,
    TindakanMassalSudahDibatalkan,
    TindakanMassalTidakDitemukan,
)

__all__ = [
    "KoreksiMassalService",
    "SnapshotDiperlukan",
    "KonfirmasiKetikDiperlukan",
    "KonfirmasiKetikTidakCocok",
]


class SnapshotDiperlukan(Exception):
    """Tindakan di atas ambang snapshot butuh snapshot basis data lebih
    dulu (SKILL.md "Koreksi massal"). Modul backup otomatis belum
    dibangun - sampai ada, tindakan sebesar ini ditolak dengan tegas,
    bukan diam-diam dilewati tanpa jaring pengaman.
    """

    def __init__(self, jumlah_baris: int, ambang: int) -> None:
        self.jumlah_baris = jumlah_baris
        self.ambang = ambang
        super().__init__(
            f"{jumlah_baris} baris terdampak, di atas ambang snapshot ({ambang}) - "
            "ambil snapshot basis data dulu, lalu ulangi dengan snapshot_sudah_diambil=True"
        )


class KonfirmasiKetikDiperlukan(Exception):
    """Tindakan di atas ambang konfirmasi butuh admin mengetik ulang
    jumlah baris terdampak sebagai bukti sudah membaca pratinjau."""

    def __init__(self, jumlah_baris: int) -> None:
        self.jumlah_baris = jumlah_baris
        super().__init__(f"Tindakan menyentuh {jumlah_baris} baris - ketik ulang jumlah ini untuk konfirmasi")


class KonfirmasiKetikTidakCocok(Exception):
    pass


class KoreksiMassalService:
    def __init__(
        self,
        *,
        attendance_repo: AttendanceRepository,
        mass_correction_repo: MassCorrectionRepository,
        audit_repo: AuditLogRepository,
        pengaturan: Pengaturan,
    ) -> None:
        self._attendance_repo = attendance_repo
        self._mass_correction_repo = mass_correction_repo
        self._audit_repo = audit_repo
        self._pengaturan = pengaturan

    def pratinjau(self, penyaring: PenyaringKoreksiMassal, *, peran: Peran) -> Pratinjau:
        pastikan_berwenang(peran)
        terdampak = self._cari_terdampak(penyaring)
        return buat_pratinjau(len(terdampak), self._pengaturan)

    def jalankan(
        self,
        *,
        penyaring: PenyaringKoreksiMassal,
        jenis_tindakan: JenisTindakanMassal,
        status_baru: StatusSiswa | None = None,
        id_pengguna: int,
        peran: Peran,
        alasan: str,
        snapshot_sudah_diambil: bool = False,
        konfirmasi_ketik: str | None = None,
    ) -> TindakanMassal:
        """Jalankan satu tindakan atas seluruh baris yang cocok penyaring.

        Baris yang tindakannya tidak berlaku (lihat
        app.core.koreksi_massal.terapkan_tindakan) dilewati diam-diam dan
        TIDAK dihitung di jumlah_baris - pratinjau dan hasil akhir selalu
        menunjukkan jumlah baris yang BENAR-BENAR berubah, bukan jumlah
        baris yang cocok penyaring.
        """
        pastikan_berwenang(peran)

        terdampak = self._cari_terdampak(penyaring)
        perubahan_semua = [
            perubahan
            for absensi in terdampak
            if (
                perubahan := terapkan_tindakan(
                    id_absensi=absensi.id,
                    status_masuk=StatusSiswa(absensi.status_masuk) if absensi.status_masuk else None,
                    status_final=StatusSiswa(absensi.status_final) if absensi.status_final else None,
                    alasan_final=AlasanFinal(absensi.alasan_final) if absensi.alasan_final else None,
                    izin_pulang_cepat=absensi.izin_pulang_cepat,
                    jenis_tindakan=jenis_tindakan,
                    status_baru=status_baru,
                )
            )
            is not None
        ]

        pratinjau = buat_pratinjau(len(perubahan_semua), self._pengaturan)
        if pratinjau.perlu_snapshot and not snapshot_sudah_diambil:
            raise SnapshotDiperlukan(pratinjau.jumlah_baris, self._pengaturan.ambang_snapshot_sebelum_massal)
        if pratinjau.perlu_konfirmasi_ketik:
            if konfirmasi_ketik is None:
                raise KonfirmasiKetikDiperlukan(pratinjau.jumlah_baris)
            if konfirmasi_ketik.strip() != str(pratinjau.jumlah_baris):
                raise KonfirmasiKetikTidakCocok(
                    f"Ketik ulang jumlah baris terdampak ({pratinjau.jumlah_baris}) untuk konfirmasi"
                )

        tindakan = self._mass_correction_repo.catat(
            id_pengguna=id_pengguna,
            jenis_tindakan=jenis_tindakan.value,
            status_baru=status_baru.value if status_baru is not None else None,
            penyaring=_penyaring_ke_json(penyaring),
            jumlah_baris=len(perubahan_semua),
            alasan=alasan,
        )

        for perubahan in perubahan_semua:
            self._attendance_repo.terapkan_koreksi_massal(perubahan.id_absensi, dict(perubahan.nilai_sesudah))
            self._mass_correction_repo.catat_baris(
                id_tindakan_massal=tindakan.id,
                id_absensi=perubahan.id_absensi,
                nilai_sebelum=dict(perubahan.nilai_sebelum),
                nilai_sesudah=dict(perubahan.nilai_sesudah),
            )

        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="koreksi_massal",
            nilai_sebelum=None,
            nilai_sesudah={
                "id_tindakan_massal": tindakan.id,
                "jenis_tindakan": jenis_tindakan.value,
                "penyaring": _penyaring_ke_json(penyaring),
                "jumlah_baris": len(perubahan_semua),
            },
            alasan=alasan,
        )
        self._mass_correction_repo.commit()
        return tindakan

    def batalkan(self, id_tindakan_massal: int, *, id_pengguna: int, peran: Peran, alasan: str) -> TindakanMassal:
        """Kembalikan seluruh baris yang disentuh satu TindakanMassal ke
        nilai_sebelum tersimpan, lalu tandai tindakan itu dibatalkan.
        Tidak melewati ambang snapshot/konfirmasi ketik - mengembalikan ke
        keadaan yang sudah diketahui berisiko jauh lebih rendah daripada
        tindakan baru.
        """
        pastikan_berwenang(peran)

        tindakan = self._mass_correction_repo.cari_by_id(id_tindakan_massal)
        if tindakan is None:
            raise TindakanMassalTidakDitemukan(id_tindakan_massal)
        if tindakan.dibatalkan:
            raise TindakanMassalSudahDibatalkan(id_tindakan_massal)

        baris_list = self._mass_correction_repo.daftar_baris(id_tindakan_massal)
        for baris in baris_list:
            self._attendance_repo.terapkan_koreksi_massal(baris.id_absensi, dict(baris.nilai_sebelum))

        self._mass_correction_repo.tandai_dibatalkan(tindakan, id_pengguna=id_pengguna)
        self._audit_repo.catat(
            id_pengguna=id_pengguna,
            aksi="batalkan_koreksi_massal",
            nilai_sebelum=None,
            nilai_sesudah={"id_tindakan_massal": id_tindakan_massal, "jumlah_baris": len(baris_list)},
            alasan=alasan,
        )
        self._mass_correction_repo.commit()
        return tindakan

    def _cari_terdampak(self, penyaring: PenyaringKoreksiMassal) -> list:
        return self._mass_correction_repo.cari_baris_terdampak(
            tanggal_mulai=penyaring.tanggal_mulai,
            tanggal_selesai=penyaring.tanggal_selesai,
            lingkup=penyaring.lingkup.value,
            tingkat=penyaring.tingkat,
            id_kelas=penyaring.id_kelas,
            id_siswa_terpilih=penyaring.id_siswa_terpilih,
            status_termasuk=frozenset(s.value for s in penyaring.status_efektif()),
        )


def _penyaring_ke_json(penyaring: PenyaringKoreksiMassal) -> dict:
    return {
        "tanggal_mulai": penyaring.tanggal_mulai.isoformat(),
        "tanggal_selesai": penyaring.tanggal_selesai.isoformat(),
        "lingkup": penyaring.lingkup.value,
        "tingkat": penyaring.tingkat,
        "id_kelas": penyaring.id_kelas,
        "id_siswa_terpilih": sorted(penyaring.id_siswa_terpilih),
        "status_termasuk": sorted(s.value for s in penyaring.status_efektif()),
    }
