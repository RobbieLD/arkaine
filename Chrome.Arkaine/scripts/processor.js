const log = (message) => {
    const console = document.getElementsByClassName('console')[0]
    const entry = document.createElement('div')
    entry.style.padding = '0.5em'
    entry.innerHTML = message
    console.appendChild(entry)
}

const post = async (url, key, api, name) => {
    const response = await fetch(api, {
        method: 'POST',
        cache: 'no-cache',
        mode: 'cors',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            Url: url,
            Key: key,
            Name: name
        })
    })

    return response.status
}

const process = () => {
    log('Starting ...')
    chrome.tabs.query({ active: true, lastFocusedWindow: true }, tabs => {
        // Get the Tab url we're going to use
        let url = tabs[0].url;
        log(url)

        // Get the api key
        chrome.storage.sync.get({ key: '', url: '' }, (items) => {
            const input = document.getElementById('file-name')
            log(input.value)
            try {
                post(url, items.key, items.url, input.value).then((status) => {
                    log(status)
                })
            } catch (e) {
                log(e)
            }
        })
    })
}

// Hook up the event handler
document.getElementById('save').addEventListener('click', () => process())
