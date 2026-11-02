"""KlienKoreksiMassal: pembungkus HTTP ke endpoint Koreksi Massal (PRD
§9.2, §10). Sama seperti klien layar admin lainnya: online-only, murni
Python + httpx tanpa PySide6, login lazy dengan satu retry pada 401.
Server yang menegakkan lingkup peran (hanya kepsek/admin) dan ambang
snapshot/konfirmasi ketik - klien ini tidak menduplikasi logika itu,
hanya meneruskan galatnya apa adanya (lihat app.ui.koreksi_massal.layar
dan dialog_tindakan untuk penanganan galat 412 yang anggun).
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


class KlienKoreksiMassal:
    def __init__(self, *, base_url: str, username: str, password: str, timeout: float = 5.0) -> None:
        self._client = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)
        self._username = username
        self._password = password
        self._token: str | None = None
        self.peran: str | None = None

    def tutup(self) -> None:
        self._client.close()

    def pratinjau(self, penyaring: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("POST", "/koreksi-massal/pratinjau", json=penyaring)

    def jalankan(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("POST", "/koreksi-massal/jalankan", json=payload)

    def batalkan(self, id_tindakan_massal: int, *, alasan: str) -> dict[str, Any]:
        return self._panggil("POST", f"/koreksi-massal/{id_tindakan_massal}/batalkan", json={"alasan": alasan})

    def riwayat(self, *, limit: int = 50) -> list[dict[str, Any]]:
        return self._panggil("GET", "/koreksi-massal", params={"limit": limit})

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
