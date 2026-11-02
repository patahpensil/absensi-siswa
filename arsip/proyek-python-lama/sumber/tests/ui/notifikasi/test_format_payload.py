from app.ui.notifikasi.format_payload import payload_mode_uji


def test_toggle_aktif_tanpa_ubah_nomor_tidak_menyertakan_nomor_admin():
    """Regresi: admin cuma centang/uncentang aktif, nomor di field sama
    persis dengan yang terakhir dimuat - payload tidak boleh menulis
    ulang nomor_admin (mencegah entri audit log kosong)."""
    payload = payload_mode_uji(
        aktif=False,
        nomor_input="+6281234567890",
        nomor_tersimpan="+6281234567890",
        alasan="Matikan mode uji",
    )
    assert payload == {"aktif": False, "alasan": "Matikan mode uji"}
    assert "nomor_admin" not in payload


def test_nomor_berubah_disertakan_di_payload():
    payload = payload_mode_uji(
        aktif=True, nomor_input="081298765432", nomor_tersimpan="+6281234567890", alasan="Ganti nomor admin"
    )
    assert payload == {"aktif": True, "alasan": "Ganti nomor admin", "nomor_admin": "081298765432"}


def test_nomor_kosong_tidak_menyertakan_nomor_admin():
    payload = payload_mode_uji(aktif=True, nomor_input="", nomor_tersimpan="", alasan="Aktifkan tanpa nomor")
    assert payload == {"aktif": True, "alasan": "Aktifkan tanpa nomor"}


def test_nomor_input_berspasi_dirapikan_sebelum_dibandingkan():
    """Spasi di ujung isian tidak boleh dianggap perubahan nyata."""
    payload = payload_mode_uji(
        aktif=False, nomor_input="  +6281234567890  ", nomor_tersimpan="+6281234567890", alasan="Toggle saja"
    )
    assert "nomor_admin" not in payload
