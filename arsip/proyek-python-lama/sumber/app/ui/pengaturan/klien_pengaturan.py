"""KlienPengaturan: pembungkus HTTP ke endpoint Pengaturan (PRD §4,
§14.2) - jadwal per hari, ambang, saklar modul, identitas sekolah.

Sama seperti app.ui.kalender.klien_kalender.KlienKalender: online-only,
murni Python + httpx tanpa PySide6, login lazy dengan satu retry pada
401. Seluruh endpoint Pengaturan dibatasi kepala sekolah/admin SAJA
(termasuk GET) - kalau login berhasil tapi peran bukan keduanya, setiap
panggilan akan gagal dengan ServerMenolak(403); layar menampilkan pesan
yang jelas, bukan mencoba menyembunyikan tombol seperti layar Kalender
(di sana ada peran "hanya lihat", di sini tidak ada sama sekali).
"""

from __future__ import annotations

from typing import Any

import httpx


class GagalHubungiServer(Exception):
    pass


class ServerMenolak(Exception):
    def __init__(self, status_code: int, detail: str) -> None:
        self.status_code = status_code
        self.detail = detail
        super().__init__(f"Server menolak (kode {status_code}): {detail}")


class KlienPengaturan:
    def __init__(self, *, base_url: str, username: str, password: str, timeout: float = 5.0) -> None:
        self._client = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)
        self._username = username
        self._password = password
        self._token: str | None = None
        self.peran: str | None = None

    def tutup(self) -> None:
        self._client.close()

    # ------------------------------------------------------------------
    # Jadwal
    # ------------------------------------------------------------------

    def daftar_jadwal(self, *, jenis_pemilik: str) -> list[dict[str, Any]]:
        return self._panggil("GET", "/pengaturan/jadwal", params={"jenis_pemilik": jenis_pemilik})

    def tambah_jadwal(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("POST", "/pengaturan/jadwal", json=payload)

    # ------------------------------------------------------------------
    # Ambang
    # ------------------------------------------------------------------

    def ambil_ambang(self) -> dict[str, Any]:
        return self._panggil("GET", "/pengaturan/ambang")

    def ubah_ambang(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("PUT", "/pengaturan/ambang", json=payload)

    # ------------------------------------------------------------------
    # Saklar modul
    # ------------------------------------------------------------------

    def ambil_saklar(self) -> dict[str, Any]:
        return self._panggil("GET", "/pengaturan/saklar")

    def ubah_saklar(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("PUT", "/pengaturan/saklar", json=payload)

    # ------------------------------------------------------------------
    # Identitas sekolah
    # ------------------------------------------------------------------

    def ambil_identitas_sekolah(self) -> dict[str, Any]:
        return self._panggil("GET", "/pengaturan/identitas-sekolah")

    def ubah_identitas_sekolah(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("PUT", "/pengaturan/identitas-sekolah", json=payload)

    # ------------------------------------------------------------------
    # Inti - login lazy, satu retry pada 401.
    # ------------------------------------------------------------------

    def _panggil(
        self, method: str, path: str, *, json: dict[str, Any] | None = None, params: dict[str, Any] | None = None
    ) -> Any:
        respons = self._minta(method, path, json=json, params=params)
        if respons.status_code == 401:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
            respons = self._minta(method, path, json=json, params=params)

        if respons.status_code in (200, 201):
            return respons.json()
        raise ServerMenolak(respons.status_code, self._detail(respons))

    def _minta(
        self, method: str, path: str, *, json: dict[str, Any] | None, params: dict[str, Any] | None
    ) -> httpx.Response:
        if self._token is None:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
        try:
            return self._client.request(method, path, json=json, params=params, headers=self._headers())
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
        badan = respons.json()
        self._token = badan["token"]
        self.peran = badan["peran"]
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
