import Album from '@/models/album'
import File from '@/models/file'

export default interface State {
    isAuthenticated: boolean,
    albums: Album[],
    files: File[]
}
