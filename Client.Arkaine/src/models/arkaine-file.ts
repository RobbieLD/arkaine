import B2File from './b2-file'
import Rating from './rating'

export default class ArkaineFile implements B2File {
    
    fileName: string
    url: string
    contentType: string
    contentLength: string
    children: ArkaineFile[] = []
    isImage: boolean
    isVideo: boolean
    isFolder: boolean
    isAudio: boolean
    rating?: Rating

    public constructor(fn: string, ct: string, cl: string, u: string, r?: Rating) {
        this.fileName = fn
        this.contentType = ct
        this.contentLength = cl
        this.url = u
        this.rating = r

        this.isImage = ct.startsWith('image')
        this.isVideo = ct.startsWith('video')
        this.isAudio = ct.startsWith('audio')
        this.isFolder = ct === ''
    }

    public add(file: B2File, url: string) {
        const path = file.fileName.split('/')

        if (path.length > 1) {
            const subName = path.shift()
            file.fileName = path.join('/')
            for (const child of this.children) {
                if (child.fileName === subName) {
                    child.add(file, url)
                    return
                }
            }

            const newChild = new ArkaineFile(subName || '', '', '', '')
            this.children.push(newChild)
            newChild.add(file, url)
        }
        else {
            this.children.push(new ArkaineFile(file.fileName, file.contentType, file.contentLength, url, file.rating))
        }
    }
}
