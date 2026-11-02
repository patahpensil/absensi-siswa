"""Aturan murni modul Dashboard (PRD §14.1): kewenangan lihat, bucketing
grafik harian/mingguan/bulanan, dan penilaian status operasional (jalur/
klien/antrean diam atau tidak).

Fungsi murni - tidak menyentuh basis data maupun app.core.engine/
mesin_absensi. Siswa SAJA untuk seluruh bagian kehadiran (grafik,
perbandingan kelas, alpa terbanyak) - "per kelas"/ALPA tidak berlaku
untuk guru, sama seperti app.core.laporan.

Kewenangan lihat Dashboard DIPAKAI ULANG dari app.core.laporan, BUKAN
diduplikasi - SKILL.md punya SATU baris tabel akses "Dashboard dan
laporan" yang menaungi keduanya sekaligus (PRD §10), jadi ini benar-benar
aturan yang sama, bukan kebetulan sama nilai (beda dari kasus
PERAN_GANTI_JALUR_ALAT/PERAN_PERPANJANG_IZIN_DARURAT_ALAT di
app.core.status yang sengaja dipisah walau saat ini identik).
"""

from __future__ import annotations

from dataclasses import dataclass
from datetime import date, datetime, timedelta
from enum import Enum

from app.core.laporan import AksesLaporanDitolak, pastikan_berwenang  # noqa: F401 - re-ekspor sengaja
from app.rfid.base import JalurAlat

LABEL_BELUM_DITEMPATKAN = "Belum ditempatkan"

#: SKILL.md "Pemantauan kesehatan alat": "peringatan bila satu jalur diam
#: lebih dari 15 menit di jam sibuk". Dipakai juga untuk status klien -
#: ambang yang sama, alasan yang sama (tidak ada laporan/aktivitas berarti
#: kemungkinan alat/PC berhenti bekerja).
AMBANG_DIAM_MENIT = 15
#: Klien lobi melapor tiap 15 detik selagi berjalan (lihat
#: app.ui.lobi.layar, _INTERVAL_TIMER_TELEMETRI_MS) - lima menit tanpa
#: laporan berarti klien itu sendiri sungguhan berhenti (proses mati,
#: bukan sekadar belum sempat lapor putaran berikutnya).
AMBANG_KEDALUWARSA_TELEMETRI_MENIT = 5


class Granularitas(str, Enum):
    HARIAN = "HARIAN"
    MINGGUAN = "MINGGUAN"
    BULANAN = "BULANAN"


#: tanggal -> {status_final: jumlah}. Bahan bersama grafik dan tren -
#: SATU query (DashboardRepository.hitung_status_per_hari) menghasilkan
#: bentuk ini, seluruh granularitas/tren dibangun darinya tanpa query
#: tambahan.
SeriHarian = dict[date, dict[str, int]]


def susun_seri_harian(baris: list[tuple[date, str | None, int]]) -> SeriHarian:
    """status_final None (baris belum sempat dihitung statusnya) DIABAIKAN
    - ringkasan/grafik hanya bicara status yang sudah dikenal, sama
    seperti app.core.laporan.hitung_rekap_siswa yang mengabaikan status di
    luar StatusSiswa."""
    seri: SeriHarian = {}
    for tanggal, status, jumlah in baris:
        if status is None:
            continue
        seri.setdefault(tanggal, {})[status] = jumlah
    return seri


def _awal_minggu(tanggal: date) -> date:
    return tanggal - timedelta(days=tanggal.weekday())  # Senin


def _jumlahkan(tujuan: dict[str, int], sumber: dict[str, int]) -> None:
    for status, jumlah in sumber.items():
        tujuan[status] = tujuan.get(status, 0) + jumlah


@dataclass(frozen=True)
class TitikGrafik:
    """``periode``: "2026-09-11" (harian), "2026-09-07" (Senin awal
    minggu itu, mingguan), atau "2026-09" (bulanan)."""

    periode: str
    per_status: dict[str, int]


def grafik_dari_seri(seri: SeriHarian, *, granularitas: Granularitas) -> list[TitikGrafik]:
    """Kelompokkan seri harian jadi titik grafik - murni, tanpa query
    tambahan untuk mingguan/bulanan (ketiga granularitas dibangun dari
    SATU seri harian yang sama)."""
    kelompok: dict[str, dict[str, int]] = {}
    for tanggal, per_status in seri.items():
        if granularitas is Granularitas.HARIAN:
            kunci = tanggal.isoformat()
        elif granularitas is Granularitas.MINGGUAN:
            kunci = _awal_minggu(tanggal).isoformat()
        elif granularitas is Granularitas.BULANAN:
            kunci = tanggal.strftime("%Y-%m")
        else:
            raise ValueError(f"Granularitas tidak dikenal: {granularitas}")
        _jumlahkan(kelompok.setdefault(kunci, {}), per_status)

    return [TitikGrafik(periode=periode, per_status=kelompok[periode]) for periode in sorted(kelompok)]


def tren_status_dari_seri(
    seri: SeriHarian, *, status: str, tanggal_mulai: date, tanggal_selesai: date
) -> list[tuple[date, int]]:
    """Deret harian SATU status dari tanggal_mulai s.d. tanggal_selesai -
    hari tanpa baris attendance sama sekali (mis. libur) tetap tampil
    dengan nilai 0, bukan hilang dari grafik (garis tren tidak boleh
    berlubang diam-diam)."""
    hasil: list[tuple[date, int]] = []
    hari = tanggal_mulai
    while hari <= tanggal_selesai:
        hasil.append((hari, seri.get(hari, {}).get(status, 0)))
        hari += timedelta(days=1)
    return hasil


