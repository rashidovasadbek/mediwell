import axios from 'axios'
import { getToken } from './token'

const client = axios.create({
    // Backend manzili: .env faylida VITE_API_URL (namuna: .env.example)
    baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5048',
    headers: { 'Content-Type': 'application/json' }
})

// Har so'rovga JWT token
client.interceptors.request.use((config) => {
    const token = getToken()
    if (token) config.headers.Authorization = `Bearer ${token}`
    return config
})

// 401 (token eskirgan/yaroqsiz) — main.js da o'rnatiladigan handler login sahifasiga qaytaradi
let onUnauthorized = null
export function setUnauthorizedHandler(fn) {
    onUnauthorized = fn
}

client.interceptors.response.use(
    (res) => res,
    (error) => {
        const isLogin = error.config?.url?.includes('/auth/login')
        if (error.response?.status === 401 && !isLogin && onUnauthorized) onUnauthorized()
        return Promise.reject(error)
    }
)

export default client
