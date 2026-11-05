// PRD §5.5/§16.4: foto dikecilkan ke ±200×200 saat unggah, sama seperti
// PengolahFoto desktop: skala agar muat (sisi terpanjang 200 px), ditengahkan
// di kanvas 200×200 transparan, disimpan sebagai PNG. Tidak dipotong.
window.absensiPhoto = {
    resize: async function (bytes) {
        var bitmap;
        try { bitmap = await createImageBitmap(new Blob([bytes])); }
        catch (e) { return null; }
        var size = 200;
        var scale = size / Math.max(bitmap.width, bitmap.height);
        var w = Math.max(1, Math.round(bitmap.width * scale));
        var h = Math.max(1, Math.round(bitmap.height * scale));
        var canvas = document.createElement('canvas');
        canvas.width = size;
        canvas.height = size;
        canvas.getContext('2d').drawImage(bitmap, (size - w) / 2, (size - h) / 2, w, h);
        bitmap.close();
        var blob = await new Promise(function (resolve) { canvas.toBlob(resolve, 'image/png'); });
        return blob ? new Uint8Array(await blob.arrayBuffer()) : null;
    }
};
