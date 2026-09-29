<script setup>
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { ElMessage } from 'element-plus'
import { login, homeFor } from '../auth'

const router = useRouter()
const route = useRoute()

const form = ref({ username: '', password: '' })
const loading = ref(false)

function submit() {
    if (!form.value.username) return ElMessage.warning('Login kiriting')
    if (!form.value.password) return ElMessage.warning('Parol kiriting')

    loading.value = true
    const res = login(form.value.username, form.value.password)
    loading.value = false

    if (!res.ok) {
        form.value.password = ''
        return ElMessage.error(res.error)
    }

    ElMessage.success(`Xush kelibsiz, ${res.user.fullName}`)
    const target = route.query.redirect || homeFor(res.user.role)
    router.replace(target)
}
</script>

<template>
    <div class="login-page">
        <div class="card">
            <div class="brand">
                MEDIWELL
                <small>Sklad boshqaruv tizimi</small>
            </div>

            <el-form label-position="top" @submit.prevent>
                <el-form-item label="Login">
                    <el-input v-model="form.username" size="large" placeholder="admin yoki sklad"
                        autocomplete="username" @keyup.enter="submit" />
                </el-form-item>

                <el-form-item label="Parol">
                    <el-input v-model="form.password" type="password" size="large" placeholder="Parol"
                        show-password autocomplete="current-password" @keyup.enter="submit" />
                </el-form-item>
            </el-form>

            <el-button type="primary" size="large" class="submit" :loading="loading" @click="submit">
                Kirish
            </el-button>

            <div class="hint">
                <div><b>Admin:</b> admin / admin123</div>
                <div><b>Skladchi:</b> sklad / sklad123</div>
            </div>
        </div>
    </div>
</template>

<style scoped>
.login-page {
    min-height: 100vh;
    display: flex;
    align-items: center;
    justify-content: center;
    background: linear-gradient(135deg, #01565e, #02897a);
    padding: 24px;
}
.card {
    width: 100%;
    max-width: 380px;
    background: #fff;
    border-radius: 14px;
    padding: 32px 28px 24px;
    box-shadow: 0 18px 50px rgba(0, 0, 0, .22);
}
.brand {
    text-align: center;
    font-size: 24px;
    font-weight: 700;
    letter-spacing: 2px;
    color: #01565e;
    margin-bottom: 24px;
}
.brand small {
    display: block;
    font-size: 12px;
    font-weight: 400;
    letter-spacing: 0;
    color: #6d8688;
    margin-top: 4px;
}
.submit { width: 100%; margin-top: 4px; }
.hint {
    margin-top: 20px;
    padding-top: 14px;
    border-top: 1px dashed #e2ecec;
    font-size: 12px;
    color: #7d9294;
    line-height: 1.7;
    text-align: center;
}
</style>
