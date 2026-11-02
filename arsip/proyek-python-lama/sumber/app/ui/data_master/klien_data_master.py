"""KlienDataMaster: pembungkus HTTP ke endpoint data master - siswa, guru,
kelas, dan siklus hidup kartu RFID (PRD §11, §12).

BEDA dari app.ui.lobi.klien_scan.KlienScan dengan sengaja: data master
TIDAK lewat antrean lokal. Mengisi/mengedit data master mensyaratkan
koneksi ke server (setara "Koreksi absensi"/"Dashboard dan laporan" di
tabel §16 SKILL.md - wajib jaringan lokal), bukan peristiwa yang harus
tetap tercatat tanpa LAN seperti scan absensi. Kegagalan terhubung
langsung dilaporkan ke pengguna sebagai galat, tidak diam-diam diantrekan.

Murni Python + httpx - tidak mengimpor PySide6 sama sekali, supaya bisa
diuji dengan httpx.MockTransport tanpa QApplication (lihat
tests/ui/lobi/test_klien_scan.py untuk pola yang sama).
"""

from __future__ import annotations

from typing import Any

import httpx


class GagalHubungiServer(Exception):
    """Jaringan terputus atau server tidak terjangkau sama sekali."""


class ServerMenolak(Exception):
    """Server terjangkau tapi menolak permintaan (4xx/5xx selain 401 yang
    sudah ditangani lewat login ulang)."""

    def __init__(self, status_code: int, detail: str) -> None:
        self.status_code = status_code
        self.detail = detail
        super().__init__(f"Server menolak (kode {status_code}): {detail}")


class KlienDataMaster:
    def __init__(self, *, base_url: str, username: str, password: str, timeout: float = 5.0) -> None:
        self._client = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)
        self._username = username
        self._password = password
        self._token: str | None = None

    def tutup(self) -> None:
        self._client.close()

    # ------------------------------------------------------------------
    # Siswa
    # ------------------------------------------------------------------

    def buat_siswa(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("POST", "/siswa", json=payload)

    def ubah_siswa(self, id_siswa: int, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("PUT", f"/siswa/{id_siswa}", json=payload)

    def daftar_siswa(self) -> list[dict[str, Any]]:
        return self._panggil("GET", "/siswa")

    def detail_siswa(self, id_siswa: int) -> dict[str, Any]:
        return self._panggil("GET", f"/siswa/{id_siswa}")

    # ------------------------------------------------------------------
    # Guru
    # ------------------------------------------------------------------

    def buat_guru(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("POST", "/guru", json=payload)

    def ubah_guru(self, id_guru: int, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("PUT", f"/guru/{id_guru}", json=payload)

    def daftar_guru(self) -> list[dict[str, Any]]:
        return self._panggil("GET", "/guru")

    # ------------------------------------------------------------------
    # Kelas
    # ------------------------------------------------------------------

    def buat_kelas(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("POST", "/kelas", json=payload)

    def ubah_kelas(self, id_kelas: int, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("PUT", f"/kelas/{id_kelas}", json=payload)

    def daftar_kelas(self) -> list[dict[str, Any]]:
        return self._panggil("GET", "/kelas")

    def arsipkan_kelas(self, id_kelas: int) -> None:
        self._panggil("DELETE", f"/kelas/{id_kelas}")

    # ------------------------------------------------------------------
    # Kartu RFID
    # ------------------------------------------------------------------

    def terbitkan_kartu(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("POST", "/kartu", json=payload)

    def nonaktifkan_kartu(self, id_kartu: int, *, alasan: str | None) -> dict[str, Any]:
        return self._panggil("POST", f"/kartu/{id_kartu}/nonaktifkan", json={"alasan": alasan})

    def cetak_ulang_kartu(self, id_kartu: int, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("POST", f"/kartu/{id_kartu}/cetak-ulang", json=payload)

    def daftar_kartu_by_pemilik(self, *, jenis_pemilik: str, id_pemilik: int) -> list[dict[str, Any]]:
        return self._panggil("GET", f"/kartu/pemilik/{jenis_pemilik}/{id_pemilik}")

    # ------------------------------------------------------------------
    # Inti - login lazy, satu retry pada 401, tidak pernah menyembunyikan
    # kegagalan sebagai sukses.
    # ------------------------------------------------------------------

    def _panggil(self, method: str, path: str, *, json: dict[str, Any] | None = None) -> Any:
        respons = self._minta(method, path, json=json)
        if respons.status_code == 401:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
            respons = self._minta(method, path, json=json)

        if respons.status_code in (200, 201):
            return respons.json()
        if respons.status_code == 204:
            return None
        raise ServerMenolak(respons.status_code, self._detail(respons))

    def _minta(self, method: str, path: str, *, json: dict[str, Any] | None) -> httpx.Response:
        if self._token is None:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
        try:
            return self._client.request(method, path, json=json, headers=self._headers())
        except httpx.HTTPError as exc:
            raise GagalHubungiServer(f"Tidak terhubung ke server: {exc}") from exc

    def _headers(self) -> dict[str, str]:
        return {"Authorization": f"Bearer {self._token}"}

    def _login(self) -> str | None:
        try:
            respons = self._client.post(
                "/auth/login", json={"username": self._username, "password": self._password}
            )
        except httpx.HTTPError as exc:
            return f"Tidak bisa login ke server: {exc}"
        if respons.status_code != 200:
            return f"Login gagal (kode {respons.status_code}) - periksa kredensial akun"
        self._token = respons.json()["token"]
        return None

    @staticmethod
    def _detail(respons: httpx.Response) -> str:
        try:
            badan = respons.json()
        except ValueError:
            return respons.text
        if isinstance(badan, dict) and "detail" in badan:
            return str(badan["detail"])
        return str(badan)
