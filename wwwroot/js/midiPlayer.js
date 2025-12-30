// wwwroot/js/midiPlayer.js

// Declare audio context and players at the module level
let audioContext = null;
let pianoPlayer = null;
let stringPlayer = null;

// Store the timeout ID for stopping the loop
let currentLoopTimeout = null;

// Track if music is currently playing
let isPlayingFlag = false;


// MIDI Program Change numbers for instruments (from ChordStylingService.cs and ChordPicker.cshtml)
const PIANO_PROGRAM = 0; // Acoustic Grand Piano
const STRING_ENSEMBLE_PROGRAM = 48; // String Ensemble 1

// Gain settings (from ChordPicker.cshtml)
const PIANO_GAIN = 1.0; // Full volume for piano
const STRING_GAIN = 0.3; // Reduced volume for strings

// Local Soundfont Path (from ChordPicker.cshtml)
const LOCAL_SOUNDFONT_PATH = '/soundfonts/MusyngKite/';

/**
 * Builds the local path for a Soundfont instrument.
 * @param {string} name - The name of the instrument (e.g., 'acoustic_grand_piano').
 * @returns {string} The full local URL to the Soundfont .js file.
 */
function buildLocalSoundfontUrl(name) {
    return LOCAL_SOUNDFONT_PATH + name + '-mp3.js';
}

/**
 * Initializes the Web Audio API context.
 */
function initializeAudioContext() {
    if (!audioContext) {
        audioContext = new (window.AudioContext || window.webkitAudioContext)();
        console.log("Web Audio API context initialized.");
    }
    return audioContext;
}

/**
 * Loads the piano and string ensemble instruments.
 * @param {HTMLElement} loadingMessageElement - The HTML element to show/hide loading messages.
 * @param {HTMLElement} playButtonElement - The HTML play button to disable/enable.
 * @returns {Promise<boolean>} A promise that resolves with the loaded instrument.
 */
async function loadInstruments(loadingMessageElement, playButtonElement) {
    initializeAudioContext(); // Ensure context is ready

    if (pianoPlayer === null || stringPlayer === null) {
        if (loadingMessageElement) loadingMessageElement.style.display = 'block';
        if (playButtonElement) playButtonElement.disabled = true;

        // Ensure Soundfont.js is loaded
        if (typeof Soundfont === 'undefined') {
            console.error("Soundfont.js is not loaded. Cannot load instruments. Make sure <script src=\"~/js/soundfont-player.min.js\"></script> is in your page.");
            if (loadingMessageElement) loadingMessageElement.textContent = "Error: Soundfont player not loaded.";
            return false;
        }

        try {
            console.log("Loading Soundfont instruments: Piano and String Ensemble.");
            [pianoPlayer, stringPlayer] = await Promise.all([
                Soundfont.instrument(audioContext, 'acoustic_grand_piano', {
                    format: 'mp3',
                    nameToUrl: buildLocalSoundfontUrl
                }),
                Soundfont.instrument(audioContext, 'string_ensemble_1', {
                    format: 'mp3',
                    nameToUrl: buildLocalSoundfontUrl
                })
            ]);

            if (loadingMessageElement) loadingMessageElement.style.display = 'none';
            if (playButtonElement) playButtonElement.disabled = false;
            console.log("Instruments loaded successfully from local path: Piano and String Ensemble.");
            return true;
        } catch (error) {
            console.error("Error loading soundfont instruments. Please double check: 1) local .js files are in 'wwwroot/soundfonts/MusyngKite/'. 2) Their names are exactly 'instrument_name-mp3.js'. 3) The 'format: 'mp3'' option is correct.", error);
            if (loadingMessageElement) loadingMessageElement.textContent = "Error loading instruments. Check console (F12).";
            return false;
        }
    }
    return true; // Instruments already loaded
}

/**
 * Plays a stylized chord progression using the loaded Soundfont instruments.
 * @param {Array<Object>} stylizedMidiEvents - An array of StylizedMidiEvent objects.
 * @param {boolean} loopPlayback - Whether the progression should loop.
 * @param {number} totalProgressionDuration - The total duration of the progression in seconds.
 */
