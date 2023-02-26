import Alert from '@/models/alert'
import ArkaineFile from '@/models/arkaine-file'
import Progress from '@/models/progress'
import Settings from '@/models/settings'

export default interface State {
    isAuthenticated: boolean,
    isAdmin: boolean,
    username: string,
    files: ArkaineFile[],
    alert?: Alert,
    nextFile: string,
    settings: Settings,
    progress: Progress
}
