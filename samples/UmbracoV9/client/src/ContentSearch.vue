<template>
  <div v-if="result" class="row">
   <template v-if="result && result.length > 0">
    <a class="col-4" v-for="content in result" :key="content.contentId" :href="content.data.url.value">
      <h4>{{ content.displayName }}</h4>
      <span v-html="content.data.splashText.value"></span>
    </a>
    </template>
    <template v-else-if="result && result.length === 0">
      <div class="col-12 fs-14">No results found</div>
    </template>
  </div>
    <div v-if="hasError" class="relewise-error">
    <h2>Search API request failed</h2>
    <p class="relewise-error">The search API request failed. This is likely due to a misconfiguration in the Relewise-appsettings section. Please verify that the dataset-id and API-key has been correctly configured</p>
  </div>
</template>

<script setup lang="ts">
import { Ref, ref } from '@vue/runtime-dom'

interface ContentResult {
  displayName: string;
  contentId: string;
  data: {[key: string]: string};
}

const term: Ref<string|null> = ref('')
const result: ContentResult[]|null = ref(null)
const hasError = ref(false)

function search () {
  term.value = new URLSearchParams(window.location.search).get('q')
  fetch('/api/content/search?q=' + term.value)
    .then(response => response.json())
    .then(data => {
      result.value = data
      hasError.value = false
    })
    .catch(() => { hasError.value = true })
}

search()
</script>
