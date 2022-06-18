
import Album from '@/models/album'
import ArkaineFile from '@/models/arkaine-file'
import B2File from '@/models/b2-file'
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

    public async Albums(): Promise<Album[]> {
        const results = await this.http.post<{ buckets: Album[] }>('/albums')
        return results.data.buckets
    }

    public async LoggedIn(): Promise<void> {
        return await this.http.get('/loggedin')
    }

    public async Files(bucketId: string, bucketName: string): Promise<ArkaineFile> {
        const results = await this.http.post<{ files: B2File[] }>('/files', {
            BucketId: bucketId
        })

        const root = new ArkaineFile('root', '', '', '')

        for (const file of results.data.files.filter(f => f.contentLength !== '0 B')) {
            root.add(file, `${this.baseUrl}/stream/${bucketName}/${file.fileName}`)
        }

        return root
    }
}
