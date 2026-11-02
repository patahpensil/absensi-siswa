"""KlienLaporan: pembungkus HTTP ke endpoint Laporan (PRD §14.2, §9.3).

Sama seperti klien layar lainnya: online-only, murni Python + httpx tanpa
PySide6, login lazy dengan satu retry pada 401. Endpoint ekspor/unduh
mengembalikan BERKAS BINER (xlsx/pdf), bukan JSON - method-method itu
lewat _panggil_biner(), terpisah dari _panggil() yang selalu mengurai
JSON.
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


class KlienLaporan:
    def __init__(self, *, base_url: str, username: str, password: str, timeout: float = 20.0) -> None:
        self._client = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)
        self._username = username
        self._password = password
        self._token: str | None = None
        self.peran: str | None = None

    def tutup(self) -> None:
        self._client.close()

    # ------------------------------------------------------------------
    # Laporan
    # ------------------------------------------------------------------

    def lihat_laporan(self, params: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("GET", "/laporan", params=_bersih(params))

    def ekspor_excel(self, params: dict[str, Any]) -> bytes:
        return self._panggil_biner("GET", "/laporan/ekspor.xlsx", params=_bersih(params))

    def ekspor_pdf(self, params: dict[str, Any]) -> bytes:
        return self._panggil_biner("GET", "/laporan/ekspor.pdf", params=_bersih(params))

    def daftar_arsip(self) -> list[dict[str, Any]]:
        return self._panggil("GET", "/laporan/arsip")

    def unduh_arsip(self, id_arsip: int) -> bytes:
        return self._panggil_biner("GET", f"/laporan/arsip/{id_arsip}/unduh")

    def koreksi_setelah_terbit(self, id_arsip: int) -> list[dict[str, Any]]:
        return self._panggil("GET", f"/laporan/arsip/{id_arsip}/koreksi-setelah-terbit")

    # ------------------------------------------------------------------
    # Inti - login lazy, satu retry pada 401.
    # ------------------------------------------------------------------

    def _panggil(self, method: str, path: str, *, params: dict[str, Any] | None = None) -> Any:
        respons = self._minta(method, path, params=params)
        if respons.status_code == 401:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
            respons = self._minta(method, path, params=params)

        if respons.status_code == 200:
            return respons.json()
        raise ServerMenolak(respons.status_code, self._detail(respons))

    def _panggil_biner(self, method: str, path: str, *, params: dict[str, Any] | None = None) -> bytes:
        respons = self._minta(method, path, params=params)
        if respons.status_code == 401:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
            respons = self._minta(method, path, params=params)

        if respons.status_code == 200:
            return respons.content
        raise ServerMenolak(respons.status_code, self._detail(respons))

    def _minta(self, method: str, path: str, *, params: dict[str, Any] | None) -> httpx.Response:
        if self._token is None:
            galat = self._login()
            if galat is not None:
                raise GagalHubungiServer(galat)
        try:
            return self._client.request(method, path, params=params, headers=self._headers())
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


def _bersih(params: dict[str, Any]) -> dict[str, Any]:
    return {k: v for k, v in params.items() if v is not None and v != ""}
