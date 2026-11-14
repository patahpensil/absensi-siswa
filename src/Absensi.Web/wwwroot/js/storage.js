// The SQLite snapshot and its version are stored in the same IndexedDB record.
(() => {
    const databaseKey = 'absensi.db';
    const legacyWizardKey = 'wizard.pending';
    const photoPrefix = 'foto/';

    function open() {
        return new Promise((resolve, reject) => {
            const request = indexedDB.open('absensi-uji', 2);
            let blocked = false;
            request.onupgradeneeded = () => {
                if (!request.result.objectStoreNames.contains('berkas')) request.result.createObjectStore('berkas');
            };
            request.onblocked = () => {
                blocked = true;
                reject(new Error('Tutup tab web uji versi sebelumnya, lalu muat ulang.'));
            };
            request.onsuccess = () => {
                if (blocked) request.result.close();
                else resolve(request.result);
            };
            request.onerror = () => reject(request.error);
        });
    }

    async function transaction(mode, work) {
        const db = await open();
        try {
            return await new Promise((resolve, reject) => {
                const tx = db.transaction('berkas', mode);
                let result;
                tx.oncomplete = () => resolve(result);
                tx.onabort = () => reject(tx.error || new Error('Penyimpanan dibatalkan.'));
                work(tx.objectStore('berkas'), value => { result = value; });
            });
        } finally {
            db.close();
        }
    }

    function snapshot(value, legacyWizard) {
        if (value && Number.isInteger(value.version)) return value;
        return { version: 0, bytes: value ?? null, wizardPending: Boolean(legacyWizard?.length) };
    }

    function updateDatabase(version, bytes, wizardPending, photos, reset) {
        return transaction('readwrite', (store, done) => {
            const request = store.get(databaseKey);
            request.onsuccess = () => {
                const current = snapshot(request.result);
                if (current.version !== version) { done(false); return; }
                store.put({ version: version + 1, bytes, wizardPending }, databaseKey);
                store.delete(legacyWizardKey);
                if (reset) store.delete(IDBKeyRange.bound(photoPrefix, 'foto0', false, true));
                for (const [name, data] of Object.entries(photos)) store.put(data, photoPrefix + name);
                done(true);
            };
        });
    }

    window.absensiStorage = {
        loadDatabase: () => transaction('readonly', (store, done) => {
            const db = store.get(databaseKey);
            const wizard = store.get(legacyWizardKey);
            wizard.onsuccess = () => {
                const { version, bytes, wizardPending } = snapshot(db.result, wizard.result);
                // Blazor transfers one Uint8Array directly, avoiding base64 for the SQLite file.
                const result = new Uint8Array(5 + (bytes?.length ?? 0));
                new DataView(result.buffer).setUint32(0, version, true);
                result[4] = wizardPending ? 1 : 0;
                if (bytes) result.set(bytes, 5);
                done(result);
            };
        }),
        saveDatabase: (version, bytes, photos) => updateDatabase(version, bytes, false, photos, false),
        resetDatabase: (version, wizardPending) => updateDatabase(version, null, wizardPending, {}, true),
        restoreDatabase: (version, bytes, photos) => updateDatabase(version, bytes, false, photos, true),
        load: key => transaction('readonly', (store, done) => {
            const request = store.get(key);
            request.onsuccess = () => done(request.result ?? null);
        }),
        // Asks the browser not to evict this origin's data when disk space runs
        // low. Browsers decide on their own; the answer is shown in Pengaturan.
        requestPersistence: async () => {
            if (!navigator.storage?.persist || !navigator.storage?.persisted) return 'unsupported';
            if (await navigator.storage.persisted()) return 'granted';
            return (await navigator.storage.persist()) ? 'granted' : 'denied';
        },
        keys: prefix => transaction('readonly', (store, done) => {
            const request = store.getAllKeys();
            request.onsuccess = () => done(request.result.filter(key => String(key).startsWith(prefix)));
        })
    };
})();
