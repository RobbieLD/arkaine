import Album from '@/models/album'
import Alert from '@/models/alert'
import ArkaineFile from '@/models/arkaine-file'

export default interface State {
    isAuthenticated: boolean,
    title: string,
    username: string,
    albums: Album[],
    filesRoot: ArkaineFile,
    path: string,
    alert?: Alert
}
