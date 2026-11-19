(() => {
    const input = document.querySelector('#header-keyword');
    const panel = document.querySelector('#search-suggestions');
    const form = input.form;
    let timer, pending, generation = 0;
    function close() {
        generation++;
        clearTimeout(timer);
        pending?.abort();
        panel.hidden = true;
        panel.replaceChildren();
        input.setAttribute('aria-expanded', 'false');
    }
    function search() {
        close();
        const q = input.value.trim();
        if (q.length < 2) return;
        const current = generation;
        timer = setTimeout(async () => {
            pending = new AbortController();
            try {
                const url = new URL(panel.dataset.url, location.href);
                url.searchParams.set('q', q);
                const response = await fetch(url, { signal: pending.signal });
                if (!response.ok) return;
                const html = await response.text();
                if (current !== generation) return;
                panel.innerHTML = html;
                panel.hidden = !panel.querySelector('a');
                input.setAttribute('aria-expanded', String(!panel.hidden));
            } catch { /* The ordinary search form remains available. */ }
        }, 180);
    }
    input.addEventListener('input', search);
    form.addEventListener('focusout', e => { if (!form.contains(e.relatedTarget)) close(); });
    document.addEventListener('pointerdown', e => { if (!form.contains(e.target)) close(); });
    form.addEventListener('keydown', e => {
        if (panel.hidden) return;
        if (e.key === 'Escape') { close(); input.focus(); }
        if (e.key !== 'ArrowDown' && e.key !== 'ArrowUp') return;
        e.preventDefault();
        const links = [...panel.querySelectorAll('a')];
        const next = links.indexOf(document.activeElement) + (e.key === 'ArrowDown' ? 1 : -1);
        (next < 0 ? input : links[Math.min(next, links.length - 1)]).focus();
    });
})();
