// Self-contained canvas confetti — no external libraries.
// Called from Blazor via JS interop: window.oadCelebrate()
window.oadCelebrate = function () {
    // Respect users who prefer reduced motion.
    if (window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches) {
        return;
    }

    const old = document.getElementById('oad-confetti');
    if (old) old.remove();

    const canvas = document.createElement('canvas');
    canvas.id = 'oad-confetti';
    canvas.style.cssText =
        'position:fixed;inset:0;width:100vw;height:100vh;pointer-events:none;z-index:9999';
    document.body.appendChild(canvas);

    const ctx = canvas.getContext('2d');
    const dpr = window.devicePixelRatio || 1;
    let W, H;
    function resize() {
        W = window.innerWidth;
        H = window.innerHeight;
        canvas.width = W * dpr;
        canvas.height = H * dpr;
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
    }
    resize();
    window.addEventListener('resize', resize);

    const colors = ['#f94144', '#f8961e', '#f9c74f', '#90be6d',
                    '#43aa8b', '#577590', '#ff6fb5', '#9b5de5'];
    const parts = [];

    function emit(x, y, centerAngle, spread, count, speed) {
        for (let i = 0; i < count; i++) {
            const a = centerAngle + (Math.random() - 0.5) * spread;
            const sp = speed * (0.55 + Math.random());
            parts.push({
                x, y,
                vx: Math.cos(a) * sp,
                vy: Math.sin(a) * sp,
                size: 5 + Math.random() * 7,
                color: colors[(Math.random() * colors.length) | 0],
                rot: Math.random() * Math.PI * 2,
                vr: (Math.random() - 0.5) * 0.4,
                circle: Math.random() < 0.5,
            });
        }
    }

    // Two side "party poppers" aimed inward-and-up, plus a center pop.
    emit(0, H, -Math.PI / 3, Math.PI / 3, 70, 15);          // bottom-left → up-right
    emit(W, H, -Math.PI + Math.PI / 3, Math.PI / 3, 70, 15); // bottom-right → up-left
    emit(W / 2, H * 0.72, -Math.PI / 2, Math.PI, 60, 13);    // center burst

    const gravity = 0.28;
    const drag = 0.99;
    const start = performance.now();
    let raf;

    function frame(now) {
        const t = now - start;
        ctx.clearRect(0, 0, W, H);
        let alive = false;
        const alpha = Math.max(0, 1 - t / 2800);
        for (const p of parts) {
            p.vy += gravity;
            p.vx *= drag;
            p.vy *= drag;
            p.x += p.vx;
            p.y += p.vy;
            p.rot += p.vr;
            if (alpha <= 0 || p.y > H + 30) continue;
            alive = true;
            ctx.save();
            ctx.globalAlpha = alpha;
            ctx.translate(p.x, p.y);
            ctx.rotate(p.rot);
            ctx.fillStyle = p.color;
            if (p.circle) {
                ctx.beginPath();
                ctx.arc(0, 0, p.size / 2, 0, Math.PI * 2);
                ctx.fill();
            } else {
                ctx.fillRect(-p.size / 2, -p.size / 2, p.size, p.size * 0.6);
            }
            ctx.restore();
        }
        if (alive && t < 4000) {
            raf = requestAnimationFrame(frame);
        } else {
            cancelAnimationFrame(raf);
            window.removeEventListener('resize', resize);
            canvas.remove();
        }
    }
    raf = requestAnimationFrame(frame);
};
