import B2File from './b2-file'
import Tag from './tag'

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
    tags: Tag[]

    constructor(file: B2File, baseUrl: string) {
        const pathParts = file.fileName.split('/').filter(f => f)
        this.name = pathParts[pathParts.length - 1]
        this.id = file.fileId
        this.isFavourite = file.favourite
        this.isDirectory = file.action === 'folder'
        this.rawFileName = file.fileName
        this.size = file.contentLength
        this.tags = file.tags
        this.isImage = file.contentType?.startsWith('image') || false
        this.isAudio = file.contentType?.startsWith('audio') || false
        this.isVideo = file.contentType?.startsWith('video') || false
        this.url = `${baseUrl}/stream/${file.fileName}`
        this.contentType = file.contentType || ''
        // This only applies if this is a directory
        this.thumb = `${baseUrl}/stream/${file.fileName}thumb.jpg`
        // This only applies to images
        this.preview = file.preview ? `${baseUrl}/preview/${file.preview}` : ''
    }

    public static Favourite: ArkaineFile = {
        id:'',
        contentType: '',
        isAudio: false,
        isDirectory: true,
        isFavourite: false,
        isImage: false,
        isVideo: false,
        name: 'Favourites',
        preview: '',
        rawFileName: '',
        size: '',
        thumb: 'favourite.png',
        url: '',
        tags: []
    }
}
