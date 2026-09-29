<script setup>
import { ref, onMounted } from 'vue'
import client from '../api/client'
import { ElMessage } from 'element-plus'

const products = ref([])
const form = ref({ name: '', manufacturer: '', nominalCapacity: 40 })

async function load() {
    const res = await client.get('/products')
    products.value = res.data
}

async function add() {
    if (!form.value.name) return ElMessage.warning('Nomi kiritilmagan')
    try {
        await client.post('/products', form.value)
        ElMessage.success('Preparat qo\'shildi')
        form.value.name = ''
        form.value.manufacturer = ''
        load()
    } catch (e) {
        ElMessage.error('Xato: ' + e.message)
    }
}

onMounted(load)
</script>

<template>
    <div>
        <h1 class="title">Preparatlar katalogi</h1>
        <p class="sub">Dorilar ro'yxati va yangi preparat qo'shish</p>

        <el-card class="box" style="margin-bottom: 20px">
            <el-form label-position="top">
                <el-form-item label="Nomi">
                    <el-input v-model="form.name" placeholder="Masalan: Analgin 500mg" />
                </el-form-item>
                <el-form-item label="Ishlab chiqaruvchi">
                    <el-input v-model="form.manufacturer" placeholder="Zavod nomi" />
                </el-form-item>
                <el-form-item label="Nominal sig'im">
                    <el-input-number v-model="form.nominalCapacity" :min="0" />
                </el-form-item>
                <el-button type="primary" @click="add">Qo'shish</el-button>
            </el-form>
        </el-card>

        <el-table :data="products" style="width: 100%" stripe>
            <el-table-column prop="id" label="ID" width="70" />
            <el-table-column prop="name" label="Nomi" />
            <el-table-column prop="manufacturer" label="Ishlab chiqaruvchi" />
            <el-table-column prop="nominalCapacity" label="Sig'im" width="100" />
        </el-table>
    </div>
</template>

<style scoped>
.title { font-size: 24px; color: #1a2e30; margin-bottom: 4px; }
.sub { font-size: 14px; color: #5b6f71; margin-bottom: 20px; }
.box { max-width: 640px; }
</style>