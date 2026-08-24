document.addEventListener('DOMContentLoaded', () => {
    document.querySelectorAll('.btn-toggle-password').forEach(btn => {
        btn.addEventListener('click', () => {
            const group = btn.closest('.input-group');
            const input = group?.querySelector('input');
            if (!input) return;
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            btn.setAttribute('aria-label', show ? 'An mat khau' : 'Hien mat khau');
            btn.querySelector('.toggle-label').textContent = show ? 'An' : 'Hien';
        });
    });

    const pwdInput = document.getElementById('register-password');
    if (pwdInput) {
        const rules = {
            length: document.getElementById('rule-length'),
            upper: document.getElementById('rule-upper'),
            special: document.getElementById('rule-special')
        };

        pwdInput.addEventListener('input', () => {
            const v = pwdInput.value;
            toggleRule(rules.length, v.length >= 8);
            toggleRule(rules.upper, /[A-Z]/.test(v));
            toggleRule(rules.special, /[^a-zA-Z0-9]/.test(v));
        });
    }
});

function toggleRule(el, valid) {
    if (!el) return;
    el.classList.toggle('valid', valid);
}
