<script setup>
import { onMounted } from 'vue'
import { ElMessage } from 'element-plus'
import { groups, groupId, loadGroups } from '../sklad/useGroup'
import { errText } from '../sklad/skladApi'

onMounted(async () => {
    try {
        await loadGroups()
    } catch (e) {
        ElMessage.error('Guruhlarni yuklab bo\'lmadi: ' + errText(e))
    }
})
</script>

<template>
    <div class="sklad-hisobi">
        <div class="group-bar">
            <span class="group-label">Guruh:</span>
            <el-radio-group v-model="groupId" size="large">
                <el-radio-button v-for="g in groups" :key="g.id" :value="g.id">
                    {{ g.code }}
                </el-radio-button>
            </el-radio-group>
        </div>

        <router-view v-if="groupId" />
        <el-empty v-else description="Guruhlar yuklanmoqda..." />
    </div>
</template>

<style>
.sklad-hisobi .group-bar {
    display: flex;
    align-items: center;
    gap: 12px;
    margin-bottom: 20px;
    padding: 12px 16px;
    background: #fff;
    border-radius: 8px;
    box-shadow: 0 1px 3px rgba(0, 0, 0, .06);
}
.sklad-hisobi .group-label { font-weight: 600; color: #1a2e30; }

/* Sahifalar uchun umumiy */
.sklad-hisobi .title { font-size: 24px; color: #1a2e30; margin: 0 0 4px; }
.sklad-hisobi .sub { font-size: 14px; color: #5b6f71; margin: 0 0 20px; }
.sklad-hisobi .toolbar {
    display: flex;
    flex-wrap: wrap;
    align-items: center;
    gap: 12px;
    margin-bottom: 16px;
}
.sklad-hisobi .num { font-variant-numeric: tabular-nums; }
.sklad-hisobi .muted { color: #8a9a9c; font-size: 12px; }
.sklad-hisobi .neg { color: #d03050; font-weight: 600; }
</style>
