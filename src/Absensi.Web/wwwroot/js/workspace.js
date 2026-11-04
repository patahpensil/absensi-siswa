let listener;
export function attach(reference) {
    detach();
    listener = (event) => {
        const dialog = document.querySelector('[role="dialog"]');
        if (dialog) {
            if (event.key === 'Tab') {
                const fields = Array.from(dialog.querySelectorAll('button:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex="0"]'));
                const first = fields[0], last = fields[fields.length - 1];
                if (!dialog.contains(document.activeElement)) { event.preventDefault(); (event.shiftKey ? last : first)?.focus(); }
                else if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
                else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
            }
            return;
        }
        const typing = event.target instanceof HTMLElement &&
            (event.target.matches('input, textarea, select') || event.target.isContentEditable);
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
            event.preventDefault();
            document.getElementById('workspace-command')?.focus();
        } else if (!typing && !event.ctrlKey && !event.altKey && !event.metaKey && ['F2', 'F3'].includes(event.key)) {
            event.preventDefault();
            reference.invokeMethodAsync('OpenPage', event.key === 'F2' ? 'Absensi' : 'Siswa');
        }
    };
    document.addEventListener('keydown', listener);
}
export function detach() {
    if (listener) document.removeEventListener('keydown', listener);
    listener = undefined;
}
