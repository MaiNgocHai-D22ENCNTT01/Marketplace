document.getElementById('saved-address')?.addEventListener('change', event => {
    const option = event.target.selectedOptions[0];
    const form = document.getElementById('delivery');
    form.elements.RecipientName.value = option.dataset.name || '';
    form.elements.Phone.value = option.dataset.phone || '';
    form.elements.DetailAddress.value = option.dataset.address || '';
});
