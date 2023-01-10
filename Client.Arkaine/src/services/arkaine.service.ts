
import Album from '@/models/album'
import ArkaineFile from '@/models/arkaine-file'
import B2File from '@/models/b2-file'
import Rating from '@/models/rating'
import BaseService from './base.service'

export default class ArkaineService extends BaseService {
    private baseUrl: string

    constructor() {
        super(process.env?.VUE_APP_ARKAINE_SERVER)
        this.baseUrl = process.env?.VUE_APP_ARKAINE_SERVER
    }

    public async Login(username: string, password: string): Promise<void> {
        await this.http.post<string>('/login',{
            username,
            password
        })
    }

    public async Logout(): Promise<void> {
        await this.http.get<void>('/logout')
    }

    public async TwoFactorAuth(username: string, code: string): Promise<string> {
        const result = await this.http.post<string>('/twofactorauth', {
            code,
            username
        })

        return result.data
    }

    public async Albums(): Promise<Album[]> {
        const results = await this.http.post<{ buckets: Album[] }>('/albums')
        return results.data.buckets
    }

    public async LoggedIn(): Promise<string> {
        const result = await this.http.get('/loggedin')
        return result.data
    }

    public async SaveRating(rating: Rating): Promise<void> {
        await this.http.put('/meta/rating', rating)
    }

    public async Files(bucketId: string, bucketName: string): Promise<ArkaineFile> {
        const results = await this.http.post<{ files: B2File[] }>('/files', {
            BucketId: bucketId
        })

        const root = new ArkaineFile('root', '', '', '')

        for (const file of results.data.files.filter(f => f.contentLength !== '0 B' && f.fileName !== 'thumb.jpg')) {
            root.add(file, `${this.baseUrl}/stream/${bucketName}/${file.fileName}`)
        }

        return root
    }
}