function playProgression(stylizedMidiEvents, loopPlayback, totalProgressionDuration) {
    if (!pianoPlayer || !stringPlayer) {
        console.error("Instruments not loaded. Cannot play progression.");
        return;
    }

    stopAllNotes(false); // Stop any currently scheduled notes/loops, but don't clear the playing flag

    isPlayingFlag = true; // Set playing flag

    const currentAudioTime = audioContext.currentTime;

    stylizedMidiEvents.forEach(event => {
        const safePitch = Math.max(0, Math.min(127, event.pitch));
        let playerToUse;
        let gainToApply = 1.0;

        if (event.instrumentProgram === PIANO_PROGRAM) {
            playerToUse = pianoPlayer;
            gainToApply = PIANO_GAIN;
        } else if (event.instrumentProgram === STRING_ENSEMBLE_PROGRAM) {
            playerToUse = stringPlayer;
            gainToApply = STRING_GAIN;
        } else {
            console.warn(`Unknown instrument program: ${event.instrumentProgram}. Defaulting to piano.`);
            playerToUse = pianoPlayer;
            gainToApply = PIANO_GAIN;
        }

        if (playerToUse) {
            playerToUse.play(safePitch, currentAudioTime + event.startTime, { duration: event.duration, gain: gainToApply });
        }
    });

    // Schedule the next loop iteration precisely
    if (loopPlayback) {
        const endOfCurrentPassTime = currentAudioTime + totalProgressionDuration;
        const delayToNextPlayback = (endOfCurrentPassTime - audioContext.currentTime) * 1000;

        const finalDelay = Math.max(0, delayToNextPlayback + 50);
        currentLoopTimeout = setTimeout(() => {
            playProgression(stylizedMidiEvents, loopPlayback, totalProgressionDuration);
        }, finalDelay);
    }
}

/**
 * Plays a single MIDI note using the loaded instrument players. Exposed for single-note playback from UI.
 * @param {number} midi - MIDI note number (0-127)
 * @param {number} [duration=0.6] - Duration in seconds
 * @param {number} [program=PIANO_PROGRAM] - MIDI program to choose player (PIANO_PROGRAM or STRING_ENSEMBLE_PROGRAM)
 * @returns {Promise<boolean>} - Resolves true if note played, false otherwise
 */
async function playSingleNote(midi, duration = 0.6, program = PIANO_PROGRAM) {
    try {
        initializeAudioContext();

        // Attempt to load instruments if not already loaded. We pass nulls for DOM elements.
        const ok = await loadInstruments(null, null);
        if (!ok) return false;

        const safePitch = Math.max(0, Math.min(127, Math.round(midi)));
        let playerToUse = pianoPlayer;
        let gainToApply = PIANO_GAIN;

        if (program === STRING_ENSEMBLE_PROGRAM) {
            playerToUse = stringPlayer;
            gainToApply = STRING_GAIN;
        }

        if (!playerToUse) {
            console.error('Requested player not available');
            return false;
        }

        // Use current audio time to schedule immediately
        const startTime = audioContext.currentTime + 0.01;
        playerToUse.play(safePitch, startTime, { duration: duration, gain: gainToApply });

        // Set playing flag briefly
        isPlayingFlag = true;
        setTimeout(() => { isPlayingFlag = false; }, duration * 1000 + 50);

        return true;
    } catch (err) {
        console.error('Error in playSingleNote', err);
        return false;
    }
}

/**
 * Stops all active notes and clears any pending loop timeouts.
 * @param {boolean} clearPlayingFlag - If true, also resets the isPlayingFlag. Useful for manual stop button.
 */
function stopAllNotes(clearPlayingFlag = true) {
    if (pianoPlayer) pianoPlayer.stop();
    if (stringPlayer) stringPlayer.stop();
    if (currentLoopTimeout) {
        clearTimeout(currentLoopTimeout);
        currentLoopTimeout = null;
    }
    if (clearPlayingFlag) {
        isPlayingFlag = false;
    }
    console.log("All notes stopped and loop cleared. Playing flag cleared:", clearPlayingFlag);
}

/**
 * Checks if music is currently playing.
 * @returns {boolean} True if music is playing, false otherwise.
 */
function isPlaying() {
    return isPlayingFlag;
}

// Expose functions globally for use in ChordPicker.cshtml
window.midiPlayer = {
    loadInstruments: loadInstruments,
    playProgression: playProgression,
    stopAllNotes: stopAllNotes,
    isPlaying: isPlaying, // NEW: Expose isPlaying status
    playSingleNote: playSingleNote // NEW: Expose single-note playback
};