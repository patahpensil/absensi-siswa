"""Layanan Dashboard (PRD §14.1): ringkasan kehadiran hari berjalan,
grafik harian/mingguan/bulanan, perbandingan kedisiplinan antar kelas,
tren keterlambatan, daftar siswa alpa terbanyak, dan status operasional.
Lapisan integrasi DB (sama pola app.server.laporan_service) - TIDAK
menyentuh app.core.engine/mesin_absensi sama sekali, murni membaca
attendance/attendance_events yang sudah dihitung.

Lingkup peran DIPAKAI ULANG dari Laporan (app.core.laporan.pastikan_berwenang,
lewat app.core.dashboard - PRD §10 punya SATU baris tabel akses "Dashboard
dan laporan") - kepsek/admin bebas, wali kelas HANYA kelasnya sendiri
(auto-scope kalau tidak menyaring eksplisit, pola sama
LaporanService._hasilkan_rekap), operator TIDAK BERWENANG SAMA SEKALI.
"""

from __future__ import annotations

from datetime import date

from app.core.akses_kelas import pastikan_akses_kelas
from app.core.dashboard import (
    Granularitas,
    PerbandinganKelas,
    RingkasanHariIni,
    SiswaAlpaTerbanyak,
    StatusOperasional,
    TitikGrafik,
    alpa_terbanyak_dari_baris,
    grafik_dari_seri,
    pastikan_berwenang,
    perbandingan_kelas_dari_baris,
    ringkasan_dari_seri,
    status_antrean_dari_baris,
    status_jalur_dari_peta,
    status_klien_dari_sesi,
    susun_seri_harian,
    tren_status_dari_seri,
)
from app.core.status import Peran
from app.core.timeutil import now_wita
from app.data.models.system import User
from app.data.repositories.attendance_event import AttendanceEventRepository
from app.data.repositories.dashboard import DashboardRepository
from app.data.repositories.perangkat import TelemetriLobiRepository
from app.data.repositories.session import SesiKlienRepository
from app.data.repositories.user import UserRepository


class WaliKelasBelumDiampu(Exception):
    """Wali kelas belum ditugaskan ke kelas mana pun - tidak ada
    Dashboard yang boleh dilihat (bukan celah "tanpa kelas berarti
    bebas akses"). Definisi LOKAL di sini, sengaja tidak diimpor dari
    app.server.laporan_service - konsepnya sama, tapi kedua modul boleh
    berubah independen tanpa diam-diam saling mempengaruhi (pola sama
    app.core.status.PERAN_GANTI_JALUR_ALAT vs
    PERAN_PERPANJANG_IZIN_DARURAT_ALAT)."""


