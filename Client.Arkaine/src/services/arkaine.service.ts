
import B2File from '@/models/b2-file'
import BaseService from './base.service'
import ArkaineFile from '@/models/arkaine-file'
import Login from '@/models/login'
import Tag from '@/models/tag'
import ThumbnailCacheStats from '@/models/thumbnail-cache-stats'
import type ProcessingReport from '@/models/processing-report'
import type AdminStatusResponse from '@/models/admin-status'
import type VideoConversionRequest from '@/models/video-conversion-request'
import Profile, {
    Passkey,
    PasskeyAssertionPayload,
    PasskeyCreationOptions,
    PasskeyCredentialPayload,
    PasskeyRequestOptions,
    TwoFactorEnableResponse,
    TwoFactorSetup
} from '@/models/profile'
import { serverUrl } from '@/config'

type AdminStatusPayload = AdminStatusResponse | Record<string, unknown> | null

export default class ArkaineService extends BaseService {
    private baseUrl: string

    constructor() {
        super(serverUrl)
        this.baseUrl = serverUrl
    }

    public async Login(username: string, password: string, remember: boolean): Promise<boolean> {
        const results = await this.http.post<boolean>('/login',{
            username,
            password,
            remember
        })

        return results.data
    }

    public async GetPasskeyRequestOptions(username?: string): Promise<PasskeyRequestOptions> {
        const result = await this.http.post<PasskeyRequestOptions>('/passkeys/options', {
            username
        })
        return result.data
    }

    public async PasskeyLogin(
        credential: PasskeyAssertionPayload,
        remember: boolean
    ): Promise<void> {
        await this.http.post('/passkeys/login', {
            credential,
            remember
        })
    }

    public async Logout(): Promise<void> {
        await this.http.get<void>('/logout')
    }

    public async TwoFactorAuth(code: string, remember: boolean): Promise<void> {
        await this.http.post<string>('/twofactorauth', {
            code,
            remember
        })
    }

    public async GetProfile(): Promise<Profile> {
        const result = await this.http.get<Profile>('/profile')
        return result.data
    }

    public async GetTwoFactorSetup(): Promise<TwoFactorSetup> {
        const result = await this.http.get<TwoFactorSetup>('/profile/2fa/setup')
        return result.data
    }

    public async EnableTwoFactor(code: string): Promise<TwoFactorEnableResponse> {
        const result = await this.http.post<TwoFactorEnableResponse>('/profile/2fa/enable', { code })
        return result.data
    }

    public async DisableTwoFactor(code: string): Promise<void> {
        await this.http.post('/profile/2fa/disable', { code })
    }

    public async GetPasskeyCreationOptions(): Promise<PasskeyCreationOptions> {
        const result = await this.http.post<PasskeyCreationOptions>('/profile/passkeys/options')
        return result.data
    }

    public async RegisterPasskey(
        credential: PasskeyCredentialPayload,
        name?: string
    ): Promise<Passkey> {
        const result = await this.http.post<Passkey>('/profile/passkeys', {
            credential,
            name
        })
        return result.data
    }

    public async RemovePasskey(id: string): Promise<void> {
        await this.http.delete(`/profile/passkeys/${encodeURIComponent(id)}`)
    }

    public async DeleteTag(id: number) : Promise<Tag[]> {
        const results = await this.http.delete<Tag[]>(`/tags/delete/${id}`)
        return results.data
    }

    public async AddTag(name: string, file: string, time: number): Promise<Tag[]> {
        const results = await this.http.post<Tag[]>('/tags/add', {
            name: name,
            fileName: file,
            timeStamp: time
        })

        return results.data
    }

    public async AddToFavourites(file: ArkaineFile): Promise<void> {
        await this.http.put('/favourite', {
            fileName: file.rawFileName
        })
    }

    public async RemoveFromFavourites(file: ArkaineFile): Promise<void> {
        await this.http.delete('/favourite', {
            data: {
                fileName: file.rawFileName
            }
        })
    }

    public async LoggedIn(): Promise<Login> {
        const result = await this.http.get('/loggedin')
        return result.data
    }

    public async StartThumbnails(): Promise<AdminStatusPayload> {
        const result = await this.http.post<AdminStatusPayload>('/admin/thumbnails/start')
        return result.data
    }

    public async StopThumbnails(): Promise<AdminStatusPayload> {
        const result = await this.http.post<AdminStatusPayload>('/admin/thumbnails/stop')
        return result.data
    }

    public async StartConversion(path: string): Promise<AdminStatusPayload> {
        const result = await this.http.post<AdminStatusPayload>('/admin/convert/start', {
            path,
        })
        return result.data
    }

    public async StopConversion(): Promise<AdminStatusPayload> {
        const result = await this.http.post<AdminStatusPayload>('/admin/convert/stop')
        return result.data
    }

    public async GetAdminStatus(): Promise<AdminStatusPayload> {
        const result = await this.http.get<AdminStatusPayload>('/admin')
        return result.data
    }

    public async GetThumbnailCacheStats(): Promise<ThumbnailCacheStats> {
        const result = await this.http.get<ThumbnailCacheStats>('/admin/thumbnail-cache')
        return result.data
    }

    public async ClearThumbnailCache(): Promise<ThumbnailCacheStats> {
        const result = await this.http.post<ThumbnailCacheStats>('/admin/thumbnail-cache/clear')
        return result.data
    }

    public async GetConversionPaths(): Promise<string[]> {
        const result = await this.http.get<string[]>('/admin/conversion/paths')
        return result.data
    }

    public async QueueVideoConversion(fileName: string, fileId: string): Promise<VideoConversionRequest> {
        const result = await this.http.post<VideoConversionRequest>('/admin/conversion/requests', {
            fileName,
            fileId
        })
        return result.data
    }

    public async GetVideoConversionRequests(status?: string): Promise<VideoConversionRequest[]> {
        const query = status ? `?status=${encodeURIComponent(status)}` : ''
        const result = await this.http.get<VideoConversionRequest[]>(`/admin/conversion/requests${query}`)
        return result.data
    }

    public async CancelVideoConversion(id: number): Promise<void> {
        await this.http.delete(`/admin/conversion/requests/${id}`)
    }

    public async GetProcessingReports(): Promise<ProcessingReport[]> {
        const result = await this.http.get<ProcessingReport[]>('/admin/reports')
        return result.data
    }

    public async DownloadProcessingReport(id: number): Promise<Blob> {
        const result = await this.http.get<Blob>(`/admin/reports/${id}`, {
            responseType: 'blob'
        })
        return result.data
    }

    public async ClearProcessingReports(): Promise<void> {
        await this.http.post('/admin/reports/clear')
    }

    public async Files(path: string, nextFile: string): Promise<{ files: ArkaineFile[], nextFile: string }> {
        const results = await this.http.post<{ files: B2File[], nextFileName:string }>('/files', {
            prefix: path,
            delimiter: '/',
            startFileName: nextFile
        })

        const files = results.data.files
            .filter(f => !f.fileName.endsWith('thumb.jpg') && !f.fileName.endsWith('.bzEmpty'))
            .map(f => new ArkaineFile(f, this.baseUrl))

        return {
            files,
            nextFile: results.data.nextFileName
        }
    }
}
