import Alert from '@/models/alert'
import ArkaineFile from '@/models/arkaine-file'

export default interface State {
    isAuthenticated: boolean,
    username: string,
    files: ArkaineFile[],
    alert?: Alert,
    nextFile: string
}
