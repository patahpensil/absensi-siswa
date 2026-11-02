"""Validasi satu baris tabel isian data master - murni, tanpa Qt (PRD
§11.4: "validasi seketika"). Mengubah teks mentah tiap sel jadi payload
siap-kirim ke KlienDataMaster, atau kumpulan pesan galat per kolom kalau
belum lengkap/valid.

Tidak menilai "kosong" sebagai galat kolom tunggal saja - dipakai juga
untuk memutuskan apakah satu baris sudah LENGKAP (tidak ada galat sama
sekali) sehingga siap disimpan otomatis (app.ui.data_master.model_tabel).
"""

from __future__ import annotations

from dataclasses import dataclass, field
from datetime import datetime
from typing import Any

from app.core.validasi_data_master import (
    NikTidakValid,
    NisnTidakValid,
    NomorWhatsappTidakValid,
    normalisasi_nomor_whatsapp,
    validasi_nik,
    validasi_nisn,
)


@dataclass
class HasilValidasiBaris:
    #: None selama ada galat - baris belum boleh disimpan.
    payload: dict[str, Any] | None
    #: nama_kolom -> pesan galat. Kosong berarti baris valid dan lengkap.
    galat: dict[str, str] = field(default_factory=dict)

    @property
    def lengkap(self) -> bool:
        return self.payload is not None


def cek_format_nisn(nilai: str) -> str | None:
    """Pemeriksaan format seketika untuk satu sel - dipanggil tiap kali
    sel berubah, TIDAK menunggu baris lengkap. Sel kosong tidak dianggap
    galat di sini (baru dianggap galat "wajib diisi" saat validasi baris
    penuh) - supaya pengguna tidak melihat tanda merah sebelum sempat
    mengetik apa pun."""
    if not nilai.strip():
        return None
    try:
        validasi_nisn(nilai.strip())
    except NisnTidakValid as exc:
        return str(exc)
    return None


def cek_format_nik(nilai: str) -> str | None:
    if not nilai.strip():
        return None
    try:
        validasi_nik(nilai.strip())
    except NikTidakValid as exc:
        return str(exc)
    return None


def cek_format_nomor_whatsapp(nilai: str) -> str | None:
    if not nilai.strip():
        return None
    try:
        normalisasi_nomor_whatsapp(nilai.strip())
    except NomorWhatsappTidakValid as exc:
        return str(exc)
    return None


def cek_format_tanggal(nilai: str) -> str | None:
    if not nilai.strip():
        return None
    try:
        datetime.strptime(nilai.strip(), "%Y-%m-%d")
    except ValueError:
        return "Format tanggal salah, pakai YYYY-MM-DD"
    return None


def cek_format_jenis_kelamin(nilai: str) -> str | None:
    if not nilai.strip():
        return None
    if nilai.strip().upper() not in ("L", "P"):
        return "Isi L atau P"
    return None


