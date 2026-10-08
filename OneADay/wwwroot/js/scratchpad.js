// The challenge page's scratch pad (PRD 01): paper for working a puzzle out, for anyone
// without pen and paper to hand. Pen, eraser and undo.
//
// All of it happens here in the browser. Nothing goes over the live connection — every
// stroke would be a round trip, laggy on a phone and work for the server — and nothing is
// saved: leaving or reloading the page wipes it, like scrap paper. Closing the pad only
// hides it, so the drawing is still there when it opens again.
//
// The listeners sit on the document, not on the pad, so they work for whichever copy of the
// pad is on screen: the page is drawn twice on arrival, and afresh on every return to it.
(function () {
    const PEN_WIDTH = 2.5;
    const ERASER_WIDTH = 20;

    // Each pad's strokes, kept by its canvas — a new canvas starts blank.
    const pads = new WeakMap();

    // The stroke being drawn: one finger or button at a time.
    let active = null;

    function padOf(canvas) {
        let pad = pads.get(canvas);
        if (!pad) {
            pad = { strokes: [], tool: 'pen', scale: 1 };
            pads.set(canvas, pad);
        }
        return pad;
    }

    function ink() {
        return getComputedStyle(document.documentElement).getPropertyValue('--ink').trim() || '#22334D';
    }

    // Sizes the bitmap to the pad as shown, so lines are sharp on any screen, then redraws.
    // The inside of the pad, not its border, so a point lands exactly where it's touched.
    function fit(canvas) {
        if (canvas.clientWidth === 0) {
            return;   // hidden
        }
        const pad = padOf(canvas);
        pad.scale = window.devicePixelRatio || 1;
        canvas.width = Math.round(canvas.clientWidth * pad.scale);
        canvas.height = Math.round(canvas.clientHeight * pad.scale);
        redraw(canvas);
    }

    function redraw(canvas) {
        const pad = padOf(canvas);
        const ctx = canvas.getContext('2d');
        ctx.setTransform(1, 0, 0, 1, 0, 0);
        ctx.clearRect(0, 0, canvas.width, canvas.height);
        for (const stroke of pad.strokes) {
            paint(canvas, stroke, 0);
        }
    }

    // Draws a stroke from point `from` on: all of it on a redraw, the newest bit while drawing.
    function paint(canvas, stroke, from) {
        const ctx = canvas.getContext('2d');
        const points = stroke.points;
        const start = Math.max(from - 1, 0);
        ctx.save();
        ctx.setTransform(padOf(canvas).scale, 0, 0, padOf(canvas).scale, 0, 0);
        // The eraser takes ink away rather than painting white, so it never leaves a mark.
        ctx.globalCompositeOperation = stroke.erase ? 'destination-out' : 'source-over';
        ctx.strokeStyle = ink();
        ctx.lineWidth = stroke.erase ? ERASER_WIDTH : PEN_WIDTH;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        ctx.beginPath();
        ctx.moveTo(points[start].x, points[start].y);
        if (points.length === 1) {
            ctx.lineTo(points[0].x + 0.01, points[0].y);   // a tap leaves a dot
        }
        for (let i = start + 1; i < points.length; i++) {
            ctx.lineTo(points[i].x, points[i].y);
        }
        ctx.stroke();
        ctx.restore();
    }

    function pointOn(canvas, event) {
        const box = canvas.getBoundingClientRect();
        return {
            x: event.clientX - box.left - canvas.clientLeft,
            y: event.clientY - box.top - canvas.clientTop,
        };
    }

    // The ring showing the eraser's reach — exactly ERASER_WIDTH across, so what it circles is
    // what gets wiped. The stylesheet shows it only while the eraser is picked.
    function ringAt(canvas, point) {
        const ring = canvas.parentElement.querySelector('.sp-ring');
        ring.style.width = ring.style.height = ERASER_WIDTH + 'px';
        ring.style.left = (point.x + canvas.clientLeft) + 'px';
        ring.style.top = (point.y + canvas.clientTop) + 'px';
        ring.style.visibility = 'visible';
    }

    document.addEventListener('pointerdown', (event) => {
        const canvas = event.target.closest && event.target.closest('.sp-canvas');
        if (!canvas || active) {
            return;
        }
        const pad = padOf(canvas);
        const point = pointOn(canvas, event);
        const stroke = { erase: pad.tool === 'eraser', points: [point] };
        pad.strokes.push(stroke);
        active = { canvas, stroke, pointer: event.pointerId };
        canvas.setPointerCapture(event.pointerId);
        paint(canvas, stroke, 0);
        ringAt(canvas, point);
        event.preventDefault();
    });

    // Drawing, or — with a mouse — just passing over, when the ring follows the pointer.
    document.addEventListener('pointermove', (event) => {
        if (active && event.pointerId === active.pointer) {
            const point = pointOn(active.canvas, event);
            active.stroke.points.push(point);
            paint(active.canvas, active.stroke, active.stroke.points.length - 1);
            ringAt(active.canvas, point);
            return;
        }
        const canvas = !active && event.target.closest && event.target.closest('.sp-canvas');
        if (canvas) {
            ringAt(canvas, pointOn(canvas, event));
        }
    });

    // A mouse leaving the pad takes the ring with it. A finger lifting leaves it where it was:
    // under the finger it can't be seen, so this is when a phone shows it.
    document.addEventListener('pointerout', (event) => {
        const canvas = event.target.closest && event.target.closest('.sp-canvas');
        if (canvas && event.pointerType === 'mouse' && !active) {
            canvas.parentElement.querySelector('.sp-ring').style.visibility = 'hidden';
        }
    });

    function endStroke(event) {
        if (active && event.pointerId === active.pointer) {
            active = null;
        }
    }
    document.addEventListener('pointerup', endStroke);
    document.addEventListener('pointercancel', endStroke);

    document.addEventListener('click', (event) => {
        const button = event.target.closest && event.target.closest('[data-sp]');
        if (!button) {
            return;
        }
        const root = button.closest('.sp');
        const canvas = root.querySelector('.sp-canvas');
        const pad = padOf(canvas);

        switch (button.dataset.sp) {
            case 'toggle': {
                const open = !root.hasAttribute('data-open');
                root.toggleAttribute('data-open', open);
                root.querySelector('.sp-launcher').setAttribute('aria-expanded', String(open));
                if (open) {
                    fit(canvas);
                }
                break;
            }
            case 'pen':
            case 'eraser':
                pad.tool = button.dataset.sp;
                root.dataset.tool = pad.tool;
                for (const tool of root.querySelectorAll('[data-sp="pen"], [data-sp="eraser"]')) {
                    tool.setAttribute('aria-pressed', String(tool === button));
                }
                // Picking the eraser shows its reach straight away, mid-pad — before a finger
                // covers it.
                if (pad.tool === 'eraser') {
                    ringAt(canvas, { x: canvas.clientWidth / 2, y: canvas.clientHeight / 2 });
                }
                break;
            case 'undo':
                pad.strokes.pop();
                redraw(canvas);
                break;
        }
    });

    // A turned phone or resized window changes the pad's size: refit, and redraw what's there.
    window.addEventListener('resize', () => {
        for (const canvas of document.querySelectorAll('.sp[data-open] .sp-canvas')) {
            fit(canvas);
        }
    });
})();
