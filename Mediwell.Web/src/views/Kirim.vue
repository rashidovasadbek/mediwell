<script setup>
import { ref, onMounted } from 'vue'
import client from '../api/client'
import { ElMessage } from 'element-plus'

const products = ref([])
const form = ref({
    productId: null,
    series: '',
    expiryDate: '',
    supplier: ''
})

const scanInput = ref('')          // skaner kiritadigan qator
const codes = ref([])              // to'plangan kodlar: { code, type }

async function loadProducts() {
    const res = await client.get('/products')
    products.value = res.data
}

// Skaner kod kiritganda (Enter bosilganda)
function onScan() {
    const code = scanInput.value.trim()
    scanInput.value = ''   // maydonni darhol tozalaymiz (keyingi skan uchun tayyor)

    if (!code) return

    // 1. Format: agregat (20, "00") yoki ichki (31, "01")?
    let type
    if (code.length === 20 && code.startsWith('00')) type = 'agregat'
    else if (code.length === 31 && code.startsWith('01')) type = 'ichki'
    else {
        ElMessage.error(`Noto'g'ri kod formati (${code.length} belgi): ${code.substring(0, 15)}...`)
        return
    }

    // 2. Ro'yxatda takror bormi? (darhol ushlash)
    if (codes.value.some(c => c.code === code)) {
        ElMessage.warning(`Bu kod allaqachon ro'yxatda: ${code.substring(0, 15)}...`)
        return   // qo'shmaymiz
    }

    // 3. Toza — ro'yxatga qo'shamiz
    codes.value.push({ code, type })
}

function removeCode(index) {
    codes.value.splice(index, 1)
}

async function submit() {
    if (!form.value.productId) return ElMessage.warning('Preparat tanlanmagan')
    if (!form.value.series) return ElMessage.warning('Seriya kiritilmagan')
    if (!form.value.expiryDate) return ElMessage.warning('Muddat kiritilmagan')
    if (codes.value.length === 0) return ElMessage.warning('Kod kiritilmagan')

    // agregat va ichki kodlarni ajratamiz
    const agregatCodes = codes.value.filter(c => c.type === 'agregat')
        .map(c => ({ code: c.code, arrivalStatus: 'queued' }))
    const unitCodes = codes.value.filter(c => c.type === 'ichki').map(c => c.code)

    try {
        const body = {
            productId: form.value.productId,
            series: form.value.series,
            expiryDate: form.value.expiryDate + 'T00:00:00',
            supplier: form.value.supplier,
            entryMethod: 'scan',
            note: '',
            codes: agregatCodes,
            unitCodes: unitCodes
        }
        const res = await client.post('/receipts', body)
        ElMessage.success(`Kirim qilindi! ID: ${res.data.receiptId}, agregat: ${agregatCodes.length}, ichki: ${unitCodes.length}`)
        form.value.series = ''
        codes.value = []
    } catch (e) {
        // Backend takror kod topsa (409)
        if (e.response?.status === 409 && e.response.data?.duplicates) {
            const dups = e.response.data.duplicates
            ElMessage.error(`Bazada allaqachon bor: ${dups.length} ta kod. Tekshiring.`)
            // takror kodlarni ro'yxatda belgilaymiz (qizil)
            codes.value.forEach(c => {
                if (dups.includes(c.code)) c.duplicate = true
            })
        } else {
            ElMessage.error('Xato: ' + (e.response?.data?.error || e.message))
        }
    }
}

// hisoblar
function agregatCount() { return codes.value.filter(c => c.type === 'agregat').length }
function ichkiCount() { return codes.value.filter(c => c.type === 'ichki').length }

onMounted(loadProducts)
</script>

<template>
    <div>
        <h1 class="title">Yangi kirim (partiya)</h1>
        <p class="sub">Zavoddan kelgan agregat va ichki kodlarni skanerlang</p>

        <el-card class="box">
            <el-form label-position="top">
                <el-form-item label="Preparat">
                    <el-select v-model="form.productId" placeholder="Tanlang" style="width: 100%">
                        <el-option v-for="p in products" :key="p.id"
                            :label="`${p.name} (sig'im: ${p.nominalCapacity})`" :value="p.id" />
                    </el-select>
                </el-form-item>

                <el-form-item label="Seriya raqami">
                    <el-input v-model="form.series" placeholder="Masalan: SER-2026-001" />
                </el-form-item>

                <el-form-item label="Amal qilish muddati">
                    <el-date-picker v-model="form.expiryDate" type="date" value-format="YYYY-MM-DD" placeholder="Sana" style="width: 100%" />
                </el-form-item>

                <el-form-item label="Yetkazib beruvchi">
                    <el-input v-model="form.supplier" placeholder="Zavod / firma" />
                </el-form-item>

                <el-form-item label="Kod skanerlang (agregat yoki ichki)">
                    <el-input v-model="scanInput" @keyup.enter="onScan"
                        placeholder="Skanerni bu yerga qarating — har kod Enter bilan qo'shiladi"
                        size="large" />
                </el-form-item>
            </el-form>

            <div class="counts">
                <el-tag type="success">Agregat: {{ agregatCount() }}</el-tag>
                <el-tag type="warning">Ichki: {{ ichkiCount() }}</el-tag>
                <el-tag type="info">Jami: {{ codes.length }}</el-tag>
            </div>

            <div class="code-list">
                <div v-for="(c, i) in codes" :key="c.code" class="code-row" :class="{ dup: c.duplicate }">
                    <span class="code-type" :class="c.type">{{ c.type === 'agregat' ? 'A' : 'I' }}</span>
                    <span class="code-text">{{ c.code }}</span>
                    <el-button size="small" text @click="removeCode(i)">✕</el-button>
                </div>
                <div v-if="codes.length === 0" class="empty">Hali kod skanerlanmagan</div>
            </div>

            <el-button type="primary" size="large" @click="submit" style="margin-top: 16px">Kirim qilish</el-button>
        </el-card>
    </div>
</template>

<style scoped>
.title { font-size: 24px; color: #1a2e30; margin-bottom: 4px; }
.sub { font-size: 14px; color: #5b6f71; margin-bottom: 20px; }
.box { max-width: 680px; }
.counts { display: flex; gap: 8px; margin: 12px 0; }
.code-list { max-height: 300px; overflow-y: auto; border: 1px solid #eef3f3; border-radius: 8px; padding: 6px; }
.code-row { display: flex; align-items: center; gap: 10px; padding: 6px 8px; border-bottom: 1px solid #f5f8f8; }
.code-row.dup { background: #fef0f0; }
.code-type { display: inline-flex; align-items: center; justify-content: center; width: 22px; height: 22px; border-radius: 6px; font-size: 12px; font-weight: 700; color: #fff; }
.code-type.agregat { background: #02a37a; }
.code-type.ichki { background: #e6a23c; }
.code-text { flex: 1; font-family: monospace; font-size: 13px; color: #364a4c; }
.empty { color: #9bb0b1; font-size: 14px; padding: 16px; text-align: center; }
</style>