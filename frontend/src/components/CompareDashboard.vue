<template>
  <div class="page">
    <div class="container">
      <div class="card top-card">
        <div class="controls">
          <div class="field">
            <label>Database Source</label>
            <select v-model="databaseSource" @change="onDatabaseChange">
              <option value="" disabled selected>Select database</option>
              <option v-for="db in databases" :key="db" :value="db">{{ db }}</option>
            </select>
          </div>

          <div class="field">
            <label>Database Target</label>
            <select v-model="databaseTarget" @change="onDatabaseChange">
              <option value="" disabled selected>Select database</option>
              <option v-for="db in databases" :key="db" :value="db">{{ db }}</option>
            </select>
          </div>

          <div class="field wide">
            <label>Table</label>
            <select v-model="tableName" :disabled="tablesLoading || tableOptions.length === 0">
              <option value="" disabled v-if="!tablesLoading && tableOptions.length">Select table</option>
              <option v-if="tablesLoading" disabled>Loading tables...</option>
              <option v-for="t in tableOptions" :key="t" :value="t">{{ t }}</option>
            </select>
            <div class="hint" v-if="tableOptions.length === 0 && !tablesLoading && (databaseSource || databaseTarget)">
              Nessuna tabella disponibile per la selezione.
            </div>
          </div>

          <div class="actions">
            <button class="primary" @click="compare" :disabled="compareDisabled">
              <span v-if="loading" class="spinner" aria-hidden="true"></span>
              <span v-if="!loading">Confronta</span>
              <span v-else>Caricamento...</span>
            </button>
            <button class="update-btn" @click="performUpdate" :disabled="loading || rows.length === 0">
              <span v-if="loading" class="spinner" aria-hidden="true"></span>
              <span v-if="!loading">Update Target</span>
              <span v-else>Caricamento...</span>
            </button>
          </div>
        </div>


      </div>

      <div class="card results-card">
        <div class="results-header">
          <h3>Risultati</h3>
          <div v-if="error" class="error">Errore: {{ error }}</div>
          <div v-if="!rows.length && !loading && !error" class="muted">Nessun risultato</div>
        </div>

        <div class="results-body">
          <div v-if="loading" class="center">
            <div class="spinner large" aria-hidden="true"></div>
          </div>

          <div v-else-if="rows.length">
           

            <div class="tables-wrapper">
              <!-- left column: source table -->
              <div class="table-column">
                <div class="sticky-header">Source</div>
                <div class="scroll-area">
                  <table class="data-table">
                    <thead>
                      <tr>
                        <th v-for="col in columns" :key="col">{{ col }}</th>
                      </tr>
                    </thead>
                    <tbody>
                      <tr v-for="(row, ridx) in rows" :key="ridx" :class="rowClass(row)">
                        <td v-for="col in columns" :key="col + '-s-' + ridx" :class="cellClass(row, col, 'source')">{{ formatValue(getSourceValue(row, col)) }}</td>
                      </tr>
                    </tbody>
                  </table>
                </div>
              </div>

              <!-- right column: target table -->
              <div class="table-column">
                <div class="sticky-header">Target</div>
                <div class="scroll-area">
                  <table class="data-table">
                    <thead>
                      <tr>
                        <th v-for="col in columns" :key="col">{{ col }}</th>
                      </tr>
                    </thead>
                    <tbody>
                        <tr v-for="(row, ridx) in rows" :key="ridx + '-t'" :class="rowClass(row)">
                            <td v-for="col in columns" :key="col + '-s-' + ridx" :class="cellClass(row, col, 'target')">{{ formatValue(getTargetValue(row, col)) }}</td>
                         </tr>
                    </tbody>
                  </table>
                </div>
              </div>
            </div>

          </div>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup>
import { ref, computed, onMounted, watch } from 'vue'

const databases = ref([])
const sourceTables = ref([])
const targetTables = ref([])
const tableOptions = ref([])
const tableName = ref('')
const databaseSource = ref('')
const databaseTarget = ref('')

const rows = ref([])
const loading = ref(false)
const tablesLoading = ref(false)
const error = ref(null)

// derived state
const compareDisabled = computed(() => {
  return loading.value || !databaseSource.value || !databaseTarget.value || !tableName.value
})

// derive columns from rows (union of keys across all rows)
const columns = computed(() => {
  const set = new Set()
  rows.value.forEach(r => {
    getKeys(r).forEach(k => set.add(k))
  })
  const arr = Array.from(set)
  arr.sort((a,b) => {
    if (a === 'Id') return -1
    if (b === 'Id') return 1
    return a.localeCompare(b)
  })
  return arr
})

