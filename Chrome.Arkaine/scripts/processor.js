const log = (message) => {
    const console = document.getElementsByClassName('console')[0]
    const entry = document.createElement('div')
    entry.innerHTML = message
    console.appendChild(entry)
}

log('Starting...')

chrome.tabs.query({active: true, lastFocusedWindow: true}, tabs => {
    let url = tabs[0].url;
    log(url)
});