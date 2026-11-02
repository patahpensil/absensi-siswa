import pytest

from app.data.models.notification import JenisNotifikasi, MessageTemplate
from app.notify.template import VariabelTemplateTidakLengkap, rakit_pesan


def _template(konten: str, *, versi: int = 1) -> MessageTemplate:
    return MessageTemplate(jenis=JenisNotifikasi.TERLAMBAT, versi=versi, konten=konten, aktif=True)


def test_rakit_pesan_mengganti_variabel_bernama():
    template = _template("Ananda {nama_siswa} hadir pukul {jam}. Status: {status}.")

    pesan = rakit_pesan(
        template,
        {"nama_siswa": "Budi", "jam": "07:12", "status": "Hadir"},
    )

    assert pesan == "Ananda Budi hadir pukul 07:12. Status: Hadir."


def test_rakit_pesan_variabel_hilang_melempar_error_jelas():
    template = _template("Ananda {nama_siswa} di kelas {kelas} hadir pukul {jam}.")

    with pytest.raises(VariabelTemplateTidakLengkap):
        rakit_pesan(template, {"nama_siswa": "Budi", "jam": "07:12"})  # kelas hilang


def test_rakit_pesan_tidak_peduli_variabel_berlebih():
    template = _template("Ananda {nama_siswa} hadir.")

    pesan = rakit_pesan(template, {"nama_siswa": "Budi", "jam": "07:12", "status": "Hadir"})

    assert pesan == "Ananda Budi hadir."


def test_rakit_pesan_kurung_kosong_ditolak_dengan_error_jelas():
    """"{}" (indeks posisional kosong) melempar IndexError mentah dari
    str.format, bukan KeyError - harus tetap ditangkap sebagai
    VariabelTemplateTidakLengkap, bukan lolos jadi galat mentah."""
    template = _template("Ananda {} terlambat pukul {jam}.")

    with pytest.raises(VariabelTemplateTidakLengkap):
        rakit_pesan(template, {"nama_siswa": "Budi", "jam": "07:12"})


def test_rakit_pesan_kurung_tak_berpasangan_ditolak_dengan_error_jelas():
    """Kurung pembuka tanpa penutup melempar ValueError dari str.format -
    sintaks rusak lain di luar IndexError/KeyError, tetap harus tertangkap."""
    template = _template("Ananda {nama_siswa terlambat pukul {jam}.")

    with pytest.raises(VariabelTemplateTidakLengkap):
        rakit_pesan(template, {"nama_siswa": "Budi", "jam": "07:12"})
