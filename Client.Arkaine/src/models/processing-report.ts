export type ProcessingReportType = 'thumbnail' | 'conversion'

export default interface ProcessingReport {
    id: number
    type: ProcessingReportType
    name: string
    createdUtc: string
}
