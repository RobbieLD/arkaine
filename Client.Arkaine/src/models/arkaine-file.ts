import B2File from './b2-file'

export default class ArkaineFile {

    name: string
    isDirectory: boolean
    isImage: boolean
    isVideo: boolean
    isAudio: boolean
    url: string
    size: string
    thumb: string
    contentType: string
    rawFileName: string
    id: string
    isFavourite: boolean
    preview: string

    constructor(file: B2File, baseUrl: string) {
        const pathParts = file.fileName.split('/').filter(f => f)
        this.name = pathParts[pathParts.length - 1]
        this.id = file.fileId
        this.isFavourite = pathParts.filter(p => p.toLocaleLowerCase() === 'fav').length > 0
        this.isDirectory = file.action === 'folder'
        this.rawFileName = file.fileName
        this.size = file.contentLength
        this.isImage = file.contentType?.startsWith('image') || false
        this.isAudio = file.contentType?.startsWith('audio') || false
        this.isVideo = file.contentType?.startsWith('video') || false
        this.url = `${baseUrl}/stream/${file.fileName}`
        this.contentType = file.contentType || ''
        // This only applies if this is a directory
        this.thumb = `${baseUrl}/stream/${file.fileName}thumb.jpg`
        // This only applies to images
        this.preview = `${baseUrl}/preview/${file.preview}`
    }
}
