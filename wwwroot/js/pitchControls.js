(function(){
    // Map note name to semitone
    const semitoneMap = { 'C':0,'C#':1,'Db':1,'D':2,'D#':3,'Eb':3,'E':4,'F':5,'F#':6,'Gb':6,'G':7,'G#':8,'Ab':8,'A':9,'A#':10,'Bb':10,'B':11 };
    const names = ['C','C#','D','D#','E','F','F#','G','G#','A','A#','B'];

    function parseNote(text){
        if (!text) return null;
        const m = String(text).trim().match(/^([A-G][#b]?)(-?\d+)$/);
        if (!m) return null;
        return { name: m[1], octave: parseInt(m[2],10) };
    }

    function updateHiddenInputs(button, newName, newOct){
        const base = `ViewModel.${button.getAttribute('data-plate')}.Holes[${button.getAttribute('data-array-index')}]`;
        const side = button.getAttribute('data-side');
        const specificNoteInput = document.querySelector(`input[name='${base}.${side}.Note']`);
        const specificOctaveInput = document.querySelector(`input[name='${base}.${side}.Octave']`);
        const specificAlteredInput = document.querySelector(`input[name='${base}.${side}.IsAltered']`);

        const container = button.closest('.note-cell');
        if (specificNoteInput) specificNoteInput.value = newName;
        if (specificOctaveInput) specificOctaveInput.value = newOct;
        if (specificAlteredInput) specificAlteredInput.value = newName.includes('#');

        // update data attributes too for playNote
        if (container) {
            container.dataset.note = newName;
            container.dataset.octave = newOct;
            const span = container.querySelector('span');
            if (span) span.textContent = `${newName}${newOct}`;
        }
    }

    async function persistChangeToServer(button, holeIndex, side, dir){
        const plate = button.getAttribute('data-plate');
        const url = `${window.location.pathname}?handler=AdjustPitch&plateId=${encodeURIComponent(plate)}&holeIndex=${encodeURIComponent(holeIndex)}&side=${encodeURIComponent(side)}&dir=${encodeURIComponent(dir)}`;
        try {
            const resp = await fetch(url, { method: 'GET', headers: { 'X-Requested-With': 'XMLHttpRequest' } });
            if (!resp.ok) return null;
            const j = await resp.json();
            return j;
        } catch (e) {
            console.debug('persistChangeToServer error', e);
            return null;
        }
    }

    async function triggerDetectionsForPlate(plateEl){
        try{
            if (window.chordDetect && typeof window.chordDetect === 'function'){
                const local = window.chordDetect(plateEl);
                plateEl.dispatchEvent(new CustomEvent('chordsDetected', { detail: local, bubbles: true, composed: true }));
            }
        } catch (e) { console.debug('trigger local detection error', e); }
        try{
            if (window.updateGlobalChordsTable && typeof window.updateGlobalChordsTable === 'function'){
                window.updateGlobalChordsTable();
            }
        } catch (e) { console.debug('trigger server update error', e); }
    }

    async function adjust(button, dir){
        const container = button.closest('.note-cell');
        const side = button.getAttribute('data-side');
        if (!container) return;
        const noteText = container.dataset.note || container.querySelector('span')?.innerText || '';
        const octaveAttr = container.dataset.octave || '';
        let parsed = parseNote(noteText) || (octaveAttr ? { name: noteText, octave: parseInt(octaveAttr,10)||4 } : null);
        if (!parsed) {
            const sp = container.querySelector('span')?.innerText || '';
            parsed = parseNote(sp) || { name: 'C', octave: 4 };
        }
        let sem = semitoneMap[parsed.name] ?? 0;
        let midi = (parsed.octave + 1) * 12 + sem;
        midi += dir === 'up' ? 1 : -1;
        midi = Math.max(0, Math.min(127, midi));
        const newSem = midi % 12;
        const newOct = Math.floor(midi/12) - 1;
        const newName = names[newSem];

        // Try to persist via server endpoint
        const res = await persistChangeToServer(button, parseInt(button.getAttribute('data-array-index')) + 1, side, dir);
        const plateEl = document.querySelector(`.reedplate[data-plate='${button.getAttribute('data-plate')}']`);
        if (res && res.success) {
            updateHiddenInputs(button, res.note, res.octave);
            // play note via midiPlayer if available
            try {
                if (window.midiPlayer && typeof window.midiPlayer.playSingleNote === 'function') {
                    const midiNum = (res.octave + 1) * 12 + (semitoneMap[res.note] ?? 0);
                    window.midiPlayer.playSingleNote(midiNum, 0.6);
                }
            } catch (e) { console.debug('play error', e); }

            // Dispatch event to trigger chord detection for this plate
            if (plateEl) plateEl.dispatchEvent(new CustomEvent('notesChanged', { bubbles: true, composed: true }));
            // Also call detection helpers directly to be robust
            if (plateEl) await triggerDetectionsForPlate(plateEl);
        } else {
            // Fallback to local update if server not reachable
            updateHiddenInputs(button, newName, newOct);
            try {
                if (window.midiPlayer && typeof window.midiPlayer.playSingleNote === 'function') {
                    window.midiPlayer.playSingleNote(midi, 0.6);
                }
            } catch (e) { console.debug('play fallback error', e); }
            if (plateEl) plateEl.dispatchEvent(new CustomEvent('notesChanged', { bubbles: true, composed: true }));
            if (plateEl) await triggerDetectionsForPlate(plateEl);
        }
    }

    document.addEventListener('click', function(e){
        const b = e.target.closest('.pitch-btn');
        if (!b) return;
        e.preventDefault();
        const dir = b.getAttribute('data-dir');
        adjust(b, dir);
    });

    // Expose helper to trigger detection externally
    window.triggerChordDetection = function(plateEl) {
        if (!plateEl) return;
        plateEl.dispatchEvent(new CustomEvent('notesChanged', { bubbles: true, composed: true }));
        try{ if (window.chordDetect) { const local = window.chordDetect(plateEl); plateEl.dispatchEvent(new CustomEvent('chordsDetected', { detail: local, bubbles: true, composed: true })); } } catch(e){console.debug(e);} 
        try{ if (window.updateGlobalChordsTable) window.updateGlobalChordsTable(); } catch(e){console.debug(e);} 
    };

    // On load, trigger detection once so initial chords are computed client-side if needed
    document.addEventListener('DOMContentLoaded', function(){
        document.querySelectorAll('.reedplate').forEach(function(p){ p.dispatchEvent(new CustomEvent('notesChanged', { bubbles: true, composed: true })); try{ if (window.chordDetect){ const local = window.chordDetect(p); p.dispatchEvent(new CustomEvent('chordsDetected', { detail: local, bubbles: true, composed: true })); } }catch(e){console.debug(e);} });
        try{ if (window.updateGlobalChordsTable) window.updateGlobalChordsTable(); } catch(e){console.debug(e);} 
    });
})();
