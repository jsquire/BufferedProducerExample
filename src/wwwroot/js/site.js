// Please see documentation at https://docs.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.


const sendTelemetry = (element, action, value) => {
    const options = {
        method: 'PUT',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ 'Element': element, 'Action': action, 'Value': value })
    };

    fetch('/api/telemetry', options)
        .then(response => {
            if (response.ok) {
                console.log('Telemetry reported.');
            }
            else {
                console.log('Telemetry failed to be reported.');
            }
        });
}

document.querySelectorAll('[data-telemetry]').forEach(item => {
    switch (item.getAttribute('data-telemetry')) {
        case 'click':
            item.addEventListener('click', () => sendTelemetry(item.getAttribute('id'), 'click', null));
            break;

        case 'change':
            item.addEventListener('change', () => sendTelemetry(item.getAttribute('id'), 'change', item.value));
            break;
    }
});


