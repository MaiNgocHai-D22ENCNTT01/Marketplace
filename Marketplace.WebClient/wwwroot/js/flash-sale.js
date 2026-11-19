(() => {
    const host = document.querySelector('[data-flash-url]');
    if (!host) return;
    function countdown() {
        const section = host.querySelector('[data-flash-deadline]');
        if (!section) return;
        const duration = Date.parse(section.dataset.flashDeadline) - Date.parse(section.dataset.serverTime);
        const started = performance.now();
        const display = section.querySelector('[role=timer]');
        async function tick() {
            const seconds = Math.max(0, Math.ceil((duration - performance.now() + started) / 1000));
            display.textContent = [Math.floor(seconds/3600), Math.floor(seconds/60)%60, seconds%60]
                .map(n => String(n).padStart(2,'0')).join(':');
            if (seconds) { setTimeout(tick, 500); return; }
            section.querySelectorAll('button').forEach(b => b.disabled = true);
            try {
                const response = await fetch(host.dataset.flashUrl, { cache:'no-store' });
                if (!response.ok) throw new Error();
                host.innerHTML = await response.text();
                countdown();
            } catch { setTimeout(tick, 5000); }
        }
        tick();
    }
    countdown();
})();
