<template>
  <div v-if="result" class="row">
   <template v-if="result && result.length > 0">
    <a class="col-4" v-for="content in result" :key="content.contentId" :href="content.data.Url.value">
      <h4>{{ content.displayName }}</h4>
      <span v-html="content.data.splashText.value"></span>
    </a>
    </template>
    <template v-else-if="result && result.length === 0">
      <div class="col-12 fs-14">No results found</div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { Ref, ref } from '@vue/runtime-dom'

const term: Ref<string|null> = ref('')
const result: any|null = ref(null)

function search () {
  term.value = new URLSearchParams(window.location.search).get('q')
  fetch('/api/content/search?q=' + term.value)
    .then(response => response.json())
    .then(data => { result.value = data })
}

search()
</script>

<style scoped>

</style>
