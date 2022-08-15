<template>
  <div v-if="products" class="row">
    <template v-if="products && products.length > 0">
      <div class="col-sm-12 col-md-6 col-lg-4 p-b-40" v-for="product in products" :key="product.productId">
          <ProductBlock :product="product" />
      </div>
    </template>
    <template v-else-if="products && products.length === 0">
      <div class="col-12 fs-14">No product results found</div>
    </template>
  </div>
  <div v-if="contents" class="row p-t-50">
   <template v-if="contents && contents.length > 0">
    <a class="col-4" v-for="content in contents" :key="content.contentId" :href="content.data.url.value">
      <h4>{{ content.displayName }}</h4>
      <span v-html="content.data.splashText.value"></span>
    </a>
    </template>
    <template v-else-if="contents && contents.length === 0">
      <div class="col-12 fs-14">No content results found</div>
    </template>
  </div>

  <div v-if="hasError" class="relewise-error">
    <h2>Search API request failed</h2>
    <p class="relewise-error">The search API request failed. This is likely due to a misconfiguration in the Relewise-appsettings section. Please verify that the dataset-id and API-key has been correctly configured</p>
  </div>
</template>

<script setup lang="ts">
import { ProductResult } from '@relewise/client'
import { Ref, ref } from '@vue/runtime-dom'

import ProductBlock from '../components/ProductBlock.vue'

interface ContentResult {
  displayName: string;
  contentId: string;
  data: {[key: string]: string};
}

const term: Ref<string|null> = ref('')
const contents: Ref<ContentResult[]|null> = ref(null)
const products: Ref<ProductResult[]|null> = ref(null)
const hasError = ref(false)

function search () {
  term.value = new URLSearchParams(window.location.search).get('q')
  fetch('/api/content/search?q=' + term.value)
    .then(response => response.json())
    .then(data => {
      contents.value = data
      hasError.value = false
    })
    .catch(() => { hasError.value = true })

  const urlParams = new URLSearchParams()
  urlParams.set('page', String(1))
  urlParams.set('pageSize', String(30))
  urlParams.set('q', term.value ?? '')
  urlParams.set('displayedAt', 'Search Page')
  fetch('/api/catalog/search?' + urlParams.toString())
    .then(response => response.json())
    .then(data => {
      products.value = data.results
      hasError.value = false
    })
    .catch(() => { hasError.value = true })
}

search()
</script>
