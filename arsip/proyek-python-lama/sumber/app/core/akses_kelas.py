"""Pembatasan kelas wali kelas (SKILL.md "Peran dan hak akses":
"Pembatasan kelas wali kelas ditegakkan di lapisan data" - PRD §10).

Murni - tidak menyentuh basis data maupun Qt. Dipanggil dari lapisan
service/router mana pun yang mengizinkan wali kelas menyentuh data
bertingkat kelas (mis. koreksi satuan, kelak - belum dibangun). Admin dan
kepala sekolah TIDAK dibatasi kelas sama sekali - hanya wali kelas.

Ini SATU-SATUNYA jalur pemeriksaan supaya aturan "wali kelas hanya
kelasnya sendiri" tidak diam-diam dilewati dari endpoint lain yang lupa
memeriksanya sendiri-sendiri - sama seperti
app.core.koreksi_massal.pastikan_berwenang() untuk kewenangan peran.
"""

from __future__ import annotations

from app.core.status import Peran


class AksesKelasDitolak(Exception):
    """Wali kelas mencoba menyentuh kelas yang bukan kelasnya sendiri."""

    def __init__(self, *, id_kelas_diampu: int | None, id_kelas_target: int) -> None:
        self.id_kelas_diampu = id_kelas_diampu
        self.id_kelas_target = id_kelas_target
        super().__init__(
            f"Wali kelas hanya berwenang atas kelasnya sendiri "
            f"(id_kelas_diampu={id_kelas_diampu}), bukan kelas id={id_kelas_target}"
        )


def pastikan_akses_kelas(*, peran: Peran, id_kelas_diampu: int | None, id_kelas_target: int) -> None:
    """Lempar AksesKelasDitolak bila peran WALI_KELAS mencoba menyentuh
    kelas selain kelasnya sendiri. Peran lain (admin, kepala sekolah)
    tidak dibatasi kelas - lolos tanpa pemeriksaan apa pun.

    ``id_kelas_diampu`` None berarti wali kelas belum ditugaskan ke kelas
    mana pun - selalu ditolak untuk kelas apa pun (bukan celah "None
    berarti bebas akses").
    """
    if peran != Peran.WALI_KELAS:
        return
    if id_kelas_diampu != id_kelas_target:
        raise AksesKelasDitolak(id_kelas_diampu=id_kelas_diampu, id_kelas_target=id_kelas_target)
