<template>
  <div class="compare-container">
    <h2>Compare Tables</h2>

    <div class="controls">
      <label>
        Database Source
        <select v-model="databaseSource">
            <option value="PrimaryConnection">TrixCompareDbTest</option>
            <option value="SecondaryConnection">TrixCompareDbProd</option>
        </select>
      </label>

      <label>
        Database Target
          <select v-model="databaseTarget">
              <option value="PrimaryConnection">TrixCompareDbTest</option>
              <option value="SecondaryConnection">TrixCompareDbProd</option>o

          </select>
      </label>

      <label>
        Table Name
        <select v-model="tableName">
          <option>Products</option>
        </select>
      </label>

      <button @click="compare" :disabled="loading">Confronta</button>
      <button @click="performUpdate" :disabled="loading || rows.length === 0" class="update-btn">Update Target</button>
    </div>

    <div class="status-line">
      <span v-if="loading">Loading...</span>
      <span v-if="error" class="error">Error: {{ error }}</span>
    </div>

    <div v-if="rows.length" class="results">
      <div v-for="(row, idx) in rows" :key="idx" class="pair-block">
        <div class="pair-header">
          <div class="label">Source</div>
          <div class="label">Target</div>
          <div class="status">{{ row.status || row.Status }}</div>
        </div>

        <div class="pair">
          <div class="table-scroll-wrapper">
            <table class="side-table source-table">
              <tbody>
                <tr v-for="key in getKeys(row)" :key="key" :class="getRowClass(row)">
                  <td class="col-key">{{ key }}</td>
                  <td class="col-val"><pre class="cell-pre">{{ formatValue(getSourceValue(row, key)) }}</pre></td>
                </tr>
              </tbody>
            </table>
          </div>

          <div class="table-scroll-wrapper">
            <table class="side-table target-table">
              <tbody>
                <tr v-for="key in getKeys(row)" :key="key" :class="getRowClass(row)">
                  <td class="col-key">{{ key }}</td>
                  <td class="col-val"><pre class="cell-pre">{{ formatValue(getTargetValue(row, key)) }}</pre></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
      </div>
    </div>

    <div v-else-if="!loading" class="no-results">Nessun risultato.</div>
  </div>
</template>

<script setup>
import { ref } from 'vue'

const databaseSource = ref('PrimaryConnection')
const databaseTarget = ref('SecondaryConnection')
const tableName = ref('Products')
const loading = ref(false)
const error = ref(null)
const rows = ref([])

// Vite env base (leave empty to use relative /api which will hit proxy or same host)
const API_BASE = import.meta.env.VITE_API_BASE_URL || ''

function apiUrl(path) {
  if (!API_BASE) return path
  return API_BASE.replace(/\/$/, '') + path
}

async function compare() {
  loading.value = true
  error.value = null
  rows.value = []
  try {
    const url = apiUrl('/api/compare')
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        databaseSource: databaseSource.value,
        databaseTarget: databaseTarget.value,
        tableName: tableName.value
      })
    })
    if (!res.ok) {
      const text = await res.text()
      throw new Error(`${res.status} ${res.statusText} ${text}`)
    }
    const data = await res.json()
    rows.value = Array.isArray(data) ? data : []
  } catch (e) {
    error.value = e?.message || String(e)
  } finally {
    loading.value = false
  }
}

async function performUpdate() {
  if (!confirm('This will overwrite the target table to match the source table. This is a destructive action. Continue?')) {
    return
  }

  loading.value = true
  error.value = null
  try {
    const url = apiUrl('/api/compare/update')
    const res = await fetch(url, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        databaseSource: databaseSource.value,
        databaseTarget: databaseTarget.value,
        tableName: tableName.value
      })
    })
    if (!res.ok) {
      const text = await res.text()
      throw new Error(`${res.status} ${res.statusText} ${text}`)
    }
    const data = await res.json()
    error.value = null
    // Re-run comparison to refresh the view
    await compare()
  } catch (e) {
    error.value = e?.message || String(e)
    loading.value = false
  }
}

