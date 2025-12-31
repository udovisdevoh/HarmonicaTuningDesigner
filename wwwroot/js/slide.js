// slide.js - Adds piano slide behavior: plays notes when pointer moves over note cells while primary button is pressed
(function () {
    let pointerDown = false;
    let lastPlayed = null;

    function playCellIfNew(cell) {
        if (!cell || !(cell instanceof Element)) return;
        if (cell === lastPlayed) return;
        // call global playNote if available
        try {
            if (typeof window.playNote === 'function') {
                window.playNote(cell);
                lastPlayed = cell;
            }
        } catch (e) {
            // ignore
        }
    }

    // pointer events
    document.addEventListener('pointerdown', (e) => {
        if (e.button !== undefined && e.button !== 0) return; // primary only
        pointerDown = true;
        lastPlayed = null;
    });
    document.addEventListener('pointerup', () => { pointerDown = false; lastPlayed = null; });
    document.addEventListener('pointercancel', () => { pointerDown = false; lastPlayed = null; });

    document.addEventListener('pointermove', (e) => {
        if (!pointerDown) return;
        try {
            const el = document.elementFromPoint(e.clientX, e.clientY);
            const cell = el && el.closest ? el.closest('.note-cell') : null;
            playCellIfNew(cell);
        } catch (err) { }
    }, { passive: true });

    // fallback for mouse-only browsers
    if (!window.PointerEvent) {
        document.addEventListener('mousedown', (e) => {
            if (e.button !== 0) return;
            pointerDown = true; lastPlayed = null;
            const cell = e.target && e.target.closest ? e.target.closest('.note-cell') : null;
            playCellIfNew(cell);
        });
        document.addEventListener('mousemove', (e) => {
            if (!pointerDown) return;
            const el = document.elementFromPoint(e.clientX, e.clientY);
            const cell = el && el.closest ? el.closest('.note-cell') : null;
            playCellIfNew(cell);
        }, { passive: true });
        document.addEventListener('mouseup', () => { pointerDown = false; lastPlayed = null; });
    }

    // touch support
    document.addEventListener('touchstart', (e) => {
        pointerDown = true; lastPlayed = null;
        const t = e.touches[0];
        if (!t) return;
        const el = document.elementFromPoint(t.clientX, t.clientY);
        const cell = el && el.closest ? el.closest('.note-cell') : null;
        playCellIfNew(cell);
    }, { passive: false });

    document.addEventListener('touchmove', (e) => {
        if (!pointerDown) return;
        const t = e.touches[0];
        if (!t) return;
        const el = document.elementFromPoint(t.clientX, t.clientY);
        const cell = el && el.closest ? el.closest('.note-cell') : null;
        playCellIfNew(cell);
    }, { passive: false });

    document.addEventListener('touchend', () => { pointerDown = false; lastPlayed = null; });
})();
