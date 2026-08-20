// Space bar deals the next hand in the Twenty Four game.
// A document-level listener, because after a solve there's nothing sensible
// focused for a component-level handler to hang off.
window.oadTwentyFour = {
    _handler: null,

    register: function (dotNetRef) {
        this.unregister();
        this._handler = (event) => {
            if (event.code !== 'Space' && event.key !== ' ') {
                return;
            }
            // Never steal the space bar from someone typing — the expression box
            // needs it ("3 + 5"), as does any other field on the page.
            const active = document.activeElement;
            const tag = active ? active.tagName.toLowerCase() : '';
            if (tag === 'input' || tag === 'textarea' || (active && active.isContentEditable)) {
                return;
            }
            // Space scrolls the page by default; the deal is the intent here.
            event.preventDefault();
            dotNetRef.invokeMethodAsync('DealFromSpaceBar');
        };
        document.addEventListener('keydown', this._handler);
    },

    unregister: function () {
        if (this._handler) {
            document.removeEventListener('keydown', this._handler);
            this._handler = null;
        }
    },
};
