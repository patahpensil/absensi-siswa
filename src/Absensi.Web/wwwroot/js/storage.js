// Penyimpanan berkas basis data di IndexedDB browser (satu catatan per kunci).
window.absensiStorage = {
    open: function () {
        return new Promise(function (resolve, reject) {
            var req = indexedDB.open('absensi-uji', 1);
            req.onupgradeneeded = function () { req.result.createObjectStore('berkas'); };
            req.onsuccess = function () { resolve(req.result); };
            req.onerror = function () { reject(req.error); };
        });
    },
    save: async function (key, bytes) {
        var db = await this.open();
        return new Promise(function (resolve, reject) {
            var tx = db.transaction('berkas', 'readwrite');
            tx.objectStore('berkas').put(bytes, key);
            tx.oncomplete = function () { db.close(); resolve(); };
            tx.onerror = function () { reject(tx.error); };
        });
    },
    load: async function (key) {
        var db = await this.open();
        return new Promise(function (resolve, reject) {
            var req = db.transaction('berkas', 'readonly').objectStore('berkas').get(key);
            req.onsuccess = function () { db.close(); resolve(req.result || null); };
            req.onerror = function () { reject(req.error); };
        });
    },
    keys: async function (prefix) {
        var db = await this.open();
        return new Promise(function (resolve, reject) {
            var req = db.transaction('berkas', 'readonly').objectStore('berkas').getAllKeys();
            req.onsuccess = function () { db.close(); resolve(req.result.filter(function (k) { return String(k).startsWith(prefix); })); };
            req.onerror = function () { reject(req.error); };
        });
    },
    remove: async function (key) {
        var db = await this.open();
        return new Promise(function (resolve, reject) {
            var tx = db.transaction('berkas', 'readwrite');
            tx.objectStore('berkas').delete(key);
            tx.oncomplete = function () { db.close(); resolve(); };
            tx.onerror = function () { reject(tx.error); };
        });
    }
};
