import axios, { AxiosError, AxiosInstance, AxiosResponse } from 'axios'

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
            // Special hook for redirection
            const url = new URL(error.request.responseURL)
            if (error.response?.status === 405 && url.search.includes('ReturnUrl')) {
                return Promise.reject(url.pathname)
            } else {
                return Promise.reject({ name: error.response?.status || error.name, message: error.response?.data || error.message })
            }
        })
    }

}
