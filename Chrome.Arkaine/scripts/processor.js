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
        headers: {
            'Content-Type': 'application/json',
            'X-Arkaine-Api-Key': key
        },
        body: JSON.stringify({
            Url: url,
            Name: name
        })
    })

    if (response.ok) {
        return await response.json()
    }
    else {
        throw await response.text()
    }    
}

const process = () => {
    log('Starting Processing')
    chrome.tabs.query({ active: true, lastFocusedWindow: true }, tabs => {
        // Get the Tab url we're going to use
        let url = tabs[0].url;
        log("Url: " + url)

        // Get the api key
        chrome.storage.sync.get({ key: '', url: '' }, (items) => {
            const input = document.getElementById('file-name')
            log("FileName: " + input.value)
            try {
                post(url, items.key, items.url + "/ingest", input.value)
                    .then((response) => {
                        log(`Upload: ${response}`)
                    })
                    .catch((err) => {
                        log('Upload failed: ' + err)
                    })
            } catch (e) {
                log(e)
            }
        })
    })
}

// Hook up the event handler
document.getElementById('save').addEventListener('click', () => process())
