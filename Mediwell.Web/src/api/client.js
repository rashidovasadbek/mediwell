import axios from 'axios'

const client = axios.create({
    // Backend manzili: .env faylida VITE_API_URL (namuna: .env.example)
    baseURL: import.meta.env.VITE_API_URL || 'http://localhost:5048',
    headers: { 'Content-Type': 'application/json' }
})

export default client
