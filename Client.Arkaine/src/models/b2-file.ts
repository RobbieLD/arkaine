import Rating from './rating'

export default interface B2File {
    fileName: string,
    contentType: string,
    contentLength: string,
    rating?: Rating
}
