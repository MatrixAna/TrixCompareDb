
<template>
  <div>
    <div v-if="loading">Loading products...</div>
    <div v-else>
      <div v-if="error" style="color:crimson">Error: {{ error }}</div>
      <div v-else>
        <table border="1" cellpadding="8" cellspacing="0" style="border-collapse:collapse;width:100%">
          <thead style="background:#eee;text-align:left">
            <tr>
              <th>ID</th>
              <th>Name</th>
              <th>Price</th>
              <th>Description</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="p in products" :key="p.id">
              <td>{{ p.id }}</td>
              <td>{{ p.name }}</td>
              <td>{{ formatPrice(p.price) }}</td>
              <td>{{ p.description }}</td>
            </tr>
          </tbody>
        </table>
        <div v-if="products.length === 0" style="margin-top:12px">Nessun prodotto trovato.</div>
      </div>
    </div>
  </div>
</template>

<script>
export default {
  name: 'ProductsList',
  data() {
    return {
      products: [],
      loading: true,
      error: null
    }
  },
  methods: {
    async fetchProducts() {
      this.loading = true
      this.error = null
      try {
        const base = import.meta.env.VITE_API_BASE_URL || ''
        const url = (base === '' ? '/api/products' : (base.endsWith('/') ? base.slice(0, -1) : base) + '/api/products')
        const res = await fetch(url, { mode: 'cors' })
        if (!res.ok) throw new Error(`${res.status} ${res.statusText}`)
        this.products = await res.json()
      } catch (err) {
        this.error = err.message || String(err)
      } finally {
        this.loading = false
      }
    },
    formatPrice(v) {
      if (v == null) return ''
      return new Intl.NumberFormat('it-IT', { style: 'currency', currency: 'EUR' }).format(v)
    }
  },
  mounted() {
    this.fetchProducts()
  }
}
</script>

<style scoped>
table th, table td { padding:8px }
</style>