function getRowClass(row) {
  const st = (row.status || row.Status || '').toString()
  if (st === 'Equal') return 'green'
  if (st === 'Different') return 'red'
  if (st === 'MissingSource' || st === 'MissingTarget') return 'yellow'
  return ''
}

function pretty(obj) {
  try {
    return JSON.stringify(obj, null, 2)
  } catch {
    return String(obj)
  }
}

function getSource(row) {
  return row.source || row.Source || {}
}

function getTarget(row) {
  return row.target || row.Target || {}
}

function getKeys(row) {
  const s = getSource(row)
  const t = getTarget(row)
  const keys = new Set()
  Object.keys(s || {}).forEach(k => keys.add(k))
  Object.keys(t || {}).forEach(k => keys.add(k))
  return Array.from(keys)
}

function getSourceValue(row, key) {
  const s = getSource(row)
  return s && Object.prototype.hasOwnProperty.call(s, key) ? s[key] : null
}

function getTargetValue(row, key) {
  const t = getTarget(row)
  return t && Object.prototype.hasOwnProperty.call(t, key) ? t[key] : null
}

function formatValue(v) {
  if (v === null || v === undefined) return ''
  try {
    if (typeof v === 'object') return JSON.stringify(v)
  } catch {}
  return String(v)
}
</script>

<style scoped>
.compare-container {
  max-width: 980px;
  margin: 20px auto;
  font-family: Arial, Helvetica, sans-serif;
}

.controls {
  display: flex;
  gap: 12px;
  align-items: center;
  margin-bottom: 12px;
  flex-wrap: wrap;
}

.controls label {
  display: flex;
  flex-direction: column;
  font-size: 14px;
}

.controls select {
  margin-top: 6px;
  padding: 6px;
}

.controls button {
  padding: 8px 12px;
  cursor: pointer;
}

.controls button.update-btn {
  background-color: #ff9800;
  color: white;
  border: none;
  border-radius: 4px;
  cursor: pointer;
  padding: 8px 12px;
  font-weight: 600;
}

.controls button.update-btn:hover:not(:disabled) {
  background-color: #e68900;
}

.controls button.update-btn:disabled {
  background-color: #ccc;
  cursor: not-allowed;
}

.status-line {
  margin-bottom: 12px;
}

.error {
  color: crimson;
}

.compare-table {
  width: 100%;
  border-collapse: collapse;
  table-layout: fixed;
}

.compare-table th,
.compare-table td {
  border: 1px solid #ddd;
  padding: 8px;
  vertical-align: top;
  text-align: left;
}

.cell-pre {
  white-space: pre-wrap;
  word-break: break-word;
  font-family: monospace;
  margin: 0;
}

/* color rules */
.green {
  background-color: #c8f7c5;
}

.red {
  background-color: #f7c5c5;
}

.yellow {
  background-color: #fff3b0;
}

.status-cell {
  width: 110px;
  text-align: center;
  font-weight: 600;
}

.no-results {
  color: #666;
  margin-top: 12px;
}

/* Pair layout */
.pair-block {
  margin-bottom: 18px;
  border: 1px solid #eee;
  padding: 8px;
  border-radius: 6px;
  background: #fff;
}
.pair-header {
  display: flex;
  gap: 12px;
  align-items: center;
  margin-bottom: 8px;
}
.pair-header .label {
  flex: 1 1 0;
  font-weight: 700;
}
.pair-header .status {
  width: 120px;
  text-align: center;
  font-weight: 700;
}
.pair {
  display: flex;
  gap: 12px;
}
.table-scroll-wrapper {
  flex: 1;
  overflow-x: auto;
  max-width: 100%;
}
.side-table {
  width: 50%;
  border-collapse: collapse;
}
.side-table td {
  border: 1px solid #eee;
  padding: 6px;
}
.col-key { width: 30%; font-weight: 600; background:#fafafa }
.col-val { width: 70% }
</style>
