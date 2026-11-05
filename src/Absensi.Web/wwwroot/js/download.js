// Menyerahkan berkas laporan ke browser sebagai unduhan.
window.absensiDownload = {
    save: function (name, mime, bytes) {
        var url = URL.createObjectURL(new Blob([bytes], { type: mime }));
        var link = document.createElement('a');
        link.href = url;
        link.download = name;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(function () { URL.revokeObjectURL(url); }, 30000);
    },
    print: function () { window.print(); }
};
