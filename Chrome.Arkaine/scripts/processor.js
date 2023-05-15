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

    return await response.json()
}

const connect = (url) => {

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(url + "/updates")
        .configureLogging(signalR.LogLevel.Information)
        .build();

    async function start() {
        try {
            await connection.start();
        } catch (err) {
            console.error(err);
        }
    };
    
    connection.onclose(async () => {
        await start();
    });

    connection.on("update", (_, message) => {
        log(message)        
    })
    
    // Start the connection.
    start();
}

const process = () => {
    log('Connecting to updates hub')
    chrome.tabs.query({ active: true, lastFocusedWindow: true }, tabs => {
        // Get the Tab url we're going to use
        let url = tabs[0].url;
        log("Url:" + url)

        // Get the api key
        chrome.storage.sync.get({ key: '', url: '' }, (items) => {
            const input = document.getElementById('file-name')
            log("FileName:" + input.value)
            connect(url)

            try {
                post(url, items.key, items.url, input.value).then((response) => {
                    log(`${response.fileName} uploaded at ${response.contentLength}`)
                })
            } catch (e) {
                log(e)
            }
        })
    })
}

// Hook up the event handler
document.getElementById('save').addEventListener('click', () => process())