@dataclass(frozen=True)
class RingkasanHariIni:
    tanggal: date
    per_status: dict[str, int]
    total_tercatat: int


def ringkasan_dari_seri(seri: SeriHarian, *, tanggal: date) -> RingkasanHariIni:
    per_status = dict(seri.get(tanggal, {}))
    return RingkasanHariIni(tanggal=tanggal, per_status=per_status, total_tercatat=sum(per_status.values()))


@dataclass(frozen=True)
class PerbandinganKelas:
    id_kelas: int | None
    nama_kelas: str
    per_status: dict[str, int]
    total_tercatat: int


def perbandingan_kelas_dari_baris(
    baris: list[tuple[int | None, str | None, str | None, int]],
) -> list[PerbandinganKelas]:
    per_kelas: dict[int | None, tuple[str, dict[str, int]]] = {}
    for id_kelas, nama_kelas, status, jumlah in baris:
        if status is None:
            continue
        label = nama_kelas or LABEL_BELUM_DITEMPATKAN
        if id_kelas not in per_kelas:
            per_kelas[id_kelas] = (label, {})
        per_kelas[id_kelas][1][status] = per_kelas[id_kelas][1].get(status, 0) + jumlah

    hasil = [
        PerbandinganKelas(id_kelas=k, nama_kelas=label, per_status=ps, total_tercatat=sum(ps.values()))
        for k, (label, ps) in per_kelas.items()
    ]
    hasil.sort(key=lambda p: p.nama_kelas)
    return hasil


@dataclass(frozen=True)
class SiswaAlpaTerbanyak:
    id_siswa: int
    nama_lengkap: str
    nama_kelas: str
    jumlah_alpa: int


def alpa_terbanyak_dari_baris(baris: list[tuple[int, str, str | None, int]]) -> list[SiswaAlpaTerbanyak]:
    """Urutan DESC sudah dijamin oleh DashboardRepository.daftar_alpa_terbanyak
    (ORDER BY di SQL) - fungsi ini murni transformasi bentuk, tidak
    mengurutkan ulang."""
    return [
        SiswaAlpaTerbanyak(
            id_siswa=id_siswa, nama_lengkap=nama, nama_kelas=nama_kelas or LABEL_BELUM_DITEMPATKAN, jumlah_alpa=jumlah
        )
        for id_siswa, nama, nama_kelas, jumlah in baris
    ]


# --------------------------------------------------------------- status operasional


@dataclass(frozen=True)
class StatusJalur:
    jalur: str
    waktu_terakhir_baca: datetime
    diam: bool


def status_jalur_dari_peta(peta: dict[JalurAlat, datetime], *, sekarang: datetime) -> list[StatusJalur]:
    """Hanya jalur yang PERNAH membaca sesuatu yang muncul (bukan
    keempat JalurAlat selalu ditampilkan) - sekolah pada praktiknya
    hanya memakai satu jalur terkunci (PRD §12.4), menampilkan tiga
    jalur lain yang tidak pernah dipakai hanya bikin bising."""
    hasil = [
        StatusJalur(
            jalur=jalur.value, waktu_terakhir_baca=waktu, diam=(sekarang - waktu) > timedelta(minutes=AMBANG_DIAM_MENIT)
        )
        for jalur, waktu in peta.items()
    ]
    hasil.sort(key=lambda s: s.jalur)
    return hasil


@dataclass(frozen=True)
class StatusKlien:
    username: str
    peran: str
    terakhir_aktif: datetime
    diam: bool


def status_klien_dari_sesi(daftar: list[tuple[str, str, datetime]], *, sekarang: datetime) -> list[StatusKlien]:
    """``daftar``: (username, peran, terakhir_dipakai) dari sesi aktif
    (SesiKlienRepository.daftar_aktif). Ini proksi AKTIVITAS TERAKHIR,
    bukan status koneksi jaringan real-time - lihat docstring
    daftar_aktif() untuk kenapa ini batasnya."""
    return [
        StatusKlien(username=u, peran=p, terakhir_aktif=t, diam=(sekarang - t) > timedelta(minutes=AMBANG_DIAM_MENIT))
        for u, p, t in daftar
    ]


@dataclass(frozen=True)
class StatusAntreanLobi:
    station_id: str
    jumlah_pending: int
    waktu_lapor: datetime
    kedaluwarsa: bool


def status_antrean_dari_baris(
    baris: list[tuple[str, int, datetime]], *, sekarang: datetime
) -> list[StatusAntreanLobi]:
    return [
        StatusAntreanLobi(
            station_id=s,
            jumlah_pending=j,
            waktu_lapor=w,
            kedaluwarsa=(sekarang - w) > timedelta(minutes=AMBANG_KEDALUWARSA_TELEMETRI_MENIT),
        )
        for s, j, w in baris
    ]


@dataclass(frozen=True)
class StatusOperasional:
    jalur: list[StatusJalur]
    klien: list[StatusKlien]
    antrean_lobi: list[StatusAntreanLobi]
    #: SELALU None sampai Slice 14 (Pencadangan) dibangun - lihat README.
    #: TIDAK PERNAH dikarang/dipalsukan.
    pencadangan_terakhir: datetime | None
