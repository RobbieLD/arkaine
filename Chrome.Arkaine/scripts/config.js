function save_options() {
    var key = document.getElementById('api-key').value;
    var url = document.getElementById('api-url').value;
    chrome.storage.sync.set({ key, url }, () => {
        // Update status to let user know options were saved.
        var status = document.getElementsByClassName('status')[0];
        status.textContent = 'Options saved.';
        setTimeout(() => {
            status.textContent = '';
        }, 750);
    });
}

function restore_options() {
    chrome.storage.sync.get({
        key: '',
        url: ''
    }, (items) => {
        document.getElementById('api-key').value = items.key;
        document.getElementById('api-url').value = items.url;
    });
}


document.addEventListener('DOMContentLoaded', restore_options);
document.getElementById('save').addEventListener('click', save_options);
