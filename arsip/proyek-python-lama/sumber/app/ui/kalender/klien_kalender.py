"""KlienKalender: pembungkus HTTP ke endpoint kalender akademik dua jalur
(PRD §8). Sama seperti app.ui.data_master.klien_data_master.KlienDataMaster
- online-only, tidak ada antrean lokal (mengelola kalender mensyaratkan
jaringan lokal), murni Python + httpx tanpa PySide6 supaya bisa diuji
dengan httpx.MockTransport tanpa QApplication.

``peran`` disimpan setelah login berhasil, dipakai layar untuk
menonaktifkan tombol tambah/batalkan/salin bagi wali kelas/operator
("hanya lihat" - SKILL.md §8.3/§10) - kenyamanan tampilan saja; penegakan
sesungguhnya ada di server (wajib_peran() + KalenderService.pastikan_berwenang()).
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


class KlienKalender:
    def __init__(self, *, base_url: str, username: str, password: str, timeout: float = 5.0) -> None:
        self._client = httpx.Client(base_url=base_url.rstrip("/"), timeout=timeout)
        self._username = username
        self._password = password
        self._token: str | None = None
        self.peran: str | None = None

    def tutup(self) -> None:
        self._client.close()

    def daftar(self, *, tahun_ajaran: str | None = None) -> list[dict[str, Any]]:
        params = {"tahun_ajaran": tahun_ajaran} if tahun_ajaran else None
        return self._panggil("GET", "/kalender", params=params)

    def jenis_hari(self, tanggal_iso: str) -> dict[str, Any]:
        return self._panggil("GET", "/kalender/hari", params={"tanggal": tanggal_iso})

    def pratinjau_dampak(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("POST", "/kalender/pratinjau", json=payload)

    def tambah(self, payload: dict[str, Any]) -> dict[str, Any]:
        return self._panggil("POST", "/kalender", json=payload)

    def batalkan(self, id_entri: int, *, alasan: str) -> dict[str, Any]:
        return self._panggil("POST", f"/kalender/{id_entri}/batalkan", json={"alasan": alasan})

    def salin_tahun(self, payload: dict[str, Any]) -> list[dict[str, Any]]:
        return self._panggil("POST", "/kalender/salin-tahun", json=payload)

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
        if respons.status_code == 204:
            return None
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
