(() => {
    'use strict';
    const origin = location.origin;
    const opener = window.opener;
    const byId = id => document.getElementById(id);
    let token, snapshot, lastHeartbeat = 0, lastCards = '', lastRecent = '', lastSound;
    let audio;
    const reader = byId('gate-reader');
    const text = (id, value) => { byId(id).textContent = String(value ?? ''); };
    const node = (tag, className, value) => {
        const el = document.createElement(tag);
        if (className) el.className = className;
        if (value !== undefined) el.textContent = value;
        return el;
    };
    // Mode web dari appsettings.json (uji/nyata), sama dengan aplikasi utama.
    fetch('appsettings.json', { cache: 'no-store' }).then(r => r.ok ? r.json() : null).then(settings => {
        if (String(settings?.Mode ?? '').trim().toLowerCase() === 'nyata') text('gate-environment', 'Uji coba data asli');
    }).catch(() => {});
    const notify = data => { if (opener && token) opener.postMessage({ ...data, token }, origin); };
    const reset = message => {
        snapshot = undefined;
        reader.disabled = true;
        reader.value = '';
        byId('gate-cards').replaceChildren();
        byId('gate-recent').replaceChildren();
        for (const id of ['gate-school', 'gate-year', 'gate-clock']) text(id, id === 'gate-school' ? 'Layar gerbang' : '—');
        for (const id of ['gate-present', 'gate-late', 'gate-not-tapped', 'gate-teachers']) text(id, '—');
        text('gate-status', message);
        text('gate-reading-state', 'Reader belum aktif');
        byId('gate-empty').hidden = false;
        text('gate-empty', 'Sesi gerbang tidak aktif');
        byId('gate-fullscreen').disabled = true;
        lastCards = ''; lastRecent = '';
    };
    const play = tone => {
        try {
            audio ??= new AudioContext();
            const oscillator = audio.createOscillator();
            const gain = audio.createGain();
            oscillator.connect(gain); gain.connect(audio.destination);
            oscillator.frequency.value = tone === 'danger' ? 220 : 880;
            gain.gain.value = 0.08;
            oscillator.start(); oscillator.stop(audio.currentTime + (tone === 'danger' ? 0.35 : 0.12));
        } catch { /* Visual feedback remains available if audio is blocked. */ }
    };
    const renderCard = card => {
        const article = node('article', `card ${['positive', 'warning', 'danger'].includes(card.tone) ? card.tone : 'neutral'}`);
        article.dataset.cardId = card.id;
        article.append(node('span', 'kind', card.kind));
        if (typeof card.photo === 'string' && card.photo.startsWith('data:image/png;base64,')) {
            const photo = node('img', 'photo'); photo.src = card.photo; photo.alt = `Foto ${card.name}`; article.append(photo);
        } else article.append(node('div', 'photo placeholder', 'Foto belum tersedia'));
        const name = node('h2', 'name', card.name);
        if (card.name.length > 52) name.classList.add('name-long');
        else if (card.name.length > 28) name.classList.add('name-medium');
        article.append(name, node('p', 'identity', `${card.kind === 'Guru' ? 'NIP' : 'NISN'} ${card.identity}`),
            node('strong', 'result', card.status), node('time', '', card.time), node('p', 'reason', card.message));
        return article;
    };
    const render = value => {
        snapshot = value; lastHeartbeat = Date.now();
        text('gate-school', value.school); text('gate-year', `Tahun ajaran ${value.academicYear}`); text('gate-clock', value.clock);
        text('gate-present', value.present); text('gate-late', value.late); text('gate-not-tapped', value.notTapped); text('gate-teachers', value.teachers);
        text('gate-status', value.storageFailed ? 'Penyimpanan browser bermasalah. Hubungi admin, jangan tutup pengendali.' : value.pausedForForm ? 'Pembacaan dijeda selama formulir pengendali terbuka' : value.receiving ? 'Siap menerima kartu siswa dan guru' : 'Penerimaan tap dihentikan oleh pengelola');
        byId('gate-status').className = value.receiving && !value.storageFailed ? 'ready' : 'warning';
        text('gate-reading-state', value.receiving ? 'Mode absensi · reader aktif saat jendela mendapat fokus' : 'Mode absensi · penerimaan tap dihentikan');
        reader.disabled = !value.receiving;
        byId('gate-fullscreen').disabled = false;
        byId('gate-empty').hidden = value.cards.length > 0;
        text('gate-empty', value.receiving ? 'Silakan tap kartu' : 'Penerimaan tap dihentikan');
        const cardIds = value.cards.map(c => c.id).join(',');
        if (lastCards !== cardIds) { byId('gate-cards').replaceChildren(...value.cards.map(renderCard)); lastCards = cardIds; }
        const recentIds = value.recent.map(c => c.id).join(',');
        if (lastRecent !== recentIds) {
            byId('gate-recent').replaceChildren(...value.recent.map(c => node('span', `recent-item ${c.tone}`, `${c.time} · ${c.kind} · ${c.name} · ${c.status}`)));
            lastRecent = recentIds;
        }
        const newest = value.cards[0];
        if (newest && lastSound !== newest.id) { lastSound = newest.id; play(newest.tone); }
        if (value.receiving && document.fullscreenElement) reader.focus();
    };
    window.addEventListener('message', event => {
        if (!opener || event.source !== opener || event.origin !== origin) return;
        const data = event.data;
        if (data?.type === 'gate-init' && typeof data.token === 'string' && !token) { token = data.token; return; }
        if (!token || data?.token !== token) return;
        if (data.type === 'gate-snapshot' && data.value && Array.isArray(data.value.cards)) render(data.value);
        if (data.type === 'gate-end') { reset('Sesi gerbang diakhiri oleh pengelola.'); token = undefined; }
    });
    byId('gate-fullscreen').addEventListener('click', async () => {
        if (!snapshot) return;
        try { audio ??= new AudioContext(); await audio.resume(); await document.documentElement.requestFullscreen(); reader.focus(); }
        catch { text('gate-status', 'Fullscreen tidak diizinkan browser. Izinkan fullscreen atau gunakan F11.'); }
    });
    document.addEventListener('fullscreenchange', () => {
        byId('gate-setup').hidden = !!document.fullscreenElement;
        notify({ type: 'gate-fullscreen', active: !!document.fullscreenElement });
        if (snapshot?.receiving) reader.focus();
    });
    reader.addEventListener('keydown', event => {
        if (event.key !== 'Enter') return;
        event.preventDefault();
        const uid = reader.value.trim(); reader.value = '';
        if (snapshot?.receiving && uid && Date.now() - lastHeartbeat < 6000) notify({ type: 'gate-tap', uid });
    });
    window.addEventListener('focus', () => { if (snapshot?.receiving) reader.focus(); });
    document.addEventListener('click', event => { if (snapshot?.receiving && event.target !== byId('gate-fullscreen')) reader.focus(); });
    setInterval(() => {
        if (snapshot && (opener?.closed || Date.now() - lastHeartbeat > 6000)) reset('Koneksi ke pengendali terputus. Buka kembali dari akun yang berwenang.');
    }, 1000);
    if (opener) opener.postMessage({ type: 'gate-ready' }, origin);
})();
