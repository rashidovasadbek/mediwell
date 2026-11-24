<script setup>
// O'z parolini almashtirish — POST /auth/change-password
import { ref, watch } from 'vue'
import { ElMessage } from 'element-plus'
import client from '../api/client'
import { errText } from '../sklad/skladApi'

const MIN = 8

const visible = defineModel({ type: Boolean, default: false })

const empty = () => ({ current: '', next: '', confirm: '' })
const form = ref(empty())
const saving = ref(false)

// Har ochilganda tozalanadi
watch(visible, (v) => { if (v) form.value = empty() })

async function save() {
    const f = form.value
    if (!f.current) return ElMessage.warning('Joriy parolni kiriting')
    if (f.next.length < MIN) return ElMessage.warning(`Yangi parol kamida ${MIN} belgi bo'lishi kerak`)
    if (f.next !== f.confirm) return ElMessage.warning('Yangi parollar mos emas')
    if (f.next === f.current) return ElMessage.warning('Yangi parol joriy paroldan farq qilishi kerak')

    saving.value = true
    try {
        await client.post('/auth/change-password', { currentPassword: f.current, newPassword: f.next })
        ElMessage.success('Parol almashtirildi')
        visible.value = false
    } catch (e) {
        ElMessage.error(errText(e))
    } finally {
        saving.value = false
    }
}
</script>

<template>
    <el-dialog v-model="visible" title="Parolni almashtirish" width="420px" :close-on-click-modal="false"
               append-to-body>
        <el-form label-position="top" @submit.prevent="save">
            <el-form-item label="Joriy parol">
                <el-input v-model="form.current" type="password" show-password autocomplete="current-password" />
            </el-form-item>
            <el-form-item :label="`Yangi parol (kamida ${MIN} belgi)`">
                <el-input v-model="form.next" type="password" show-password autocomplete="new-password" />
            </el-form-item>
            <el-form-item label="Yangi parolni takrorlang">
                <el-input v-model="form.confirm" type="password" show-password autocomplete="new-password"
                          @keyup.enter="save" />
            </el-form-item>
        </el-form>
        <template #footer>
            <el-button @click="visible = false">Bekor qilish</el-button>
            <el-button type="primary" :loading="saving" @click="save">Saqlash</el-button>
        </template>
    </el-dialog>
</template>
