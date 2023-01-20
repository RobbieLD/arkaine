
import B2File from '@/models/b2-file'
import BaseService from './base.service'
import ArkaineFile from '@/models/arkaine-file'

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

    public async TwoFactorAuth(username: string, code: string): Promise<void> {
        await this.http.post<string>('/twofactorauth', {
            code,
            username
        })
    }

    public async LoggedIn(): Promise<string> {
        const result = await this.http.get('/loggedin')
        return result.data
    }
    
    public async Files(path: string, nextFile: string): Promise<{ files: ArkaineFile[], nextFile: string }> {
        const results = await this.http.post<{ files: B2File[], nextFileName:string }>('/files', {
            prefix: path,
            delimiter: '/',
            startFileName: nextFile
        })

        return {
            files: results.data.files
            .filter(f => !f.fileName.endsWith('thumb.jpg') && !f.fileName.endsWith('.bzEmpty'))
            .map(f => new ArkaineFile(f, this.baseUrl)),
            nextFile: results.data.nextFileName
        }
    }
}
