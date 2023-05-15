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
            'Content-Type': 'application/json'
        },
        body: JSON.stringify({
            Url: url,
            Key: key,
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

const connect = async (url) => {

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(url + "/progress")
        .configureLogging(signalR.LogLevel.Information)
        .build();

    async function start() {
        try {
            await connection.start();
        } catch (err) {
            log(err)
        }
    };
    
    connection.onclose(async () => {
        await start();
    });

    connection.on("update", (message) => {
        log(message)        
    })
    
    // Start the connection.
    await start();
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
            connect(items.url).then(() => {
                try {
                    post(url, items.key, items.url + "/ingest", input.value)
                        .then((response) => {
                            log(`${response.fileName} uploaded at ${response.contentLength}`)
                        })
                        .catch((err) => {
                            log('Upload failed: ' + err)
                        })
                } catch (e) {
                    log(e)
                }
            })            
        })
    })
}

// Hook up the event handler
document.getElementById('save').addEventListener('click', () => process())
