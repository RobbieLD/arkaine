const log = (message) => {
    const console = document.getElementsByClassName('console')[0]
    const entry = document.createElement('div')
    entry.innerHTML = message
    console.appendChild(entry)
}

const process = (url, key, api) => {
    console.log(url, key, api)
}

log('Starting...')

chrome.tabs.query({active: true, lastFocusedWindow: true}, tabs => {
    // Get the Tab url we're going to use
    let url = tabs[0].url;
    log(url)

    // Get the api key
    chrome.storage.sync.get({ key: '', url: '' }, (items) => {
        process(url, items.key, items.url)
    })
})
