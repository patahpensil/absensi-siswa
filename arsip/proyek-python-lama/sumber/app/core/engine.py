from dataclasses import dataclass
from datetime import date, datetime

from app.config.pengaturan import JadwalHarian, Pengaturan
from app.core.kalender_service import muat_kalender
from app.core.mesin_absensi import (
    BarisAbsensi,
    HasilCutoffPulang,
    ScanMasuk,
    jalankan_cutoff_pulang as _jalankan_cutoff_pulang_murni,
    klasifikasi_scan,
    scan_duplikat,
    tentukan_status_masuk,
    tentukan_status_masuk_guru,
    terapkan_cutoff_alpa,
)
from app.core.status import AlasanFinal, HasilScan, JenisHari, StatusSiswa
from app.data.models.attendance import Attendance, AttendanceEvent, JenisEventScan
from app.data.models.enums import JenisPemilik
from app.data.models.schedule import SchoolSchedule
from app.data.repositories.attendance import AttendanceRepository
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.data.repositories.audit_log import AuditLogRepository
from app.data.repositories.kalender import KalenderRepository
from app.data.repositories.schedule import ScheduleRepository
from app.data.repositories.student import StudentRepository
from app.data.repositories.teacher import TeacherRepository
from app.rfid.base import JalurAlat

__all__ = ["MesinAbsensi", "HasilProsesScan", "jadwal_dari_baris", "ke_baris_absensi"]


@dataclass(frozen=True)
class HasilProsesScan:
    """Hasil satu scan yang sudah diproses.

    event selalu terisi - setiap scan tercatat sebagai peristiwa mentah
    apa pun hasilnya. absensi kosong kalau scan tidak membentuk atau
    mengubah baris attendance (mis. kartu tidak dikenal, duplikat, atau
    di luar periode).
    """

    event: AttendanceEvent
    absensi: Attendance | None


def jadwal_dari_baris(baris: SchoolSchedule) -> JadwalHarian:
    """Petakan satu baris school_schedules ke JadwalHarian (app.config.pengaturan).

    Dipakai di sini dan oleh app/scheduler/ - keduanya perlu menerjemahkan
    jadwal DB ke bentuk yang dipahami fungsi murni app.core.mesin_absensi.
    """
    return JadwalHarian(
        jam_masuk=baris.jam_masuk,
        batas_terlambat=baris.batas_terlambat,
        cutoff_alpa=baris.cutoff_alpa,
        pulang_mulai=baris.pulang_mulai,
        pulang_selesai=baris.jam_pulang,
    )


def ke_baris_absensi(absensi: Attendance) -> BarisAbsensi:
    return BarisAbsensi(
        orang_id=absensi.id_pemilik,
        tanggal=absensi.tanggal,
        status_masuk=StatusSiswa(absensi.status_masuk) if absensi.status_masuk else None,
        status_final=StatusSiswa(absensi.status_final) if absensi.status_final else None,
        jam_masuk=absensi.jam_masuk,
        jam_pulang=absensi.jam_pulang,
        alasan_final=AlasanFinal(absensi.alasan_final) if absensi.alasan_final else None,
        izin_pulang_cepat=absensi.izin_pulang_cepat,
    )