class DashboardService:
    def __init__(
        self,
        *,
        dashboard_repo: DashboardRepository,
        attendance_event_repo: AttendanceEventRepository,
        sesi_repo: SesiKlienRepository,
        telemetri_repo: TelemetriLobiRepository,
        user_repo: UserRepository,
    ) -> None:
        self._dashboard_repo = dashboard_repo
        self._attendance_event_repo = attendance_event_repo
        self._sesi_repo = sesi_repo
        self._telemetri_repo = telemetri_repo
        self._user_repo = user_repo

    def _id_kelas_efektif(self, *, pelaku: User, id_kelas: int | None) -> int | None:
        """Wali kelas SELALU dibatasi ke kelasnya sendiri: tanpa saring
        eksplisit -> auto-scope ke id_kelas_diampu; dengan saring
        eksplisit -> ditolak kalau bukan kelasnya. Pola sama
        LaporanService._hasilkan_rekap. Kepsek/admin tidak dibatasi -
        id_kelas dikembalikan apa adanya (None berarti seluruh sekolah)."""
        peran = Peran(pelaku.peran.value)
        if peran != Peran.WALI_KELAS:
            return id_kelas
        if pelaku.id_kelas_diampu is None:
            raise WaliKelasBelumDiampu("Akun ini belum ditugaskan ke kelas mana pun")
        if id_kelas is not None:
            pastikan_akses_kelas(peran=peran, id_kelas_diampu=pelaku.id_kelas_diampu, id_kelas_target=id_kelas)
        return pelaku.id_kelas_diampu

    def ringkasan_hari_ini(self, *, pelaku: User, tanggal: date | None = None) -> RingkasanHariIni:
        pastikan_berwenang(Peran(pelaku.peran.value))
        tanggal_efektif = tanggal or now_wita().date()
        id_kelas = self._id_kelas_efektif(pelaku=pelaku, id_kelas=None)
        baris = self._dashboard_repo.hitung_status_per_hari(
            tanggal_mulai=tanggal_efektif, tanggal_selesai=tanggal_efektif, id_kelas=id_kelas
        )
        return ringkasan_dari_seri(susun_seri_harian(baris), tanggal=tanggal_efektif)

    def grafik(
        self,
        *,
        pelaku: User,
        tanggal_mulai: date,
        tanggal_selesai: date,
        granularitas: Granularitas,
        id_kelas: int | None = None,
    ) -> list[TitikGrafik]:
        pastikan_berwenang(Peran(pelaku.peran.value))
        id_kelas_efektif = self._id_kelas_efektif(pelaku=pelaku, id_kelas=id_kelas)
        baris = self._dashboard_repo.hitung_status_per_hari(
            tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_kelas=id_kelas_efektif
        )
        return grafik_dari_seri(susun_seri_harian(baris), granularitas=granularitas)

    def tren_keterlambatan(
        self, *, pelaku: User, tanggal_mulai: date, tanggal_selesai: date, id_kelas: int | None = None
    ) -> list[tuple[date, int]]:
        pastikan_berwenang(Peran(pelaku.peran.value))
        id_kelas_efektif = self._id_kelas_efektif(pelaku=pelaku, id_kelas=id_kelas)
        baris = self._dashboard_repo.hitung_status_per_hari(
            tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_kelas=id_kelas_efektif
        )
        seri = susun_seri_harian(baris)
        return tren_status_dari_seri(
            seri, status="TERLAMBAT", tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai
        )

    def perbandingan_kelas(
        self, *, pelaku: User, tanggal_mulai: date, tanggal_selesai: date
    ) -> list[PerbandinganKelas]:
        """Wali kelas TETAP boleh memanggil ini - hasilnya otomatis
        disaring ke satu baris kelasnya sendiri (perbandingan lintas
        kelas hanya bermakna untuk kepsek/admin, tapi tidak ditolak
        keras, cukup jadi daftar satu baris - tidak ada informasi kelas
        lain yang bocor)."""
        peran = Peran(pelaku.peran.value)
        pastikan_berwenang(peran)
        baris = self._dashboard_repo.hitung_status_per_kelas(
            tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai
        )
        hasil = perbandingan_kelas_dari_baris(baris)
        if peran == Peran.WALI_KELAS:
            if pelaku.id_kelas_diampu is None:
                raise WaliKelasBelumDiampu("Akun ini belum ditugaskan ke kelas mana pun")
            hasil = [h for h in hasil if h.id_kelas == pelaku.id_kelas_diampu]
        return hasil

    def alpa_terbanyak(
        self,
        *,
        pelaku: User,
        tanggal_mulai: date,
        tanggal_selesai: date,
        id_kelas: int | None = None,
        limit: int = 10,
    ) -> list[SiswaAlpaTerbanyak]:
        pastikan_berwenang(Peran(pelaku.peran.value))
        id_kelas_efektif = self._id_kelas_efektif(pelaku=pelaku, id_kelas=id_kelas)
        baris = self._dashboard_repo.daftar_alpa_terbanyak(
            tanggal_mulai=tanggal_mulai, tanggal_selesai=tanggal_selesai, id_kelas=id_kelas_efektif, limit=limit
        )
        return alpa_terbanyak_dari_baris(baris)

    def status_operasional(self, *, pelaku: User) -> StatusOperasional:
        """Sama untuk seluruh peran berwenang (kepsek/admin/wali kelas) -
        ini data infrastruktur sekolah, bukan data siswa bertingkat
        kelas, jadi tidak ada scoping id_kelas di sini."""
        pastikan_berwenang(Peran(pelaku.peran.value))
        sekarang = now_wita()

        peta_jalur = self._attendance_event_repo.waktu_terakhir_per_jalur()
        jalur = status_jalur_dari_peta(peta_jalur, sekarang=sekarang)

        daftar_klien: list[tuple[str, str, date]] = []
        for sesi in self._sesi_repo.daftar_aktif(sekarang=sekarang):
            pengguna = self._user_repo.cari_by_id(sesi.id_pengguna)
            if pengguna is None:
                continue
            daftar_klien.append((pengguna.username, pengguna.peran.value, sesi.terakhir_dipakai))
        klien = status_klien_dari_sesi(daftar_klien, sekarang=sekarang)

        telemetri = self._telemetri_repo.daftar()
        antrean_lobi = status_antrean_dari_baris(
            [(t.station_id, t.jumlah_pending, t.waktu_lapor) for t in telemetri], sekarang=sekarang
        )

        return StatusOperasional(jalur=jalur, klien=klien, antrean_lobi=antrean_lobi, pencadangan_terakhir=None)
