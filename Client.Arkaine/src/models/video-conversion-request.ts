export default interface VideoConversionRequest {
    id: number
    fileName: string
    fileId: string
    requestedBy: string
    status: string
    reason: string
    error: string
    requestedUtc: string
    updatedUtc: string
}