def validasi_baris_siswa(nilai: dict[str, str], *, id_kelas_by_nama: dict[str, int]) -> HasilValidasiBaris:
    galat: dict[str, str] = {}

    nama_lengkap = nilai.get("nama_lengkap", "").strip()
    if not nama_lengkap:
        galat["nama_lengkap"] = "Wajib diisi"

    nisn = nilai.get("nisn", "").strip()
    if not nisn:
        galat["nisn"] = "Wajib diisi"
    else:
        pesan = cek_format_nisn(nisn)
        if pesan:
            galat["nisn"] = pesan

    jenis_kelamin = nilai.get("jenis_kelamin", "").strip().upper()
    if not jenis_kelamin:
        galat["jenis_kelamin"] = "Wajib diisi (L/P)"
    elif jenis_kelamin not in ("L", "P"):
        galat["jenis_kelamin"] = "Isi L atau P"

    tempat_lahir = nilai.get("tempat_lahir", "").strip()
    if not tempat_lahir:
        galat["tempat_lahir"] = "Wajib diisi"

    tanggal_lahir_str = nilai.get("tanggal_lahir", "").strip()
    tanggal_lahir = None
    if not tanggal_lahir_str:
        galat["tanggal_lahir"] = "Wajib diisi (YYYY-MM-DD)"
    else:
        pesan = cek_format_tanggal(tanggal_lahir_str)
        if pesan:
            galat["tanggal_lahir"] = pesan
        else:
            tanggal_lahir = datetime.strptime(tanggal_lahir_str, "%Y-%m-%d").date()

    alamat = nilai.get("alamat", "").strip()
    if not alamat:
        galat["alamat"] = "Wajib diisi"

    nama_ortu = nilai.get("nama_ortu", "").strip()
    if not nama_ortu:
        galat["nama_ortu"] = "Wajib diisi (nama salah satu orang tua)"

    nik_str = nilai.get("nik", "").strip()
    nik: str | None = None
    if nik_str:
        pesan = cek_format_nik(nik_str)
        if pesan:
            galat["nik"] = pesan
        else:
            nik = nik_str

    telp_str = nilai.get("nomor_whatsapp_ortu", "").strip()
    telp: str | None = None
    if telp_str:
        pesan = cek_format_nomor_whatsapp(telp_str)
        if pesan:
            galat["nomor_whatsapp_ortu"] = pesan
        else:
            telp = normalisasi_nomor_whatsapp(telp_str)

    nama_kelas = nilai.get("nama_kelas", "").strip()
    id_kelas: int | None = None
    if nama_kelas:
        id_kelas = id_kelas_by_nama.get(nama_kelas)
        if id_kelas is None:
            galat["nama_kelas"] = "Kelas tidak ditemukan (kosongkan untuk 'Belum ditempatkan')"

    nipd = nilai.get("nipd", "").strip() or None

    if galat:
        return HasilValidasiBaris(payload=None, galat=galat)

    payload = {
        "nisn": nisn,
        "nipd": nipd,
        "nama_lengkap": nama_lengkap,
        "jenis_kelamin": jenis_kelamin,
        "tempat_lahir": tempat_lahir,
        "tanggal_lahir": tanggal_lahir.isoformat(),
        "nik": nik,
        "alamat": alamat,
        "id_kelas": id_kelas,
        "nama_ortu": nama_ortu,
        "nomor_whatsapp_ortu": telp,
    }
    return HasilValidasiBaris(payload=payload, galat={})


def validasi_baris_guru(nilai: dict[str, str]) -> HasilValidasiBaris:
    galat: dict[str, str] = {}
    nama_lengkap = nilai.get("nama_lengkap", "").strip()
    if not nama_lengkap:
        galat["nama_lengkap"] = "Wajib diisi"
    if galat:
        return HasilValidasiBaris(payload=None, galat=galat)
    return HasilValidasiBaris(
        payload={
            "nama_lengkap": nama_lengkap,
            "nip": nilai.get("nip", "").strip() or None,
            "pangkat_jabatan": nilai.get("pangkat_jabatan", "").strip() or None,
        },
        galat={},
    )


def validasi_baris_kelas(nilai: dict[str, str], *, id_guru_by_nama: dict[str, int]) -> HasilValidasiBaris:
    galat: dict[str, str] = {}

    tingkat_str = nilai.get("tingkat", "").strip()
    tingkat = None
    if not tingkat_str:
        galat["tingkat"] = "Wajib diisi"
    elif not tingkat_str.isdigit():
        galat["tingkat"] = "Wajib angka"
    else:
        tingkat = int(tingkat_str)

    nama_kelas = nilai.get("nama_kelas", "").strip()
    if not nama_kelas:
        galat["nama_kelas"] = "Wajib diisi"

    tahun_ajaran = nilai.get("tahun_ajaran", "").strip()
    if not tahun_ajaran:
        galat["tahun_ajaran"] = "Wajib diisi (mis. 2026/2027)"

    nama_wali = nilai.get("wali_kelas", "").strip()
    wali_kelas_id = None
    if nama_wali:
        wali_kelas_id = id_guru_by_nama.get(nama_wali)
        if wali_kelas_id is None:
            galat["wali_kelas"] = "Guru tidak ditemukan (kosongkan bila belum ada)"

    if galat:
        return HasilValidasiBaris(payload=None, galat=galat)

    return HasilValidasiBaris(
        payload={
            "tingkat": tingkat,
            "nama_kelas": nama_kelas,
            "tahun_ajaran": tahun_ajaran,
            "wali_kelas_id": wali_kelas_id,
        },
        galat={},
    )
