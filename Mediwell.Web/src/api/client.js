import axios from 'axios'

const client = axios.create({
    baseURL: 'http://localhost:5048',   // backend manzili
    headers: { 'Content-Type': 'application/json' }
})

export default client