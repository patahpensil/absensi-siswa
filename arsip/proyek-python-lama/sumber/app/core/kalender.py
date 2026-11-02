"""
Kalender akademik.

Dua jalur terpisah: kalender siswa dan kalender guru. Satu tanggal bisa LIBUR
bagi siswa sekaligus EFEKTIF bagi guru — contoh nyata dari kalender sekolah,
14-19 Desember 2026, pengolahan dan penandatanganan nilai LHBPD.

Tanggal yang tidak diatur otomatis dianggap EFEKTIF, dan Minggu selalu libur.
Jadi admin hanya perlu memasukkan pengecualiannya — sekitar sepuluh entri per
semester, kebanyakan berupa rentang.

Pengelolaan kalender adalah kewenangan admin dan kepala sekolah, setara dengan
koreksi massal, dan perubahannya diperlakukan sebagai tindakan massal:
tercatat di log audit, punya pratinjau dampak, dan bisa dibatalkan.
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import date, timedelta

from app.config.pengaturan import MINGGU
from app.core.status import HARI_TANPA_ABSENSI, JenisHari


@dataclass(frozen=True)
class EntriKalender:
    """Satu agenda kalender, berlaku untuk satu tanggal atau satu rentang."""

    mulai: date
    selesai: date
    keterangan: str
    jenis_siswa: JenisHari = JenisHari.EFEKTIF
    jenis_guru: JenisHari = JenisHari.EFEKTIF
    tahun_ajaran: str = ""

    def mencakup(self, tanggal: date) -> bool:
        return self.mulai <= tanggal <= self.selesai

    def daftar_tanggal(self) -> list[date]:
        jumlah = (self.selesai - self.mulai).days + 1
        return [self.mulai + timedelta(days=i) for i in range(jumlah)]


@dataclass
class Kalender:
    """Kumpulan entri untuk satu atau beberapa tahun ajaran.

    Beberapa tahun ajaran boleh tersimpan bersamaan. Yang berlaku ditentukan
    oleh tanggalnya, bukan oleh mana yang terakhir diisi — sehingga menyiapkan
    kalender tahun depan tidak mengganggu absensi yang sedang berjalan.
    """

    entri: list[EntriKalender] = field(default_factory=list)

    def _cari(self, tanggal: date) -> EntriKalender | None:
        # Entri yang lebih spesifik (rentang lebih pendek) menang, supaya
        # libur mendadak satu hari bisa menimpa rentang libur semester.
        cocok = [e for e in self.entri if e.mencakup(tanggal)]
        if not cocok:
            return None
        return min(cocok, key=lambda e: (e.selesai - e.mulai).days)

    def jenis_hari_siswa(self, tanggal: date) -> JenisHari:
        if tanggal.weekday() == MINGGU:
            return JenisHari.LIBUR_NASIONAL
        entri = self._cari(tanggal)
        return entri.jenis_siswa if entri else JenisHari.EFEKTIF

    def jenis_hari_guru(self, tanggal: date) -> JenisHari:
        if tanggal.weekday() == MINGGU:
            return JenisHari.LIBUR_NASIONAL
        entri = self._cari(tanggal)
        return entri.jenis_guru if entri else JenisHari.EFEKTIF

    def hari_efektif_siswa(self, mulai: date, selesai: date) -> list[date]:
        jumlah = (selesai - mulai).days + 1
        semua = (mulai + timedelta(days=i) for i in range(jumlah))
        return [
            t
            for t in semua
            if self.jenis_hari_siswa(t) not in HARI_TANPA_ABSENSI
        ]

    def tambah(self, entri: EntriKalender) -> None:
        self.entri.append(entri)

    # -- perubahan berlaku surut ------------------------------------------

    def dampak_perubahan(
        self, entri_baru: EntriKalender, jumlah_orang: int
    ) -> dict:
        """Hitung dampak sebuah perubahan sebelum disimpan.

        Dipakai untuk pratinjau: "Perubahan ini memengaruhi 6 hari dan 6.000
        baris absensi. Hitung ulang statusnya?"
        """
        tanggal = entri_baru.daftar_tanggal()
        terdampak = [
            t
            for t in tanggal
            if self.jenis_hari_siswa(t) != entri_baru.jenis_siswa
            or self.jenis_hari_guru(t) != entri_baru.jenis_guru
        ]
        return {
            "jumlah_hari": len(terdampak),
            "tanggal": terdampak,
            "perkiraan_baris": len(terdampak) * jumlah_orang,
        }


def kalender_ganjil_2026_2027() -> Kalender:
    """Kalender semester ganjil 2026/2027 sesuai keputusan dinas.

    Disediakan sebagai data awal agar aplikasi bisa langsung diuji. Di
    lapangan, admin tetap memasukkan dan mengoreksinya lewat menu kalender.

    Catatan yang perlu dipastikan ke sekolah: 23-25 Desember 2026 belum
    tercantum di agenda resmi, padahal 25 Desember libur nasional dan 23-24
    biasanya cuti bersama. Sengaja TIDAK diisi di sini agar tidak menjadi
    tebakan yang tampak seperti data resmi.
    """
    ta = "2026/2027"
    return Kalender(
        entri=[
            EntriKalender(
                date(2026, 7, 13), date(2026, 7, 17),
                "MPLS dan hari pertama sekolah",
                JenisHari.EFEKTIF, JenisHari.EFEKTIF, ta,
            ),
            EntriKalender(
                date(2026, 8, 17), date(2026, 8, 17),
                "Libur Nasional, HUT Kemerdekaan Ke-81 RI",
                JenisHari.LIBUR_NASIONAL, JenisHari.LIBUR_NASIONAL, ta,
            ),
            EntriKalender(
                date(2026, 8, 25), date(2026, 8, 25),
                "Libur Nasional, Maulid Nabi Muhammad SAW",
                JenisHari.LIBUR_NASIONAL, JenisHari.LIBUR_NASIONAL, ta,
            ),
            EntriKalender(
                date(2026, 9, 21), date(2026, 9, 25),
                "Sumatif Tengah Semester",
                JenisHari.EFEKTIF, JenisHari.EFEKTIF, ta,
            ),
            EntriKalender(
                date(2026, 11, 30), date(2026, 12, 5),
                "Sumatif Akhir Semester",
                JenisHari.EFEKTIF, JenisHari.EFEKTIF, ta,
            ),
            EntriKalender(
                date(2026, 12, 7), date(2026, 12, 12),
                "Jeda Antar Semester (Porseni)",
                JenisHari.EFEKTIF, JenisHari.EFEKTIF, ta,
            ),
            EntriKalender(
                date(2026, 12, 14), date(2026, 12, 19),
                "Pemeriksaan dan penandatanganan nilai LHBPD",
                JenisHari.KEGIATAN_TANPA_KBM, JenisHari.EFEKTIF, ta,
            ),
            EntriKalender(
                date(2026, 12, 22), date(2026, 12, 22),
                "Penyerahan LHBPD",
                JenisHari.KEGIATAN_TANPA_KBM, JenisHari.EFEKTIF, ta,
            ),
            EntriKalender(
                date(2026, 12, 26), date(2027, 1, 2),
                "Libur Semester Ganjil",
                JenisHari.LIBUR_SEMESTER, JenisHari.LIBUR_SEMESTER, ta,
            ),
        ]
    )
