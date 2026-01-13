(function(){
    // Client-side chord detection (mirrors server heuristics enough for instant feedback)
    function normalize(name){
        if (!name) return '';
        const s = String(name).trim().replace(/\d+$/, '');
        if (s.includes('/')) return s.split('/')[0];
        return s;
    }
    const names = ['C','C#','D','D#','E','F','F#','G','G#','A','A#','B'];
    const map = {};
    names.forEach((n,i)=>map[n]=i);
    map['Db']=map['C#']; map['Eb']=map['D#']; map['Gb']=map['F#']; map['Ab']=map['G#']; map['Bb']=map['A#'];

    const intervals = {
        Major: [0,4,7],
        Minor: [0,3,7],
        Diminished: [0,3,6],
        Augmented: [0,4,8]
    };

    function detectOnPlate(plateEl){
        if (!plateEl) return [];
        const holes = Array.from(plateEl.querySelectorAll('.hole-column'));
        const blowSeq = holes.map(h=>{
            const cell = h.querySelector('.note-cell.blow');
            const n = normalize(cell?.dataset.note || cell?.querySelector('span')?.innerText || '');
            return { name: n, sem: map[n] ?? -1 };
        });
        const drawSeq = holes.map(h=>{
            const cell = h.querySelector('.note-cell.draw');
            const n = normalize(cell?.dataset.note || cell?.querySelector('span')?.innerText || '');
            return { name: n, sem: map[n] ?? -1 };
        });

        function find(seq, isBlow){
            const results = [];
            for (let start=0; start<seq.length; start++){
                for (let end=start; end<seq.length; end++){
                    const sub = seq.slice(start, end+1);
                    if (sub.length < 3) continue;
                    const uniq = Array.from(new Set(sub.map(s=>((s.sem%12)+12)%12).filter(s=>s>=0)));
                    if (uniq.length < 2) continue;
                    for (const [type, ints] of Object.entries(intervals)){
                        for (let root=0; root<12; root++){
                            const required = ints.map(i=> (root + i) % 12);
                            const ok = required.every(r => uniq.includes(r));
                            if (ok){
                                results.push({ Root: names[root], Type: type, StartHoleIndex: start+1, EndHoleIndex: end+1, IsBlow: isBlow, Notes: sub.map(x=>x.name) });
                                break;
                            }
                        }
                        if (results.length) break;
                    }
                }
            }
            return results;
        }

        return find(blowSeq, true).concat(find(drawSeq, false));
    }

    // Server-side update for authoritative global chords table
    function collectPlatesData(){
        const plates = Array.from(document.querySelectorAll('.reedplate'));
        const out = plates.map(p => {
            const plateId = p.getAttribute('data-plate');
            const holes = Array.from(p.querySelectorAll('.hole-column')).map(h => {
                const blow = h.querySelector('.note-cell.blow');
                const draw = h.querySelector('.note-cell.draw');
                const bnote = (blow && (blow.dataset.note || blow.querySelector('span')?.innerText)) || '';
                const boct = parseInt((blow && blow.dataset.octave) || (blow && blow.querySelector('span')?.innerText?.match(/\d+$/)?.[0]) || '4', 10);
                const dnote = (draw && (draw.dataset.note || draw.querySelector('span')?.innerText)) || '';
                const doct = parseInt((draw && draw.dataset.octave) || (draw && draw.querySelector('span')?.innerText?.match(/\d+$/)?.[0]) || '4', 10);
                return { blow: { note: bnote, octave: boct }, draw: { note: dnote, octave: doct } };
            });
            return { plateId, holes };
        });
        return { plates: out };
    }

    function getRequestVerificationToken(){
        const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
        return tokenInput ? tokenInput.value : null;
    }

    async function postPlates(){
        const payload = collectPlatesData();
        try{
            const headers = { 'Content-Type': 'application/json' };
            const token = getRequestVerificationToken();
            if (token) headers['RequestVerificationToken'] = token;

            const resp = await fetch(window.location.pathname + '?handler=UpdateChords', {
                method: 'POST',
                headers: headers,
                body: JSON.stringify(payload)
            });
            if (!resp.ok) {
                console.error('postPlates response not ok', resp.status, resp.statusText);
                return null;
            }
            const json = await resp.json();
            console.debug('postPlates response JSON', json);
            return json;
        } catch (e) {
            console.error('postPlates error', e);
            return null;
        }
    }

    function normalizeServerChord(c){
        if (!c) return null;
        const type = c.Type ?? c.type ?? (c.TypeName ?? c.typeName) ?? (typeof c.Type === 'string' ? c.Type : null);
        const root = c.Root ?? c.root ?? (c.RootName ?? c.rootName) ?? (typeof c.Root === 'string' ? c.Root : null);
        const start = (c.Start ?? c.start ?? c.StartHoleIndex ?? c.startHoleIndex ?? c.StartIndex ?? c.startIndex) ?? null;
        const end = (c.End ?? c.end ?? c.EndHoleIndex ?? c.endHoleIndex ?? c.EndIndex ?? c.endIndex) ?? null;
        const isBlow = (c.IsBlow ?? c.isBlow) === true;
        let notes = c.Notes ?? c.notes ?? c.NotesList ?? c.notesList ?? [];
        if (!Array.isArray(notes)) {
            if (typeof notes === 'string' && notes.length > 0) notes = notes.split(' ');
            else notes = [];
        }
        return { Type: type ?? 'Unknown', Root: root ?? 'Unknown', Start: start ?? 'undefined', End: end ?? 'undefined', IsBlow: isBlow, Notes: notes };
    }

    function renderGlobalChords(chords){
        const tbody = document.getElementById('chords-tbody');
        if (!tbody) return;
        console.debug('renderGlobalChords called with', chords);
        if (!chords || chords.length === 0){
            tbody.innerHTML = '<tr><td colspan="5">No chords detected</td></tr>';
            return;
        }
        // Normalize server objects then render
        const norm = chords.map(normalizeServerChord).filter(x=>x!=null);
        const rows = norm.map(c => `<tr><td>${c.Type}</td><td>${c.Root}</td><td>${c.Start} - ${c.End}</td><td>${c.IsBlow ? 'Blow' : 'Draw'}</td><td>${(c.Notes||[]).join(' ')}</td></tr>`);
        tbody.innerHTML = rows.join('');
    }

    async function updateFromServer(){
        const res = await postPlates();
        console.debug('updateFromServer got', res);
        if (res && res.success){
            renderGlobalChords(res.chords);
            // update available notes
            const availContainer = document.querySelector('.available-notes-global');
            if (availContainer && Array.isArray(res.availableNotes)){
                if (res.availableNotes.length === 0){
                    availContainer.innerHTML = '<h3>Available Notes</h3><div>No notes available</div>';
                } else {
                    // build table HTML
                    const tableHtml = `<h3>Available Notes</h3><table class="available-notes-table"><thead><tr><th>Note</th></tr></thead><tbody>${res.availableNotes.map(n => `<tr><td>${n}</td></tr>`).join('')}</tbody></table>`;
                    availContainer.innerHTML = tableHtml;
                }
            }

            // update missing notes
            const missingContainer = document.querySelector('.missing-notes-global');
            if (missingContainer && Array.isArray(res.missingNotes)){
                if (res.missingNotes.length === 0){
                    missingContainer.innerHTML = '<h3>Missing Notes</h3><div>None — instrument contains all 12 semitones</div>';
                } else {
                    const tableHtml = `<h3>Missing Notes</h3><table class="available-notes-table"><thead><tr><th>Note</th></tr></thead><tbody>${res.missingNotes.map(n => `<tr><td>${n}</td></tr>`).join('')}</tbody></table>`;
                    missingContainer.innerHTML = tableHtml;
                }
            }
        } else {
            console.debug('updateFromServer received no chords or failed', res);
        }
    }

    // notesChanged -> run local detection instantly and dispatch chordsDetected, then refresh global table from server
    document.addEventListener('notesChanged', function(e){
        // Local instant detection
        const plate = e.target.closest && e.target.closest('.reedplate') ? e.target.closest('.reedplate') : null;
        if (plate) {
            const local = detectOnPlate(plate);
            plate.dispatchEvent(new CustomEvent('chordsDetected', { detail: local, bubbles: true }));
        }

        // Update server-verified global chords table (async)
        updateFromServer();
    });

    // Expose for direct calls
    window.chordDetect = function(plateEl){ return detectOnPlate(plateEl); };
    window.updateGlobalChordsTable = updateFromServer;

    // On load, trigger detection once (local + server global)
    document.addEventListener('DOMContentLoaded', function(){
        document.querySelectorAll('.reedplate').forEach(p => {
            const local = detectOnPlate(p);
            p.dispatchEvent(new CustomEvent('chordsDetected', { detail: local, bubbles: true }));
        });
        updateFromServer();
    });
})();
