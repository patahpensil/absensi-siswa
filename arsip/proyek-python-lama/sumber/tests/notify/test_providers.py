import logging

import pytest

from app.notify.log_provider import LogProvider
from app.notify.meta_cloud_provider import MetaCloudProvider
from app.notify.null_provider import NullProvider
from app.notify.provider import StatusKirim


def test_null_provider_tidak_mengirim():
    hasil = NullProvider().send(to="+6281234567890", message="halo", idempotency_key="k1")
    assert hasil.status == StatusKirim.TERLEWAT


def test_log_provider_menulis_ke_log_dan_menandai_terkirim(caplog):
    with caplog.at_level(logging.INFO, logger="app.notify.log_provider"):
        hasil = LogProvider().send(to="+6281234567890", message="halo dunia", idempotency_key="k1")

    assert hasil.status == StatusKirim.TERKIRIM
    assert any("halo dunia" in rekaman.message for rekaman in caplog.records)


def test_meta_cloud_provider_belum_lengkap():
    provider = MetaCloudProvider(access_token="x", phone_number_id="y")
    with pytest.raises(NotImplementedError):
        provider.send(to="+6281234567890", message="halo", idempotency_key="k1")
