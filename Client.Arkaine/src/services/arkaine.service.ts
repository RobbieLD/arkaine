
import B2File from '@/models/b2-file'
import BaseService from './base.service'
import ArkaineFile from '@/models/arkaine-file'
import Login from '@/models/login'
import Settings from '@/models/settings'
import Tag from '@/models/tag'

export default class ArkaineService extends BaseService {
    private baseUrl: string

    constructor() {
        super(process.env?.VUE_APP_ARKAINE_SERVER)
        this.baseUrl = process.env?.VUE_APP_ARKAINE_SERVER
    }

    public async Login(username: string, password: string, remember: boolean): Promise<void> {
        await this.http.post<string>('/login',{
            username,
            password,
            remember
        })
    }

    public async Logout(): Promise<void> {
        await this.http.get<void>('/logout')
    }

    public async TwoFactorAuth(username: string, code: string, remember: boolean): Promise<void> {
        await this.http.post<string>('/twofactorauth', {
            code,
            username,
            remember
        })
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

    public async LoggedIn(): Promise<Login> {
        const result = await this.http.get('/loggedin')
        return result.data
    }

    public async Start(): Promise<void> {
        await this.http.get('/settings/start')
    }

    public async Stop(): Promise<void> {
        await this.http.get('/settings/stop')
    }

    public async GetSettings(): Promise<Settings> {
        const result = await this.http.get('/settings')
        return result.data
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
