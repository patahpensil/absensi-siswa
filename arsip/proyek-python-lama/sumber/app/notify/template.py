from app.data.models.notification import MessageTemplate


class VariabelTemplateTidakLengkap(Exception):
    """Variabel yang dibutuhkan template tidak tersedia saat merakit pesan."""


def rakit_pesan(template: MessageTemplate, variabel: dict[str, str]) -> str:
    """Rakit isi pesan dari template.konten dan variabel bernama.

    konten memakai placeholder gaya {nama_siswa}, {jam}, {status}, {kelas}
    (lihat PRD). Melempar VariabelTemplateTidakLengkap kalau template
    menyebut placeholder yang tidak ada di variabel (KeyError) ATAU
    memakai sintaks placeholder yang rusak - kurung tak berpasangan,
    indeks posisional kosong "{}"/bernomor (IndexError), format spec
    tidak valid, atau mengakses atribut yang tidak ada (ValueError/
    AttributeError) - bukan diam-diam mengirim pesan dengan bagian kosong
    atau melempar galat mentah `str.format` ke pemanggil.
    """
    try:
        return template.konten.format(**variabel)
    except KeyError as galat:
        raise VariabelTemplateTidakLengkap(
            f"Template {template.jenis.value} v{template.versi} butuh variabel {galat}, tidak tersedia"
        ) from galat
    except (IndexError, ValueError, AttributeError) as galat:
        raise VariabelTemplateTidakLengkap(
            f"Template {template.jenis.value} v{template.versi} memakai sintaks placeholder yang "
            f"tidak valid: {galat}"
        ) from galat
