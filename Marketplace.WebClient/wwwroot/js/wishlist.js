const busyHearts = new Set();
document.addEventListener('submit', async event => {
    const form = event.target.closest('.wish-form');
    if (!form || !form.dataset.signedIn) return;
    event.preventDefault();
    if (busyHearts.has(form.action)) return;
    busyHearts.add(form.action);
    const copies = [...document.querySelectorAll('.wish-form')].filter(f => f.action === form.action);
    copies.forEach(f => f.querySelector('button').disabled = true);
    try {
        const response = await fetch(form.action, {
            method: 'POST', body: new FormData(form), headers: { Accept: 'application/json' }
        });
        if (response.status === 401) { location.assign(form.dataset.login); return; }
        if (!response.ok) throw new Error();
        const { isWishlisted } = await response.json();
        for (const copy of copies) {
            const button = copy.querySelector('button');
            button.setAttribute('aria-pressed', String(isWishlisted));
            button.title = isWishlisted ? 'Bỏ yêu thích' : 'Thêm vào yêu thích';
            button.setAttribute('aria-label', button.title);
            button.firstElementChild.className = isWishlisted ? 'bi bi-heart-fill' : 'bi bi-heart';
        }
        if (!isWishlisted && form.closest('[data-wishlist-page]')) location.reload();
    } catch {
        window.alert('Chưa xác nhận được trạng thái yêu thích. Vui lòng tải lại trang để kiểm tra.');
    } finally {
        busyHearts.delete(form.action);
        copies.forEach(f => f.querySelector('button').disabled = false);
    }
});
