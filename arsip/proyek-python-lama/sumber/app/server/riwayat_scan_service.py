"""Layanan Riwayat Scan (PRD §7.4, §10): baca-saja peristiwa scan mentah
dengan saring rentang tanggal, kelas, dan orang - lingkup dibatasi peran.
Lapisan integrasi DB (sama seperti app.server.auth_service dan
app.server.pengguna_service). TIDAK menyentuh app.core.engine/
mesin_absensi - murni membaca attendance_events apa adanya lewat
AttendanceEventRepository.cari(), tidak pernah mengubah atau menghitung
ulang apa pun (koreksi dari layar ini di luar cakupan slice ini).

Lingkup peran (SKILL.md/PRD §10, baris "Dashboard dan laporan"): admin
dan kepala sekolah bebas (semua orang/kelas); operator "riwayat scan"
tanpa batas kelas; wali kelas HANYA kelasnya sendiri, ditegakkan lewat
app.core.akses_kelas.pastikan_akses_kelas() - PEMANGGIL PERTAMA primitif
itu (dibuat di slice Manajemen Akun & Peran, sebelumnya belum ada
pemanggil - lihat README "UTANG KOREKSI SATUAN").

Label tampilan memetakan JenisEventScan (5 nilai nyata yang benar-benar
tersimpan) ke istilah PRD §7.4 - PRD menyebut 8 label
(ABSEN_MASUK/ABSEN_PULANG/DUPLIKAT/DI_LUAR_JENDELA/KARTU_TIDAK_DIKENAL/
KARTU_NONAKTIF/HARI_LIBUR/MANUAL_ENTRY) tapi
app.core.mesin_absensi.klasifikasi_scan() hanya pernah menghasilkan 5
(gap ini sudah tercatat di README, BUKAN tanggung jawab slice ini -
mengubahnya berarti menyentuh mesin klasifikasi). Label di bawah
mencerminkan penggabungan itu apa adanya, tidak berpura-pura granular.
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import datetime

from app.core.akses_kelas import pastikan_akses_kelas
from app.core.status import Peran
from app.data.models.attendance import AttendanceEvent, JenisEventScan
from app.data.models.enums import JenisPemilik
from app.data.models.system import User
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.data.repositories.kelas import KelasRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.teacher import TeacherRepository

#: PRD §7.4 - lihat catatan modul di atas soal penggabungan 8 -> 5 label.
LABEL_JENIS_EVENT: dict[JenisEventScan, str] = {
    JenisEventScan.MASUK: "Absen Masuk",
    JenisEventScan.PULANG: "Absen Pulang",
    JenisEventScan.DUPLICATE: "Duplikat",
    JenisEventScan.DI_LUAR_PERIODE: "Di Luar Jendela / Hari Libur",
    JenisEventScan.UNKNOWN_CARD: "Kartu Tidak Dikenal / Nonaktif",
}


class KelasTidakDitemukan(LookupError):
    pass


@dataclass(frozen=True)
class FilterRiwayatScan:
    sejak: datetime | None = None
    sampai: datetime | None = None
    id_kelas: int | None = None
    id_pemilik: int | None = None
    jenis_pemilik: JenisPemilik | None = None


@dataclass(frozen=True)
class BarisRiwayatScan:
    event: AttendanceEvent
    nama: str | None
    label: str


class RiwayatScanService:
    def __init__(
        self,
        *,
        event_repo: AttendanceEventRepository,
        student_repo: StudentRepository,
        teacher_repo: TeacherRepository,
        kelas_repo: KelasRepository,
    ) -> None:
        self._event_repo = event_repo
        self._student_repo = student_repo
        self._teacher_repo = teacher_repo
        self._kelas_repo = kelas_repo

    def cari(self, *, pengguna: User, filter: FilterRiwayatScan, limit: int = 200) -> list[BarisRiwayatScan]:
        peran = Peran(pengguna.peran.value)
        id_pemilik = filter.id_pemilik
        jenis_pemilik = filter.jenis_pemilik
        ids_pemilik_siswa: list[int] | None = None

        if peran == Peran.WALI_KELAS:
            id_kelas_diampu = pengguna.id_kelas_diampu
            if filter.id_kelas is not None:
                pastikan_akses_kelas(peran=peran, id_kelas_diampu=id_kelas_diampu, id_kelas_target=filter.id_kelas)
            if id_kelas_diampu is None:
                return []  # belum ditugaskan ke kelas mana pun - tidak ada yang boleh dilihat

            jenis_pemilik = JenisPemilik.SISWA  # wali kelas tidak pernah melihat data guru
            siswa_kelas = {s.id for s in self._student_repo.daftar_by_kelas(id_kelas_diampu, hanya_aktif=False)}
            if id_pemilik is not None:
                if id_pemilik not in siswa_kelas:
                    return []  # orang di luar kelasnya - kosong, bukan galat keras
            else:
                ids_pemilik_siswa = list(siswa_kelas)
        elif filter.id_kelas is not None and id_pemilik is None:
            if self._kelas_repo.cari_by_id(filter.id_kelas) is None:
                raise KelasTidakDitemukan(filter.id_kelas)
            ids_pemilik_siswa = [s.id for s in self._student_repo.daftar_by_kelas(filter.id_kelas, hanya_aktif=False)]

        events = self._event_repo.cari(
            sejak=filter.sejak,
            sampai=filter.sampai,
            id_pemilik=id_pemilik,
            jenis_pemilik=jenis_pemilik,
            ids_pemilik_siswa=ids_pemilik_siswa,
            limit=limit,
        )
        return [
            BarisRiwayatScan(event=e, nama=self._cari_nama(e), label=LABEL_JENIS_EVENT.get(e.jenis_event, e.jenis_event.value))
            for e in events
        ]

    def _cari_nama(self, event: AttendanceEvent) -> str | None:
        if event.id_pemilik is None or event.jenis_pemilik is None:
            return None
        if event.jenis_pemilik == JenisPemilik.SISWA:
            orang = self._student_repo.cari_by_id(event.id_pemilik)
        else:
            orang = self._teacher_repo.cari_by_id(event.id_pemilik)
        return orang.nama_lengkap if orang is not None else None
