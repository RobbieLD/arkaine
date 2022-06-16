import axios, { AxiosError, AxiosInstance } from 'axios'

export default abstract class BaseService {
    public http: AxiosInstance

    constructor(baseUrl: string) {
        this.http = axios.create({
            baseURL: baseUrl,
            headers: {
                'Content-type': 'application/json; charset=UTF-8',
            },
            withCredentials: true
        })

        // Hook up the error handler
        this.http.interceptors.response.use(undefined, (error: AxiosError) => {
            return Promise.reject({ name: error.response?.status || error.name, message: error.response?.data || error.message })
        })
    }

}
