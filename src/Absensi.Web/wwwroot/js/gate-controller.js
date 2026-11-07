let gateWindow, reference, token, snapshot, full = false, keyBuffer = '';
let onMessage, onClick, onKey, onUnload;
const origin = location.origin;

export function attach(dotnet) {
    detach();
    reference = dotnet;
    onClick = event => {
        const button = event.target.closest?.('#gate-open');
        if (!button || button.disabled) return;
        if (!gateWindow || gateWindow.closed) {
            token = crypto.randomUUID();
            gateWindow = window.open('gate.html', '_blank', 'popup,width=1280,height=800');
        } else gateWindow.focus();
    };
    onMessage = event => {
        if (!gateWindow || event.source !== gateWindow || event.origin !== origin) return;
        const data = event.data;
        if (data?.type === 'gate-ready') {
            gateWindow.postMessage({ type: 'gate-init', token }, origin);
            if (snapshot) publish(snapshot);
            return;
        }
        if (!token || data?.token !== token) return;
        if (data.type === 'gate-fullscreen') full = data.active === true;
        if (data.type === 'gate-tap' && !document.querySelector('[role="dialog"]') && snapshot?.receiving && typeof data.uid === 'string' && data.uid.length <= 64)
            reference?.invokeMethodAsync('ReadCard', data.uid);
    };
    // Keyboard-emulation readers can also reach the controller when the TV is
    // open on another monitor. Never capture typing in a form or registration.
    onKey = event => {
        const editable = event.target instanceof HTMLElement &&
            (event.target.matches('input, textarea, select') || event.target.isContentEditable);
        if (!snapshot?.receiving || editable || document.querySelector('[role="dialog"]') || event.ctrlKey || event.altKey || event.metaKey) {
            keyBuffer = '';
            return;
        }
        if (event.key === 'Enter' && keyBuffer) {
            event.preventDefault();
            const uid = keyBuffer;
            keyBuffer = '';
            reference?.invokeMethodAsync('ReadCard', uid);
        } else if (event.key.length === 1) {
            keyBuffer = (keyBuffer + event.key).slice(-64);
        } else keyBuffer = '';
    };
    onUnload = () => closeDisplay();
    document.addEventListener('click', onClick, true);
    document.addEventListener('keydown', onKey, true);
    window.addEventListener('message', onMessage);
    window.addEventListener('pagehide', onUnload);
}
export function isOpen() { return !!gateWindow && !gateWindow.closed; }
export function isFullscreen() { return isOpen() && full; }
export function publish(value) {
    const pausedForForm = !!document.querySelector('[role="dialog"]');
    snapshot = { ...value, pausedForForm, receiving: value.receiving && !pausedForForm };
    if (isOpen() && token) gateWindow.postMessage({ type: 'gate-snapshot', token, value: snapshot }, origin);
}
export function closeDisplay() {
    if (isOpen()) {
        gateWindow.postMessage({ type: 'gate-end', token }, origin);
        gateWindow.close();
    }
    gateWindow = undefined;
    token = undefined;
    full = false;
}
export function detach() {
    closeDisplay();
    if (onClick) document.removeEventListener('click', onClick, true);
    if (onKey) document.removeEventListener('keydown', onKey, true);
    if (onMessage) window.removeEventListener('message', onMessage);
    if (onUnload) window.removeEventListener('pagehide', onUnload);
    reference = undefined;
    snapshot = undefined;
    keyBuffer = '';
}
