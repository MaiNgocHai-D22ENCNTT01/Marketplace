(() => {
    const dialog = document.querySelector('#review-dialog');
    const form = dialog.querySelector('form');
    const stars = [...form.querySelectorAll('[name=rating]')];
    const error = document.querySelector('#review-error');
    const update = () => stars.forEach(s => s.parentElement.classList.toggle('gold', +s.value <= +form.elements.rating.value));
    stars.forEach(s => s.addEventListener('change', update));
    document.addEventListener('click', e => {
        const btn = e.target.closest('[data-review-item]');
        if (!btn) return;
        form.reset();
        update();
        error.textContent = '';
        form.elements.orderItemId.value = btn.dataset.reviewItem;
        document.querySelector('#review-product').textContent = btn.dataset.productName;
        dialog.showModal();
    });
    async function jpeg(file) {
        const bitmap = await createImageBitmap(file);
        try {
            const scale = Math.min(1, 720 / Math.max(bitmap.width, bitmap.height));
            const canvas = document.createElement('canvas');
            canvas.width = Math.max(1, Math.round(bitmap.width * scale));
            canvas.height = Math.max(1, Math.round(bitmap.height * scale));
            const ctx = canvas.getContext('2d');
            ctx.fillStyle = 'white';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            ctx.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
            const image = canvas.toDataURL('image/jpeg', 0.65);
            if (image.length > 275000) throw new Error('Ảnh quá lớn sau khi thu nhỏ. Hãy chọn ảnh nhỏ hơn.');
            return image;
        } finally { bitmap.close(); }
    }
    form.addEventListener('submit', async e => {
        e.preventDefault();
        const send = form.querySelector('.btn-primary');
        if (send.disabled) return;
        send.disabled = true;
        error.textContent = '';
        try {
            const files = [...document.querySelector('#review-files').files];
            if (files.length > 3 || files.some(f => f.size > 8*1024*1024)) throw new Error('Chọn tối đa 3 ảnh, mỗi ảnh tối đa 8 MB.');
            const images = [];
            for (const f of files) images.push(await jpeg(f));
            for (const image of images) {
                const input = document.createElement('input');
                input.type = 'hidden'; input.name = 'images'; input.value = image;
                form.append(input);
            }
            // Multipart avoids the URL-encoded form value-count and request-line limits.
            form.enctype = 'multipart/form-data';
            form.submit();
        } catch (err) { error.textContent = err.name === 'Error' ? err.message : 'Không đọc được ảnh đã chọn.'; send.disabled = false; }
    });
})();
