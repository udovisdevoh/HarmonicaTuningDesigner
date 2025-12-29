// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.


<script>
    function playNote(cell) {
    const hole = cell.dataset.hole;
    const type = cell.dataset.type;
    const note = cell.innerText;

    // MIDI logic goes here (you’ll plug this in)
    console.log(`Play ${note} (${type}) on hole ${hole}`);
}
</script>