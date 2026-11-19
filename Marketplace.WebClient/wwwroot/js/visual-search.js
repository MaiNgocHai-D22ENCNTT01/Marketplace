const visual = document.querySelector('#visual-dialog');
const fileInput = visual.querySelector('input[type=file]');
const preview = visual.querySelector('.preview');
const zone = visual.querySelector('.drop-zone');
const busy = visual.querySelector('.analysis-state');
const submit = visual.querySelector('button:not([type=button])');
let previewUrl;

let version = 0;
let prepared;

async function showPreview() {
    const selection = ++version;
    const file = fileInput.files[0];
    prepared = null;
    submit.disabled = true;
    if (previewUrl) URL.revokeObjectURL(previewUrl);
    preview.hidden = true;
    preview.removeAttribute('src');
    const message = visual.querySelector('.upload-error');
    message.textContent = '';
    fileInput.setCustomValidity('');
    if (!file) return;
    try {
        if (!file.size || file.size > 10 * 1024 * 1024) throw Error('Vui lòng chọn ảnh tối đa 10 MiB.');
        if (!['image/jpeg','image/png','image/webp'].includes(file.type)) throw Error('Vui lòng chọn ảnh JPG, PNG hoặc WEBP.');
        const image = await createImageBitmap(file);
        const canvas = document.createElement('canvas');
        canvas.width = canvas.height = 224;
        try {
            const ctx = canvas.getContext('2d');
            ctx.fillStyle = '#fff';
            ctx.fillRect(0, 0, 224, 224);
            const scale = Math.min(224 / image.width, 224 / image.height);
            const w = image.width * scale, h = image.height * scale;
            ctx.drawImage(image, (224-w)/2, (224-h)/2, w, h);
        } finally { image.close(); }
        let blob;
        for (const quality of [0.85, 0.7, 0.5, 0.32]) {
            const encode = type => new Promise(resolve => canvas.toBlob(resolve, type, quality));
            blob = await encode('image/webp');
            if (blob?.type !== 'image/webp') blob = await encode('image/jpeg');
            if (blob && blob.size <= 30 * 1024) break;
        }
        if (!blob || blob.size > 30 * 1024) throw Error('Không thể nén ảnh. Vui lòng chọn ảnh khác.');
        if (selection !== version) return;
        prepared = new File([blob], blob.type === 'image/webp' ? 'search.webp' : 'search.jpg', {type: blob.type});
        const transfer = new DataTransfer();
        transfer.items.add(prepared);
        fileInput.files = transfer.files;
        previewUrl = URL.createObjectURL(blob);
        preview.src = previewUrl;
        preview.hidden = false;
        submit.disabled = false;
    } catch (error) {
        if (selection !== version) return;
        message.textContent = error instanceof DOMException ? 'Không thể đọc ảnh.' : error.message;
        fileInput.setCustomValidity(message.textContent);
    }
}
fileInput.addEventListener('change', showPreview);
for (const type of ['dragenter', 'dragover', 'dragleave', 'drop']) {
    zone.addEventListener(type, event => {
        event.preventDefault();
        zone.classList.toggle('dragging', type === 'dragover' || type === 'dragenter');
        if (type === 'drop' && event.dataTransfer.files.length) {
            const transfer = new DataTransfer();
            transfer.items.add(event.dataTransfer.files[0]);
            fileInput.files = transfer.files;
            showPreview();
        }
    });
}
visual.querySelector('form').addEventListener('submit', event => {
    if (!prepared) { event.preventDefault(); return; }
    busy.hidden = false;
    submit.disabled = true;
});
window.addEventListener('pageshow', () => { busy.hidden = true; submit.disabled = !prepared; });
document.addEventListener('click', event => {
    if (event.target.closest('[data-visual]')) visual.showModal();
    if (event.target.closest('[data-close]')) event.target.closest('dialog').close();
    const menu = document.querySelector('.user-menu');
    if (menu && !menu.contains(event.target)) menu.open = false;
    const button = event.target.closest('.quick');
    if (!button) return;
    const card = button.closest('.product-card');
    const dialog = document.querySelector('#quick-dialog');
    const photo = card.querySelector('.product-photo img');
    const img = dialog.querySelector('img');
    img.hidden = !photo;
    if (photo) img.src = photo.src;
    const link = card.querySelector('h3 a');
    dialog.querySelector('h2').textContent = link.textContent;
    dialog.querySelector('a').href = link.href;
    dialog.querySelector('.price').textContent = card.querySelector('.price').textContent;
    dialog.querySelector('small').textContent = card.querySelector('small').textContent;
    dialog.showModal();
});
document.addEventListener('change', event => {
    if (!event.target.matches('.variants input')) return;
    const quantity = event.target.form.elements.Quantity;
    quantity.max = event.target.dataset.stock;
    if (Number(quantity.value) > Number(quantity.max)) quantity.value = quantity.max;
    document.querySelector('.detail-price').textContent = event.target.dataset.price;
});