class MesinAbsensi:
    """Mesin absensi: sambungan DB untuk aturan murni di app.core.mesin_absensi.

    Tidak mengimpor apa pun dari app/ui/. Tidak membangun pemicu waktu -
    kapan tetapkan_alpa() dan jalankan_cutoff_pulang() dipanggil adalah
    tanggung jawab app/scheduler/, bukan kelas ini. Seluruh akses data
    lewat repository; tidak ada Session SQLAlchemy yang disentuh langsung
    di sini.

    Keputusan status itu sendiri (batas terlambat, cutoff, dedup, dan
    konversi ALPA pulang) TIDAK dihitung ulang di sini - kelas ini hanya
    memetakan baris database ke/dari struktur murni app.core.mesin_absensi
    dan memanggil fungsinya. Itu supaya aturan yang sama persis dengan yang
    diuji di tests/test_mesin_absensi.py yang benar-benar jalan di produksi.

    Jenis hari (EFEKTIF/LIBUR/dst) dibaca lewat kalender akademik dua jalur
    siswa/guru (app.core.kalender, disambungkan lewat _jenis_hari_siswa()
    di bawah) - siswa dan guru bisa berbeda jenis hari pada tanggal yang
    sama (SKILL.md "Kalender akademik"). Mesin absensi ini hanya pernah
    butuh jalur siswa (cutoff ALPA pagi, cutoff pulang, klasifikasi scan
    siswa); guru tidak punya pemeriksaan hari libur sama sekali (SKILL.md
    tidak memintanya - guru bisa bertugas kapan pun).
    """

    def __init__(
        self,
        *,
        attendance_repo: AttendanceRepository,
        attendance_event_repo: AttendanceEventRepository,
        schedule_repo: ScheduleRepository,
        kalender_repo: KalenderRepository,
        student_repo: StudentRepository,
        teacher_repo: TeacherRepository,
        audit_repo: AuditLogRepository,
        pengaturan: Pengaturan,
    ) -> None:
        self._attendance_repo = attendance_repo
        self._attendance_event_repo = attendance_event_repo
        self._schedule_repo = schedule_repo
        self._kalender_repo = kalender_repo
        self._student_repo = student_repo
        self._teacher_repo = teacher_repo
        self._audit_repo = audit_repo
        self._pengaturan = pengaturan

    # ------------------------------------------------------------------
    # Penentuan status dari scan
    # ------------------------------------------------------------------

    def proses_scan(
        self,
        *,
        uid_kartu: str,
        id_pemilik: int | None,
        jenis_pemilik: JenisPemilik | None,
        waktu_scan: datetime,
        uid_mentah: str | None = None,
        id_perangkat: int | None = None,
        metadata_json: dict | None = None,
        jalur: JalurAlat | None = None,
    ) -> HasilProsesScan:
        """Proses satu scan mentah dari app/rfid/.

        uid_kartu yang belum diketahui pemiliknya (id_pemilik kosong)
        dicatat sebagai peristiwa mentah saja. Duplikat dideteksi DI SINI,
        bukan di lapisan pembaca - lihat app.core.mesin_absensi.scan_duplikat
        dan catatan di app/rfid/base.py: perbandingannya global per UID di
        seluruh jalur, bukan per pembaca.

        uid_mentah dan jalur (PRD §17) murni diteruskan apa adanya ke
        setiap attendance_event_repo.buat() di bawah - tidak pernah dibaca
        atau dibandingkan untuk keputusan apa pun di sini; keputusan status
        selalu memakai uid_kartu (baku) dan waktu_scan, persis seperti
        sebelum parameter ini ada.
        """
        if id_pemilik is None or jenis_pemilik is None:
            peristiwa = self._attendance_event_repo.buat(
                uid_kartu=uid_kartu,
                uid_mentah=uid_mentah,
                jenis_event=JenisEventScan.UNKNOWN_CARD,
                waktu_scan=waktu_scan,
                id_perangkat=id_perangkat,
                metadata_json=metadata_json,
                jalur=jalur,
            )
            return HasilProsesScan(event=peristiwa, absensi=None)

        waktu_scan_sebelumnya = self._attendance_event_repo.waktu_scan_terakhir(uid_kartu)

        if jenis_pemilik == JenisPemilik.GURU:
            if scan_duplikat(waktu_scan, waktu_scan_sebelumnya, self._pengaturan):
                peristiwa = self._attendance_event_repo.buat(
                    uid_kartu=uid_kartu,
                    uid_mentah=uid_mentah,
                    id_pemilik=id_pemilik,
                    jenis_pemilik=jenis_pemilik,
                    jenis_event=JenisEventScan.DUPLICATE,
                    waktu_scan=waktu_scan,
                    id_perangkat=id_perangkat,
                    metadata_json=metadata_json,
                    jalur=jalur,
                )
                return HasilProsesScan(event=peristiwa, absensi=None)
            return self._proses_scan_guru(
                uid_kartu=uid_kartu,
                id_pemilik=id_pemilik,
                waktu_scan=waktu_scan,
                uid_mentah=uid_mentah,
                id_perangkat=id_perangkat,
                metadata_json=metadata_json,
                jalur=jalur,
            )

        return self._proses_scan_siswa(
            uid_kartu=uid_kartu,
            id_pemilik=id_pemilik,
            waktu_scan=waktu_scan,
            uid_mentah=uid_mentah,
            id_perangkat=id_perangkat,
            metadata_json=metadata_json,
            waktu_scan_sebelumnya=waktu_scan_sebelumnya,
            jalur=jalur,
        )

    def _proses_scan_siswa(
        self,
        *,
        uid_kartu: str,
        id_pemilik: int,
        waktu_scan: datetime,
        id_perangkat: int | None,
        metadata_json: dict | None,
        waktu_scan_sebelumnya: datetime | None,
        uid_mentah: str | None = None,
        jalur: JalurAlat | None = None,
    ) -> HasilProsesScan:
        tanggal = waktu_scan.date()
        hari = tanggal.isoweekday()
        baris_jadwal = self._schedule_repo.jadwal_berlaku(jenis_pemilik=JenisPemilik.SISWA, hari=hari, tanggal=tanggal)
        jadwal = jadwal_dari_baris(baris_jadwal) if baris_jadwal is not None else None
        jenis_hari = self._jenis_hari_siswa(tanggal)

        absensi_ada = self._attendance_repo.cari_by_pemilik_tanggal(
            id_pemilik=id_pemilik, jenis_pemilik=JenisPemilik.SISWA, tanggal=tanggal
        )
        sudah_masuk = absensi_ada is not None and absensi_ada.jam_masuk is not None

        scan = ScanMasuk(
            orang_id=id_pemilik,
            waktu=waktu_scan,
            station_id=str(id_perangkat) if id_perangkat is not None else "",
        )
        hasil = klasifikasi_scan(scan, jadwal, jenis_hari, sudah_masuk, waktu_scan_sebelumnya, self._pengaturan)

        if hasil == HasilScan.DUPLIKAT:
            peristiwa = self._attendance_event_repo.buat(
                uid_kartu=uid_kartu,
                uid_mentah=uid_mentah,
                id_pemilik=id_pemilik,
                jenis_pemilik=JenisPemilik.SISWA,
                jenis_event=JenisEventScan.DUPLICATE,
                waktu_scan=waktu_scan,
                id_perangkat=id_perangkat,
                metadata_json=metadata_json,
                jalur=jalur,
            )
            return HasilProsesScan(event=peristiwa, absensi=absensi_ada)

        if hasil in (HasilScan.HARI_LIBUR, HasilScan.DI_LUAR_JENDELA):
            peristiwa = self._attendance_event_repo.buat(
                uid_kartu=uid_kartu,
                uid_mentah=uid_mentah,
                id_pemilik=id_pemilik,
                jenis_pemilik=JenisPemilik.SISWA,
                jenis_event=JenisEventScan.DI_LUAR_PERIODE,
                waktu_scan=waktu_scan,
                id_perangkat=id_perangkat,
                metadata_json=metadata_json,
                jalur=jalur,
            )
            return HasilProsesScan(event=peristiwa, absensi=absensi_ada)

        if hasil == HasilScan.ABSEN_PULANG:
            peristiwa = self._attendance_event_repo.buat(
                uid_kartu=uid_kartu,
                uid_mentah=uid_mentah,
                id_pemilik=id_pemilik,
                jenis_pemilik=JenisPemilik.SISWA,
                jenis_event=JenisEventScan.PULANG,
                waktu_scan=waktu_scan,
                id_perangkat=id_perangkat,
                metadata_json=metadata_json,
                jalur=jalur,
            )
            if absensi_ada.jam_pulang is None:
                absensi_ada = self._attendance_repo.catat_pulang(absensi_ada.id, waktu_scan)
            return HasilProsesScan(event=peristiwa, absensi=absensi_ada)

        # HasilScan.ABSEN_MASUK - scan kedua dst. di hari yang sama tidak
        # mengubah status yang sudah ditetapkan, hanya tercatat sebagai
        # peristiwa tambahan.
        peristiwa = self._attendance_event_repo.buat(
            uid_kartu=uid_kartu,
            uid_mentah=uid_mentah,
            id_pemilik=id_pemilik,
            jenis_pemilik=JenisPemilik.SISWA,
            jenis_event=JenisEventScan.MASUK,
            waktu_scan=waktu_scan,
            id_perangkat=id_perangkat,
            metadata_json=metadata_json,
            jalur=jalur,
        )
        if absensi_ada is not None:
            return HasilProsesScan(event=peristiwa, absensi=absensi_ada)

        status = tentukan_status_masuk(waktu_scan, jadwal)
        absensi_baru = self._attendance_repo.tambah(
            id_pemilik=id_pemilik,
            jenis_pemilik=JenisPemilik.SISWA,
            tanggal=tanggal,
            status_masuk=status.value,
            jam_masuk=waktu_scan,
            id_jadwal=baris_jadwal.id if baris_jadwal is not None else None,
        )
        return HasilProsesScan(event=peristiwa, absensi=absensi_baru)

    def _proses_scan_guru(
        self,
        *,
        uid_kartu: str,
        id_pemilik: int,
        waktu_scan: datetime,
        id_perangkat: int | None,
        metadata_json: dict | None,
        uid_mentah: str | None = None,
        jalur: JalurAlat | None = None,
    ) -> HasilProsesScan:
        """Guru tidak punya periode masuk/pulang seperti siswa - hanya satu
        titik pembanding (toleransi guru), berlaku kapan pun scan pertama
        hari itu terjadi. Guru tidak mengenal ALPA (lihat SKILL.md).
        """
        tanggal = waktu_scan.date()

        peristiwa = self._attendance_event_repo.buat(
            uid_kartu=uid_kartu,
            uid_mentah=uid_mentah,
            id_pemilik=id_pemilik,
            jenis_pemilik=JenisPemilik.GURU,
            jenis_event=JenisEventScan.MASUK,
            waktu_scan=waktu_scan,
            id_perangkat=id_perangkat,
            metadata_json=metadata_json,
            jalur=jalur,
        )

        absensi_ada = self._attendance_repo.cari_by_pemilik_tanggal(
            id_pemilik=id_pemilik, jenis_pemilik=JenisPemilik.GURU, tanggal=tanggal
        )
        if absensi_ada is not None:
            return HasilProsesScan(event=peristiwa, absensi=absensi_ada)

        status = tentukan_status_masuk_guru(waktu_scan, self._pengaturan)
        absensi_baru = self._attendance_repo.tambah(
            id_pemilik=id_pemilik,
            jenis_pemilik=JenisPemilik.GURU,
            tanggal=tanggal,
            status_masuk=status.value,
            jam_masuk=waktu_scan,
        )
        return HasilProsesScan(event=peristiwa, absensi=absensi_baru)

    # ------------------------------------------------------------------
    # Cutoff ALPA pagi (siswa)
    # ------------------------------------------------------------------

    def tetapkan_alpa(self, tanggal: date) -> list[Attendance]:
        """Tetapkan ALPA untuk siswa aktif yang belum punya baris attendance
        pada tanggal ini. Dipanggil oleh app/scheduler/ pada waktu cutoff
        (07:30) - fungsi ini sendiri tidak tahu jam berapa sekarang.

        Siswa yang statusnya sudah manual (IZIN/SAKIT/DISPENSASI) sudah
        punya baris attendance sebelum cutoff, jadi otomatis dikecualikan
        lewat pengecekan "sudah_ada" di bawah sebelum baris murni
        (BarisAbsensi status_masuk=None) dibentuk - jadi pengecekan
        STATUS_MANUAL_SISWA di app.core.mesin_absensi.terapkan_cutoff_alpa
        tidak pernah perlu bercabang di sini, tapi fungsinya tetap yang
        benar-benar dipanggil untuk menentukan nilai ALPA-nya.

        Hari libur, atau tanggal yang bukan hari sekolah sama sekali,
        tidak menghasilkan ALPA untuk siapa pun.
        """
        jenis_hari = self._jenis_hari_siswa(tanggal)
        if jenis_hari != JenisHari.EFEKTIF:
            return []

        hari = tanggal.isoweekday()
        jadwal = self._schedule_repo.jadwal_berlaku(jenis_pemilik=JenisPemilik.SISWA, hari=hari, tanggal=tanggal)
        if jadwal is None:
            return []

        sudah_ada = {
            a.id_pemilik
            for a in self._attendance_repo.daftar_by_tanggal(tanggal=tanggal, jenis_pemilik=JenisPemilik.SISWA)
        }

        hasil: list[Attendance] = []
        for siswa in self._student_repo.daftar_aktif():
            if siswa.id in sudah_ada:
                continue
            baris = terapkan_cutoff_alpa(BarisAbsensi(orang_id=siswa.id, tanggal=tanggal), jenis_hari)
            absensi = self._attendance_repo.tambah(
                id_pemilik=siswa.id,
                jenis_pemilik=JenisPemilik.SISWA,
                tanggal=tanggal,
                status_masuk=baris.status_masuk.value,
                id_jadwal=jadwal.id,
            )
            hasil.append(absensi)
        return hasil

    # ------------------------------------------------------------------
    # Cutoff pulang - konversi ALPA karena tidak scan pulang
    # ------------------------------------------------------------------

    def jalankan_cutoff_pulang(self, tanggal: date, *, paksa: bool = False) -> HasilCutoffPulang:
        """Konversi status_final jadi ALPA untuk siswa yang tidak scan
        pulang sampai cutoff. Dipanggil app/scheduler/ pada jam cutoff
        pulang - lihat app.core.mesin_absensi.jalankan_cutoff_pulang untuk
        aturannya (ambang 30%, pengecualian izin/sakit/dispensasi dan izin
        pulang cepat).

        Menggantikan Tutup Hari/BOLOS lama - lihat SKILL.md, "Absensi
        pulang dan ALPA pulang". Tidak ada lagi larangan menjalankan dua
        kali untuk tanggal yang sama; memanggil ulang untuk tanggal yang
        sudah dikonversi aman (baris yang sudah ALPA dikecualikan oleh
        app.core.mesin_absensi.boleh_dikonversi_alpa_pulang).
        """
        if self._jenis_hari_siswa(tanggal) != JenisHari.EFEKTIF:
            return HasilCutoffPulang(jumlah_sasaran=0, jumlah_hadir=0, proporsi=0.0, ditahan=False)

        baris_db = self._attendance_repo.daftar_by_tanggal(tanggal=tanggal, jenis_pemilik=JenisPemilik.SISWA)
        daftar = [ke_baris_absensi(b) for b in baris_db]
        _, hasil = _jalankan_cutoff_pulang_murni(daftar, self._pengaturan, paksa=paksa)

        dikonversi = 0
        for baris_murni, baris_row in zip(daftar, baris_db):
            sudah_dikonversi_di_db = baris_row.alasan_final == AlasanFinal.TIDAK_SCAN_PULANG.value
            if baris_murni.alasan_final == AlasanFinal.TIDAK_SCAN_PULANG and not sudah_dikonversi_di_db:
                self._attendance_repo.konversi_alpa_pulang(
                    baris_row.id,
                    status_final=StatusSiswa.ALPA.value,
                    alasan_final=AlasanFinal.TIDAK_SCAN_PULANG.value,
                )
                dikonversi += 1

        self._audit_repo.catat(
            id_pengguna=None,
            aksi="cutoff_pulang",
            nilai_sebelum=None,
            nilai_sesudah={
                "tanggal": tanggal.isoformat(),
                "jumlah_sasaran": hasil.jumlah_sasaran,
                "jumlah_hadir": hasil.jumlah_hadir,
                "proporsi": hasil.proporsi,
                "ditahan": hasil.ditahan,
                "dipaksa": paksa,
                "jumlah_dikonversi": dikonversi,
            },
            alasan=(
                "Cutoff pulang ditahan - proporsi tidak scan pulang melebihi ambang"
                if hasil.ditahan and not paksa
                else "Cutoff pulang otomatis - tidak scan pulang dikonversi ALPA"
            ),
        )
        return hasil

    # ------------------------------------------------------------------
    # Kalender akademik (jalur siswa - lihat catatan di docstring kelas)
    # ------------------------------------------------------------------

    def _jenis_hari_siswa(self, tanggal: date) -> JenisHari:
        return muat_kalender(self._kalender_repo).jenis_hari_siswa(tanggal)
