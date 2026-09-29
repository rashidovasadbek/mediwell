<script setup>
import { ref } from 'vue'
import client from '../api/client'
import { ElMessage } from 'element-plus'

const scanInput = ref('')
const done = ref([])       // muvaffaqiyatli prixod qilinganlar
const failed = ref([])     // xato bo'lganlar

async function onScan() {
    const code = scanInput.value.trim()
    scanInput.value = ''
    if (!code) return

    // format tekshiruvi
    const isAgg = code.length === 20 && code.startsWith('00')
    const isUnit = code.length === 31 && code.startsWith('01')
    if (!isAgg && !isUnit) {
        ElMessage.error(`Noto'g'ri format (${code.length} belgi)`)
        failed.value.unshift({ code, reason: 'Format xato' })
        return
    }

    try {
        const res = await client.post('/aggregates/mediwell-arrive', { code })
        done.value.unshift({ code, type: isAgg ? 'agregat' : 'ichki', message: res.data.message })
        ElMessage.success('Mediwellga prixod qilindi')
    } catch (e) {
        const reason = e.response?.data?.error || e.message
        failed.value.unshift({ code, reason })
        ElMessage.warning(reason)
    }
}

function clearAll() {
    done.value = []
    failed.value = []
}
</script>

<template>
    <div>
        <h1 class="title">Mediwellga prixod</h1>
        <p class="sub">Nalichidan Mediwellga o'tkazish — kodni skanerlang, naqddan rasmiyga o'tadi</p>

        <el-card class="box">
            <label class="lbl">Kod skanerlang (agregat yoki ichki)</label>
            <el-input v-model="scanInput" @keyup.enter="onScan" size="large"
                placeholder="Skanerni bu yerga qarating — har kod Enter bilan o'tadi" />

            <div class="counts">
                <el-tag type="success">Prixod qilindi: {{ done.length }}</el-tag>
                <el-tag type="danger" v-if="failed.length">Xato: {{ failed.length }}</el-tag>
                <el-button v-if="done.length || failed.length" size="small" text @click="clearAll">Tozalash</el-button>
            </div>
        </el-card>

        <el-card v-if="done.length" style="margin-top:16px">
            <div class="section-head" style="color:#67c23a">✓ Prixod qilindi ({{ done.length }})</div>
            <div v-for="(d, i) in done" :key="i" class="row ok">
                <span class="mono">{{ d.code }}</span>
                <el-tag size="small" :type="d.type === 'agregat' ? 'success' : 'warning'">{{ d.type }}</el-tag>
            </div>
        </el-card>

        <el-card v-if="failed.length" style="margin-top:16px">
            <div class="section-head" style="color:#f56c6c">✕ Xato ({{ failed.length }})</div>
            <div v-for="(f, i) in failed" :key="i" class="row err">
                <span class="mono">{{ f.code }}</span>
                <span class="reason">{{ f.reason }}</span>
            </div>
        </el-card>
    </div>
</template>

<style scoped>
.title { font-size: 24px; color: #1a2e30; margin-bottom: 4px; }
.sub { font-size: 14px; color: #5b6f71; margin-bottom: 20px; }
.box { max-width: 680px; }
.lbl { display:block; font-size:13px; color:#5b6f71; margin-bottom:6px; }
.counts { display:flex; align-items:center; gap:10px; margin-top:14px; }
.section-head { font-weight:600; margin-bottom:10px; }
.row { display:flex; align-items:center; gap:12px; padding:6px 8px; border-bottom:1px solid #f5f8f8; }
.row.err .reason { color:#f56c6c; font-size:13px; }
.mono { font-family:monospace; font-size:13px; flex:1; }
</style>