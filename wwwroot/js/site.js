// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

// Simple site JS for HarmonicaTuningDesigner
// Play a note when a note-cell is clicked. Uses WebAudio oscillator to synthesize a simple tone.

(function () {
    // Note name to semitone mapping
    const noteMap = {
        'C': 0, 'C#': 1, 'Db': 1, 'D': 2, 'D#': 3, 'Eb': 3,
        'E': 4, 'F': 5, 'F#': 6, 'Gb': 6, 'G': 7, 'G#': 8, 'Ab': 8,
        'A': 9, 'A#': 10, 'Bb': 10, 'B': 11
    };

    // Instrument program numbers (match midiPlayer.js constants)
    const PIANO_PROGRAM = 0;
    const STRING_ENSEMBLE_PROGRAM = 48;

    // Promise to track loading state of midiPlayer + soundfont
    let midiPlayerReadyPromise = null;

    function loadScript(url) {
        return new Promise((resolve, reject) => {
            // If already loaded, resolve
            const existing = document.querySelector(`script[src='${url}']`);
            if (existing) {
                if (existing.getAttribute('data-loaded') === 'true') return resolve();
                existing.addEventListener('load', () => resolve());
                existing.addEventListener('error', () => reject(new Error('Failed to load ' + url)));
                return;
            }

            const s = document.createElement('script');
            s.src = url;
            s.async = false; // preserve execution order
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
            // Load Soundfont player first, then midiPlayer
            try {
                if (typeof Soundfont === 'undefined') {
                    await loadScript('/js/soundfont-player.min.js');
                }
            } catch (err) {
                console.error('Failed to load soundfont-player.min.js', err);
                throw err;
            }

            try {
                if (typeof window.midiPlayer === 'undefined') {
                    await loadScript('/js/midiPlayer.js');
                }
            } catch (err) {
                console.error('Failed to load midiPlayer.js', err);
                throw err;
            }

            // Wait a tick for midiPlayer to initialize
            return new Promise((resolve, reject) => {
                const start = Date.now();
                const timeout = 5000;
                (function check() {
                    if (window.midiPlayer && typeof window.midiPlayer.playSingleNote === 'function') return resolve(true);
                    if (Date.now() - start > timeout) return reject(new Error('midiPlayer did not initialize in time'));
                    setTimeout(check, 50);
                })();
            });
        })();

        return midiPlayerReadyPromise;
    }

    window.playNote = async function (cell) {
        try {
            const noteAttr = cell.dataset.note || '';
            const octaveAttr = cell.dataset.octave || '';
            let name = noteAttr.trim();
            let octave = parseInt(octaveAttr);

            if (!name) {
                // fallback parse innerText (e.g. "C4" or "C#4")
                const txt = cell.innerText.trim();
                const m = txt.match(/^([A-G][#b]?)(\d+)$/);
                if (m) {
                    name = m[1];
                    octave = parseInt(m[2]);
                }
            }

            if (!name) {
                console.warn('No note name found for cell', cell);
                return;
            }

            if (isNaN(octave)) octave = 4;

            const semitone = noteMap[name];
            if (semitone === undefined) {
                console.warn('Unknown note name', name);
                return;
            }

            const midi = (octave + 1) * 12 + semitone;

            // Choose instrument by cell type: draw -> strings, blow -> piano
            const type = (cell.dataset.type || '').toLowerCase();
            // const program = type === 'draw' ? STRING_ENSEMBLE_PROGRAM : PIANO_PROGRAM;
            const program = PIANO_PROGRAM;

            if (!window.midiPlayer || typeof window.midiPlayer.playSingleNote !== 'function') {
                // Attempt to load midiPlayer and its dependency
                try {
                    await ensureMidiPlayerLoaded();
                } catch (err) {
                    console.warn('midiPlayer not available and failed to load; cannot play note');
                    return;
                }
            }

            // Play using midiPlayer
            window.midiPlayer.playSingleNote(midi, 0.6, program);
        } catch (err) {
            console.error(err);
        }
    };
})()