// helpers: API base (use VITE_API_BASE_URL or relative)
const API_BASE = import.meta.env.VITE_API_BASE_URL || ''
function apiUrl(path) {
  if (!API_BASE) return path
  return API_BASE.replace(/\/$/, '') + path
}

/* API calls */
async function fetchDatabases() {
  error.value = null
  try {
    const res = await fetch(apiUrl('/api/databases'))
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    databases.value = await res.json()
    // set defaults if none
    if (!databaseSource.value && databases.value.length) databaseSource.value = databases.value[0]
    if (!databaseTarget.value && databases.value.length > 1) databaseTarget.value = databases.value[1] || databases.value[0]
  } catch (e) {
    console.error(e)
    error.value = `Impossibile caricare i database: ${e.message || e}`
  }
}

async function fetchTablesFor(db, targetRef) {
  if (!db) {
    targetRef.value = []
    return
  }
  tablesLoading.value = true
  error.value = null
  try {
    const res = await fetch(apiUrl(`/api/tables?database=${encodeURIComponent(db)}`))
    if (!res.ok) throw new Error(`HTTP ${res.status}`)
    const data = await res.json()
    targetRef.value = Array.isArray(data) ? data : []
  } catch (e) {
    console.error(e)
    error.value = `Impossibile caricare le tabelle per ${db}: ${e.message || e}`
    targetRef.value = []
  } finally {
    tablesLoading.value = false
    computeTableOptions()
  }
}

/* Compute table options (intersection preferred) */
function computeTableOptions() {
  const s = sourceTables.value || []
  const t = targetTables.value || []
  if (s.length && t.length) {
    // intersection
    const inter = s.filter(x => t.includes(x))
    tableOptions.value = inter.length ? inter : Array.from(new Set([...s, ...t]))
  } else if (s.length) {
    tableOptions.value = [...s]
  } else if (t.length) {
    tableOptions.value = [...t]
  } else {
    tableOptions.value = []
  }
  // reset selected table if not present
  if (tableName.value && !tableOptions.value.includes(tableName.value)) {
    tableName.value = ''
  }
}

/* when DB changed */
function onDatabaseChange() {
  // fetch tables for each selected db
  fetchTablesFor(databaseSource.value, sourceTables)
  fetchTablesFor(databaseTarget.value, targetTables)
}

/* Compare request */
async function compare() {
  if (!databaseSource.value || !databaseTarget.value || !tableName.value) return
  loading.value = true
  error.value = null
  rows.value = []
  try {
    const payload = {
      databaseSource: databaseSource.value,
      databaseTarget: databaseTarget.value,
      tableName: tableName.value
    }
    const res = await fetch(apiUrl('/api/compare'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })
    if (!res.ok) {
      const text = await res.text()
      throw new Error(`${res.status} ${res.statusText} ${text}`)
    }
    const data = await res.json()
    rows.value = Array.isArray(data) ? data : []
    // compute initial statuses for rows so coloring appears immediately
    rows.value.forEach(r => computeRowStatus(r))
    // small animation trigger: add a tiny delay to allow CSS transition if needed
  } catch (e) {
    console.error(e)
    error.value = `Confronto fallito: ${e.message || e}`
  } finally {
    loading.value = false
  }
}

/* Update target table request */
async function performUpdate() {
  if (!confirm('This will overwrite the target table to match the source table. This is a destructive action. Continue?')) {
    return
  }

  loading.value = true
  error.value = null
  try {
    const payload = {
      databaseSource: databaseSource.value,
      databaseTarget: databaseTarget.value,
      tableName: tableName.value
    }
    const res = await fetch(apiUrl('/api/compare/update'), {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    })
    if (!res.ok) {
      const text = await res.text()
      throw new Error(`${res.status} ${res.statusText} ${text}`)
    }
    const data = await res.json()
    // Refresh the comparison view
    await compare()
  } catch (e) {
    console.error(e)
    error.value = `Update fallito: ${e.message || e}`
    loading.value = false
  }
}

