from app.data.models.notification import JenisNotifikasi
from app.data.repositories.message_template import MessageTemplateRepository


def test_tambah_dan_cari_aktif(session):
    repo = MessageTemplateRepository(session)
    template = repo.tambah(jenis=JenisNotifikasi.ALPA_PAGI, versi=1, konten="v1")

    aktif = repo.cari_aktif(JenisNotifikasi.ALPA_PAGI)
    assert aktif is not None
    assert aktif.id == template.id
    assert aktif.versi == 1


def test_tambah_versi_baru_menonaktifkan_versi_lama(session):
    repo = MessageTemplateRepository(session)
    v1 = repo.tambah(jenis=JenisNotifikasi.ALPA_PAGI, versi=1, konten="v1")
    v2 = repo.tambah(jenis=JenisNotifikasi.ALPA_PAGI, versi=2, konten="v2")

    aktif = repo.cari_aktif(JenisNotifikasi.ALPA_PAGI)
    assert aktif.id == v2.id

    v1_setelah = repo.cari_by_id(v1.id)
    assert v1_setelah.aktif is False
    # Versi lama tetap ada di tabel, tidak dihapus.
    assert v1_setelah.konten == "v1"


def test_jenis_berbeda_tidak_saling_memengaruhi(session):
    repo = MessageTemplateRepository(session)
    alpa = repo.tambah(jenis=JenisNotifikasi.ALPA_PAGI, versi=1, konten="alpa v1")
    masuk = repo.tambah(jenis=JenisNotifikasi.TERLAMBAT, versi=1, konten="masuk v1")

    assert repo.cari_aktif(JenisNotifikasi.ALPA_PAGI).id == alpa.id
    assert repo.cari_aktif(JenisNotifikasi.TERLAMBAT).id == masuk.id


def test_daftar_versi_mengembalikan_riwayat_terurut(session):
    repo = MessageTemplateRepository(session)
    repo.tambah(jenis=JenisNotifikasi.ALPA_PAGI, versi=1, konten="v1")
    repo.tambah(jenis=JenisNotifikasi.ALPA_PAGI, versi=2, konten="v2")
    repo.tambah(jenis=JenisNotifikasi.ALPA_PAGI, versi=3, konten="v3")

    versi = repo.daftar_versi(JenisNotifikasi.ALPA_PAGI)
    assert [t.versi for t in versi] == [1, 2, 3]
    assert [t.aktif for t in versi] == [False, False, True]


def test_tambah_tidak_aktif_tidak_menonaktifkan_yang_lain(session):
    repo = MessageTemplateRepository(session)
    aktif = repo.tambah(jenis=JenisNotifikasi.ALPA_PAGI, versi=1, konten="v1")
    repo.tambah(jenis=JenisNotifikasi.ALPA_PAGI, versi=2, konten="draf v2", aktif=False)

    assert repo.cari_aktif(JenisNotifikasi.ALPA_PAGI).id == aktif.id
