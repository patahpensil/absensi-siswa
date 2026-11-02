from datetime import date, datetime
from typing import Any

from sqlalchemy import select

from app.core.timeutil import now_wita
from app.data.models.attendance import Attendance
from app.data.models.enums import JenisPemilik
from app.data.models.system import AuditLog
from app.data.repositories.audit_log import pastikan_alasan_terisi
from app.data.repositories.base import BaseRepository


class AttendanceNotFoundError(LookupError):
    pass


def _jsonable(nilai: dict[str, Any]) -> dict[str, Any]:
    """Ubah nilai date/datetime jadi string supaya bisa disimpan di kolom JSON."""
    return {kunci: (v.isoformat() if hasattr(v, "isoformat") else v) for kunci, v in nilai.items()}


class AttendanceRepository(BaseRepository):
    """Akses data untuk attendance (hasil olahan, satu baris per orang per
    tanggal). Tidak ada perhitungan status atau cutoff di sini - itu
    tanggung jawab mesin absensi di app/core/.
    """

    def tambah(
        self,
        *,
        id_pemilik: int,
        jenis_pemilik: JenisPemilik,
        tanggal: date,
        status_masuk: str | None,
        jam_masuk: datetime | None = None,
        jam_pulang: datetime | None = None,
        id_jadwal: int | None = None,
    ) -> Attendance:
        """status_masuk None berarti belum pernah scan sama sekali - dipakai
        app.server.koreksi_satuan_service untuk input manual IZIN/SAKIT/
        DISPENSASI (PRD §10) pada tanggal yang belum punya baris attendance
        apa pun. status_final ikut None di sini (sama seperti status_masuk,
        lihat bawah) - pemanggil mengoreksinya lewat koreksi() segera
        setelahnya, tidak pernah dibiarkan None di baris yang sudah final."""
        absensi = Attendance(
            id_pemilik=id_pemilik,
            jenis_pemilik=jenis_pemilik,
            tanggal=tanggal,
            status_masuk=status_masuk,
            status_final=status_masuk,
            jam_masuk=jam_masuk,
            jam_pulang=jam_pulang,
            id_jadwal=id_jadwal,
            dikoreksi=False,
        )
        self._session.add(absensi)
        self._commit()
        return absensi

    def cari_by_id(self, id_absensi: int) -> Attendance | None:
        return self._session.get(Attendance, id_absensi)

    def cari_by_pemilik_tanggal(
        self, *, id_pemilik: int, jenis_pemilik: JenisPemilik, tanggal: date
    ) -> Attendance | None:
        stmt = select(Attendance).where(
            Attendance.id_pemilik == id_pemilik,
            Attendance.jenis_pemilik == jenis_pemilik,
            Attendance.tanggal == tanggal,
        )
        return self._session.execute(stmt).scalar_one_or_none()

    def catat_pulang(self, id_absensi: int, jam_pulang: datetime) -> Attendance:
        absensi = self._session.get(Attendance, id_absensi)
        if absensi is None:
            raise AttendanceNotFoundError(id_absensi)
        absensi.jam_pulang = jam_pulang
        self._commit()
        return absensi

    def daftar_by_tanggal(self, *, tanggal: date, jenis_pemilik: JenisPemilik) -> list[Attendance]:
        """Seluruh baris attendance pada satu tanggal untuk satu jenis pemilik.

        Dipakai untuk menentukan siapa yang SUDAH punya baris (jadi tidak
        perlu ALPA/TIDAK_HADIR) - keputusan siapa yang belum diambil di
        app/core/, bukan di sini.
        """
        stmt = select(Attendance).where(
            Attendance.tanggal == tanggal,
            Attendance.jenis_pemilik == jenis_pemilik,
        )
        return list(self._session.execute(stmt).scalars())

    def konversi_alpa_pulang(self, id_absensi: int, *, status_final: str, alasan_final: str) -> Attendance:
        """Ubah status_final menjadi ALPA karena tidak scan pulang sampai
        cutoff (lihat app.core.mesin_absensi.konversi_alpa_pulang).
        status_masuk TIDAK disentuh - baris ini tetap tercatat HADIR/
        TERLAMBAT paginya, hanya status resmi laporannya yang berubah.
        """
        absensi = self._session.get(Attendance, id_absensi)
        if absensi is None:
            raise AttendanceNotFoundError(id_absensi)
        absensi.status_final = status_final
        absensi.alasan_final = alasan_final
        self._commit()
        return absensi

    def terapkan_koreksi_massal(self, id_absensi: int, perubahan: dict[str, Any]) -> Attendance:
        """Terapkan perubahan kolom untuk SATU baris sebagai bagian dari
        koreksi massal (app.core.koreksi_massal_service).

        Beda dari koreksi(): tidak menulis audit_logs per baris (itu akan
        jadi ribuan entri untuk satu tindakan massal, lihat SKILL.md
        "Koreksi massal") dan TIDAK commit di sini - pemanggil menuliskan
        detail per baris ke tindakan_massal_baris dan satu entri audit_logs
        gabungan, baru commit sekali untuk semuanya.
        """
        absensi = self._session.get(Attendance, id_absensi)
        if absensi is None:
            raise AttendanceNotFoundError(id_absensi)
        for kolom, nilai_baru in perubahan.items():
            setattr(absensi, kolom, nilai_baru)
        absensi.dikoreksi = True
        return absensi

    def koreksi(
        self,
        id_absensi: int,
        *,
        perubahan: dict[str, Any],
        id_pengguna: int,
        alasan: str,
    ) -> Attendance:
        """Koreksi manual satu atau beberapa kolom pada baris attendance.

        Alasan wajib diisi - divalidasi lewat jalur yang sama dengan
        AuditLogRepository.catat(), sebelum baris attendance disentuh sama
        sekali. Kalau alasan kosong, koreksi gagal di sini, bukan cuma di
        UI. Mutasi baris attendance dan baris audit log ditulis dalam satu
        transaksi (satu commit), jadi keduanya selalu selaras.
        """
        alasan = pastikan_alasan_terisi(alasan)

        absensi = self._session.get(Attendance, id_absensi)
        if absensi is None:
            raise AttendanceNotFoundError(id_absensi)

        nilai_sebelum = {kolom: getattr(absensi, kolom) for kolom in perubahan}
        for kolom, nilai_baru in perubahan.items():
            setattr(absensi, kolom, nilai_baru)
        absensi.dikoreksi = True

        self._session.add(
            AuditLog(
                id_pengguna=id_pengguna,
                aksi="koreksi_absensi",
                nilai_sebelum=_jsonable(nilai_sebelum),
                nilai_sesudah=_jsonable(perubahan),
                alasan=alasan,
                waktu=now_wita(),
            )
        )
        self._commit()
        return absensi