/* utilities for rendering */
function getSource(row) { return row.source || row.Source || {} }
function getTarget(row) { return row.target || row.Target || {} }
function getKeys(row) {
  const s = getSource(row), t = getTarget(row)
  const keys = new Set()
  Object.keys(s || {}).forEach(k => keys.add(k))
  Object.keys(t || {}).forEach(k => keys.add(k))
  // ensure predictable order: put Id first if present
  const arr = Array.from(keys)
  arr.sort((a,b) => {
    if (a === 'Id') return -1
    if (b === 'Id') return 1
    return a.localeCompare(b)
  })
  return arr
}
function getSourceValue(row, key) {
  const s = getSource(row)
  return (s && Object.prototype.hasOwnProperty.call(s, key)) ? s[key] : null
}
function getTargetValue(row, key) {
  const t = getTarget(row)
  return (t && Object.prototype.hasOwnProperty.call(t, key)) ? t[key] : null
}
function formatValue(v) {
  if (v === null || v === undefined) return ''
  if (v instanceof Object) return JSON.stringify(v)
  return String(v)
}
function equalValues(a,b) {
  if (a === null && b === null) return true
  if (a === null || b === null) return false
  // coerce dates/numbers/strings sensibly
  return String(a) === String(b)
}

/* row-level class (soft background) */
function rowClass(row, _type) {
  const st = (row.status || row.Status || '').toString()
  if (st === 'Equal') return 'row-equal'
  if (st === 'Different') return 'row-different'
  if (st === 'MissingSource' || st === 'MissingTarget') return 'row-missing'
  return ''
}

/* cell-level class: highlight only when different or missing on that side */
function cellClass(row, key, side) {
  const sVal = getSourceValue(row, key)
  const tVal = getTargetValue(row, key)
  const st = (row.status || row.Status || '').toString()
  // missing on source/target
  if (side === 'source' && (sVal === null || sVal === undefined) && (tVal !== null && tVal !== undefined)) return 'cell-missing'
  if (side === 'target' && (tVal === null || tVal === undefined) && (sVal !== null && sVal !== undefined)) return 'cell-missing'
  // different values
  if (!equalValues(sVal, tVal)) return 'cell-different'
  // otherwise, nothing
  return ''
}

function getKeyValue(row, key) {
  const v = getSourceValue(row, key) ?? getTargetValue(row, key)
  return v === null || v === undefined ? '' : v
}

// parse input value to number if source suggests numeric value
function parseInputValue(raw, row, key) {
  if (raw === null || raw === undefined) return null
  const sVal = getSourceValue(row, key)
  // try to parse decimal with comma or dot
  const cleaned = String(raw).trim().replace(',', '.')
  if (typeof sVal === 'number') {
    const n = parseFloat(cleaned)
    return Number.isNaN(n) ? raw : n
  }
  // if source is numeric-like, try parse
  if (sVal !== undefined && sVal !== null && !isNaN(Number(sVal))) {
    const n = parseFloat(cleaned)
    return Number.isNaN(n) ? raw : n
  }
  return raw
}

function computeRowStatus(row) {
  const s = getSource(row)
  const t = getTarget(row)
  if (!s || Object.keys(s).length === 0) { row.status = 'MissingSource'; return }
  if (!t || Object.keys(t).length === 0) { row.status = 'MissingTarget'; return }
  const keys = getKeys(row)
  for (const k of keys) {
    const sv = s[k]
    const tv = t[k]
    if (!equalValues(sv, tv)) { row.status = 'Different'; return }
  }
  row.status = 'Equal'
}

function onTargetInput(e, ridx, col) {
  const raw = e.target.value
  const row = rows.value[ridx]
  if (!row) return
  if (!row.target && !row.Target) row.target = {}
  const parsed = parseInputValue(raw, row, col)
  // set on the correct property (target or Target)
  if (row.target !== undefined) row.target[col] = parsed
  else row.Target[col] = parsed
  // recompute status for the row
  computeRowStatus(row)
}

onMounted(async () => {
  await fetchDatabases()
  // initial tables load if defaults set
  if (databaseSource.value) fetchTablesFor(databaseSource.value, sourceTables)
  if (databaseTarget.value) fetchTablesFor(databaseTarget.value, targetTables)
})

watch([sourceTables, targetTables], () => computeTableOptions())
</script>

<style scoped>
/* Page/background */
.page {
  background: #f5f7fa;
  min-height: 100vh;
  padding: 28px 16px;
  box-sizing: border-box;
  font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  color: #1f2937;
}

/* central container - full width for full-page panel */
.container {
  width: 100%;
  max-width: 100%;
  margin: 0 auto;
  display: grid;
  grid-template-columns: 1fr;
  gap: 18px;
  padding: 0 18px;
}

/* card */
.card {
  background: #fff;
  border-radius: 10px;
  padding: 14px;
  box-shadow: 0 6px 16px rgba(31,41,55,0.06);
  /* make card adapt to page width with consistent side margin */
  width: calc(100% - 36px);
  margin: 0 18px;
  box-sizing: border-box;
}

