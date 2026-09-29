<script setup>
import { ref, onMounted } from 'vue'
import client from '../api/client'

const stock = ref([])

async function load() {
    const res = await client.get('/stock/grouped')
    stock.value = res.data
}

function shortDate(d) {
    return (d || '').substring(0, 10)
}

function holatLabel(status) {
    if (status === 'arrived') return 'Prixod qilingan'
    if (status === 'queued') return 'Navbatda'
    if (status === 'unit') return 'Ochilgan dona'
    return status
}

function officialLabel(status) {
    return status === 'official' ? 'Rasmiy (Asl Belgi)' : 'Naqd'
}

onMounted(load)
</script>

<template>
    <div>
        <h1 class="title">Real-time qoldiq</h1>
        <p class="sub">Naqd — Asl Belgida yo'q. Rasmiy — Mediwellga prixod qilingan (Asl Belgida)</p>

        <el-button @click="load" style="margin-bottom: 16px">Yangilash</el-button>

        <el-table :data="stock" style="width: 100%" row-key="receiptId">
            <el-table-column type="expand">
                <template #default="{ row }">
                    <div class="detail">
                        <el-table :data="row.details" size="small" style="width: 100%">
                            <el-table-column label="Turi" width="170">
                                <template #default="{ row: d }">
                                    <el-tag :type="d.arrivalStatus === 'arrived' ? 'success' : d.arrivalStatus === 'unit' ? 'info' : 'warning'">
                                        {{ holatLabel(d.arrivalStatus) }}
                                    </el-tag>
                                </template>
                            </el-table-column>
                            <el-table-column label="Holat" width="170">
                                <template #default="{ row: d }">
                                    <el-tag :type="d.officialStatus === 'official' ? 'success' : 'info'" effect="plain">
                                        {{ officialLabel(d.officialStatus) }}
                                    </el-tag>
                                </template>
                            </el-table-column>
                            <el-table-column label="Karobka" width="120">
                                <template #default="{ row: d }">{{ d.boxCount || '—' }}</template>
                            </el-table-column>
                            <el-table-column label="Dona">
                                <template #default="{ row: d }">{{ d.unitCount }}</template>
                            </el-table-column>
                        </el-table>
                    </div>
                </template>
            </el-table-column>

            <el-table-column prop="productName" label="Preparat" />
            <el-table-column prop="series" label="Seriya" width="130" />
            <el-table-column label="Muddat" width="120">
                <template #default="{ row }">{{ shortDate(row.expiryDate) }}</template>
            </el-table-column>
            <el-table-column label="Naqd" width="110">
                <template #default="{ row }">
                    <span style="color:#909399">{{ row.cashUnits }}</span>
                </template>
            </el-table-column>
            <el-table-column label="Rasmiy" width="110">
                <template #default="{ row }">
                    <span style="color:#67c23a; font-weight:600">{{ row.officialUnits }}</span>
                </template>
            </el-table-column>
            <el-table-column label="Jami dona" width="120">
                <template #default="{ row }">
                    <b style="color:#028090">{{ row.totalUnits }}</b>
                </template>
            </el-table-column>
        </el-table>
    </div>
</template>

<style scoped>
.title { font-size: 24px; color: #1a2e30; margin-bottom: 4px; }
.sub { font-size: 14px; color: #5b6f71; margin-bottom: 20px; }
.detail { padding: 8px 16px; background: #f8fbfb; }
</style>