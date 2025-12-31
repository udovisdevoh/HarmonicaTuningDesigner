(function () {
    const noteMap = {
        'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3,
        'E': 4, 'F': 5, 'F#': 6, 'Gb': 6, 'G': 7, 'G#': 8, 'Ab': 8,
        'A': 9, 'A#': 10, 'Bb': 10, 'B': 11
    };

    const PIANO_PROGRAM = 0;
    const STRING_ENSEMBLE_PROGRAM = 48;

    let midiPlayerReadyPromise = null;

    function loadScript(url) {
        return new Promise((resolve, reject) => {
            const existing = document.querySelector(`script[src='${url}']`);
            if (existing) {
                if (existing.getAttribute('data-loaded') === 'true') return resolve();
                existing.addEventListener('load', () => resolve());
                existing.addEventListener('error', () => reject(new Error('Failed to load ' + url)));
                return;
            }

            const s = document.createElement('script');
            s.src = url;
            s.async = false;
            s.onload = () => {
                s.setAttribute('data-loaded', 'true');
                resolve();
            };
            s.onerror = () => reject(new Error('Failed to load ' + url));
            document.head.appendChild(s);
        });
    }

    async function ensureMidiPlayerLoaded() {
        if (midiPlayerReadyPromise) return midiPlayerReadyPromise;

        midiPlayerReadyPromise = (async () => {
            try {
                if (typeof Soundfont === 'undefined') {
                    await loadScript('/js/soundfont-player.min.js');
                }
                if (typeof window.midiPlayer === 'undefined') {
                    await loadScript('/js/midiPlayer.js');
                }
            } catch (err) {
                console.error('Failed to load dependencies', err);
                throw err;
            }

            return new Promise((resolve, reject) => {
                const start = Date.now();
                const timeout = 5000;
                (function check() {
                    if (window.midiPlayer && typeof window.midiPlayer.playSingleNote === 'function') {
                        // --- PRELOAD INSTRUMENTS HERE ---
                        // We call a silent note or a load method if your midiPlayer supports it
                        // This forces the browser to download the .js/mp3 samples now.
                        console.log("MidiPlayer ready, preloading instrument samples...");

                        // If your midiPlayer.js has a specific 'load' or 'warmup' method, use it.
                        // Otherwise, playing a note with 0 volume often triggers the download.
                        if (typeof window.midiPlayer.preloadInstrument === 'function') {
                            window.midiPlayer.preloadInstrument(PIANO_PROGRAM);
                            window.midiPlayer.preloadInstrument(STRING_ENSEMBLE_PROGRAM);
                        } else {
                            // Fallback: trigger a silent note to force soundfont-player to fetch assets
                            window.midiPlayer.playSingleNote(0, 0, PIANO_PROGRAM);
                        }

                        return resolve(true);
                    }
                    if (Date.now() - start > timeout) return reject(new Error('midiPlayer timeout'));
                    setTimeout(check, 50);
                })();
            });
        })();

        return midiPlayerReadyPromise;
    }

    // --- NEW: AUTO-START PRELOAD ---
    // This triggers the download as soon as the page is idle or loaded
    if (document.readyState === 'complete') {
        ensureMidiPlayerLoaded();
    } else {
        window.addEventListener('load', () => ensureMidiPlayerLoaded());
    }

    window.playNote = async function (cell) {
        try {
            // ... (Your existing note parsing logic remains the same)
            const noteAttr = cell.dataset.note || '';
            const octaveAttr = cell.dataset.octave || '';
            let name = noteAttr.trim();
            let octave = parseInt(octaveAttr);

            if (!name) {
                const txt = cell.innerText.trim();
                const m = txt.match(/^([A-G][#b]?)(\d+)$/);
                if (m) { name = m[1]; octave = parseInt(m[2]); }
            }
            if (!name) return;
            if (isNaN(octave)) octave = 4;

            const semitone = noteMap[name];
            if (semitone === undefined) return;

            let midi = (octave - 1) * 12 + semitone;
            while (midi > 127) midi -= 12;
            while (midi < 0) midi += 12;

            const program = PIANO_PROGRAM;

            // This will now resolve instantly because we preloaded
            await ensureMidiPlayerLoaded();

            window.midiPlayer.playSingleNote(midi, 0.6, program);
        } catch (err) {
            console.error(err);
        }
    };
})();