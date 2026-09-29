<script setup>
import { ref, onMounted } from 'vue'
import client from '../api/client'

const status = ref('official')   // 'official' | 'cash' | ''
const agregat = ref([])
const ichki = ref([])

async function load() {
    const params = status.value ? { status: status.value } : {}
    const res = await client.get('/aggregates/codes', { params })
    agregat.value = res.data.agregat || []
    ichki.value = res.data.ichki || []
}

function setStatus(s) {
    status.value = s
    load()
}

onMounted(load)
</script>

<template>
    <div>
        <h1 class="title">Markirovka kodlar</h1>
        <p class="sub">Mediwellga prixod qilingan (rasmiy) va naqddagi kodlar alohida</p>

        <div class="tabs">
            <el-button :type="status === 'official' ? 'primary' : 'default'" @click="setStatus('official')">
                Rasmiy (Mediwell)
            </el-button>
            <el-button :type="status === 'cash' ? 'primary' : 'default'" @click="setStatus('cash')">
                Naqd (nalichi)
            </el-button>
            <el-button :type="status === '' ? 'primary' : 'default'" @click="setStatus('')">
                Hammasi
            </el-button>
        </div>

        <el-card style="margin-bottom: 16px">
            <div class="section-head">
                Agregat kodlar <el-tag type="success">{{ agregat.length }}</el-tag>
            </div>
            <el-table :data="agregat" size="small" style="width: 100%" max-height="320">
                <el-table-column prop="code" label="Kod" width="230">
                    <template #default="{ row }"><span class="mono">{{ row.code }}</span></template>
                </el-table-column>
                <el-table-column prop="productName" label="Preparat" />
                <el-table-column prop="series" label="Seriya" width="120" />
                <el-table-column prop="actualCount" label="Dona" width="90" />
                <el-table-column label="Holat" width="130">
                    <template #default="{ row }">
                        <el-tag :type="row.officialStatus === 'official' ? 'success' : 'info'" effect="plain">
                            {{ row.officialStatus === 'official' ? 'Rasmiy' : 'Naqd' }}
                        </el-tag>
                    </template>
                </el-table-column>
            </el-table>
        </el-card>

        <el-card>
            <div class="section-head">
                Ichki kodlar <el-tag type="warning">{{ ichki.length }}</el-tag>
            </div>
            <el-table :data="ichki" size="small" style="width: 100%" max-height="320">
                <el-table-column prop="code" label="Kod" width="320">
                    <template #default="{ row }"><span class="mono">{{ row.code }}</span></template>
                </el-table-column>
                <el-table-column prop="productName" label="Preparat" />
                <el-table-column prop="series" label="Seriya" width="120" />
                <el-table-column label="Holat" width="130">
                    <template #default="{ row }">
                        <el-tag :type="row.officialStatus === 'official' ? 'success' : 'info'" effect="plain">
                            {{ row.officialStatus === 'official' ? 'Rasmiy' : 'Naqd' }}
                        </el-tag>
                    </template>
                </el-table-column>
            </el-table>
        </el-card>
    </div>
</template>

<style scoped>
.title { font-size: 24px; color: #1a2e30; margin-bottom: 4px; }
.sub { font-size: 14px; color: #5b6f71; margin-bottom: 20px; }
.tabs { display: flex; gap: 8px; margin-bottom: 20px; }
.section-head { display: flex; align-items: center; gap: 10px; font-weight: 600; color: #028090; margin-bottom: 12px; }
.mono { font-family: monospace; font-size: 12px; }
</style>