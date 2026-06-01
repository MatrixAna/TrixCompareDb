<template>
  <div style="font-family: Arial, Helvetica, sans-serif; max-width:800px;margin:32px auto;">
    <h1>TrixCompare - Products</h1>
    <ProductsList />
  </div>
</template>

<script>
import ProductsList from './components/ProductsList.vue'
export default {
  components: { ProductsList }
}
</script>

<style>
body { background: #f7f7f7; }
</style>