.top-card .controls {
  display: flex;
  gap: 12px;
  align-items: flex-end; /* align controls (selects) and button on the same baseline */
  flex-wrap: wrap;
  /* fixed control width for dropdowns and button */
  --control-width: 260px;
}
.field {
  display: flex;
  flex-direction: column;
  width: var(--control-width);
}
.field.wide { width: var(--control-width); }
.field label { font-size: 15px; margin-bottom: 6px; color: #374151; font-weight: 700; }
.field select {
  padding: 8px 10px;
  height: 40px;
  border-radius: 8px;
  border: 1px solid #e6e9ee;
  background: #fff;
  outline: none;
  font-size: 14px;
  width: 100%;
}
.field select:disabled {
  opacity: 0.6;
}

.actions { display:flex; align-items:flex-end; gap:8px; }
button.primary {
  background: #e5e7eb; /* light gray */
  color: #111827; /* dark text */
  border: none;
  padding: 0 12px;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 600;
  box-shadow: 0 2px 6px rgba(15,23,42,0.06);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: var(--control-width);
  height: 40px; /* same height as dropdown */
}
button.primary:disabled { opacity: 0.6; cursor: not-allowed; box-shadow:none; }
button.primary:hover:not(:disabled) { background: #d1d5db; }
button.primary:focus { outline: 3px solid rgba(17,24,39,0.06); }

/* update button - warning/destructive style */
button.update-btn {
  background: #ff9800; /* orange warning color */
  color: #fff;
  border: none;
  padding: 0 12px;
  border-radius: 8px;
  cursor: pointer;
  font-weight: 600;
  box-shadow: 0 2px 6px rgba(15,23,42,0.06);
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: 8px;
  width: var(--control-width);
  height: 40px; /* same height as primary button */
}
button.update-btn:disabled { opacity: 0.6; cursor: not-allowed; box-shadow: none; }
button.update-btn:hover:not(:disabled) { background: #e68900; }
button.update-btn:focus { outline: 3px solid rgba(255,152,0,0.2); }

/* spinner */
.spinner {
  border: 3px solid #e6e9ee;
  border-top: 3px solid #007bff;
  border-radius: 50%;
  width: 18px;
  height: 18px;
  animation: spin 0.8s linear infinite;
}
@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

/* table styles */
.data-table {
  width: 100%;
  border-collapse: collapse;
  margin-top: 10px;
  /* allow horizontal scrolling when table is wider than column */
  min-width: 600px;
}
.data-table th, .data-table td {
  padding: 10px;
  border: 1px solid #e6e9ee;
}
.data-table th {
  background: #f8f9fa;
  text-align: left;
  font-weight: 600;
}
.data-table td {
  background: transparent;
}

/* layout for side-by-side tables */
.tables-wrapper {
  display: flex;
  gap: 16px;
  align-items: stretch;
  flex-wrap: nowrap; /* keep side-by-side on wide screens */
}
.table-column {
  flex: 0 0 calc(50% - 8px);
  min-width: 280px;
  border-radius: 8px;
  overflow: hidden;
  background: #fff;
  border: 1px solid #eef2f7;
  display: flex;
  flex-direction: column;
}
/* sticky header for each table (Source / Target) */
.sticky-header {
  padding: 12px 16px;
  font-weight: 800;
  font-size: 18px;
  text-align: center;
  background: linear-gradient(180deg, #ffffff, #fbfdff);
  border-bottom: 1px solid #f1f5f9;
  z-index: 3;
}
.scroll-area {
  flex: 1 1 auto;
  overflow-x: auto;
  overflow-y: visible;
  -webkit-overflow-scrolling: touch;
}

@media (max-width: 900px) {
  .tables-wrapper { flex-direction: column; }
  .table-column { flex: 1 1 auto; width: 100%; }
}

.inline-edit {
  width: 100%;
  border: none;
  background: transparent;
  padding: 6px 4px;
  font: inherit;
}
.inline-edit:focus { outline: 2px solid rgba(37,99,235,0.12); border-radius: 4px }

/* row/column highlighting */
.row-equal {
  background: #e6ffed;
}
.row-different {
    background: #ffb3b3;
}
.row-missing {
  background: #fff3cd;
}
.cell-missing {
  background: #e9ecef;
}
.cell-different {
  background: #ffe6e6;
}

/* responsive adjustments */
@media (max-width: 900px) {
  .top-card .controls {
    flex-direction: column;
    align-items: stretch;
  }
  .field, .field.wide { width: 100%; }
  .actions { margin-left: 0; }
  button.primary { width: 100%; }
}
</style>