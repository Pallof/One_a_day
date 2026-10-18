// Keeps today's question, and everything on yesterday's page, from being copied (PRD 01), so
// someone who wants to look a puzzle up has to type it out. A deterrent for honest solvers, not a lock: the text is in the page for
// anyone who opens the browser's developer tools, and a phone can read a screenshot.
//
// The stylesheet already stops it being selected (.oad-no-copy). This catches what that
// misses: a select-all, or a browser that copies unselectable text anyway. A copy, cut or drag
// whose selection touches guarded text copies nothing at all. Pasting is never touched, so an
// answer can still be pasted in.
//
// The listeners sit on the document, like the scratch pad's, so they cover whichever copy of
// the page is on screen.
(function () {
    function touchesPuzzle(selection) {
        const protectedText = document.querySelectorAll('.oad-no-copy');
        for (let i = 0; i < selection.rangeCount; i++) {
            const range = selection.getRangeAt(i);
            for (const element of protectedText) {
                if (range.intersectsNode(element)) {
                    return true;
                }
            }
        }
        return false;
    }

    function refuse(event) {
        const selection = document.getSelection();
        if (selection && !selection.isCollapsed && touchesPuzzle(selection)) {
            event.preventDefault();
        }
    }

    document.addEventListener('copy', refuse);
    document.addEventListener('cut', refuse);
    document.addEventListener('dragstart', refuse);
})();
