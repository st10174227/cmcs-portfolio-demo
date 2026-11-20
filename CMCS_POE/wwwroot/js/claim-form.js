document.addEventListener('DOMContentLoaded', () => {
    const hours = document.getElementById('claim-hours');
    const rate = document.getElementById('claim-rate');
    const total = document.getElementById('claim-total');
    if (!hours || !rate || !total) return;
    const money = new Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' });
    const update = () => {
        const amount = Number(hours.value) * Number(rate.dataset.rate);
        total.textContent = Number.isFinite(amount) ? money.format(amount) : 'Enter valid hours';
    };
    hours.addEventListener('input', update);
    update();
});
