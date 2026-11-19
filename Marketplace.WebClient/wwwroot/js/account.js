(() => {
    const dialog = document.getElementById('address-dialog');
    document.getElementById('add-address')?.addEventListener('click', () => dialog.showModal());
    document.getElementById('close-address')?.addEventListener('click', () => dialog.close());
    document.querySelectorAll('[data-delete-address]').forEach(form => form.addEventListener('submit', event => {
        if (!confirm('Xóa địa chỉ nhận hàng này?')) event.preventDefault();
    }));
    const form = document.getElementById('password-form');
    if (form) {
        const check = () => form.elements.ConfirmPassword.setCustomValidity(
            form.elements.NewPassword.value === form.elements.ConfirmPassword.value ? '' : 'Xác nhận mật khẩu mới không khớp.');
        form.elements.NewPassword.addEventListener('input', check);
        form.elements.ConfirmPassword.addEventListener('input', check);
    }
})();
