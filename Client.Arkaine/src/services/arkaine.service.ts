
import Album from '@/models/album'
import File from '@/models/file'
import BaseService from './base.service'

export default class ArkaineService extends BaseService {
    constructor() {
        super(process.env?.VUE_APP_ARKAINE_SERVER)
    }

    public async Login(username: string, password: string): Promise<void> {
        await this.http.post<string>('/login',{
            username,
            password
        })
    }

    public async Albums(accountId: string): Promise<Album[]> {
        const results = await this.http.post<{ buckets: Album[] }>('/albums', {
            AccountId: accountId
        })

        return results.data.buckets
    }

    public async Files(bucketId: string): Promise<File[]> {
        const results = await this.http.post<{ files: File[] }>('/files', {
            BucketId: bucketId
        })

        return results.data.files
    }
}
