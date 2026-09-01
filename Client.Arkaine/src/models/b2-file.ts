import Tag from './tag'

export default interface B2File {
    fileName: string,
    contentType?: string,
    contentLength: string,
    action: string,
    fileId: string,
    preview: string,
    previewWidth?: number,
    previewHeight?: number,
    favourite: boolean,
    tags: Tag[]
}
