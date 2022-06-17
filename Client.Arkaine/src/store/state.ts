import Album from '@/models/album'
import ArkaineFile from '@/models/arkaine-file'

export default interface State {
    isAuthenticated: boolean,
    albums: Album[],
    files: ArkaineFile[]
}
