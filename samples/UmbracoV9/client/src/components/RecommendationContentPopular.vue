<template>
  <div v-if="result && result.length > 0" class="row">
    <template v-for="(group, index) in result" :key="index">
        <div class="col-sm-10 col-md-8 col-lg-4 m-l-r-auto">
          <!-- block1 -->
          <div class="block1 b-size hov-img-zoom pos-relative m-b-30" v-for="(content, contentIndex) in group" :key="content.contentId" :class="showLarge(index, contentIndex) ? '-large' : ''">
              <span :style="{ backgroundImage: 'url(' + content.data?.splashImage_Block?.value + ')' }" :alt="content.displayName" class="c-image"></span>

              <div class="block1-wrapbtn w-size2">
                  <!-- Button -->
                  <a :href="content.data?.Url?.value" class="content-button flex-c-m size2 m-text2 bg3 hov1 trans-0-4" :title="content.displayName">
                      <span class="text-truncate">{{ content.displayName }}</span>
                  </a>
              </div>
          </div>
      </div>
    </template>
  </div>
  <div v-if="hasError" class="relewise-error">
    <h2>Recommendation API request failed</h2>
    <p class="relewise-error">The recommendation API request failed. This is likely due to a misconfiguration in the Relewise-appsettings section. Please verify that the dataset-id and API-key has been correctly configured</p>
  </div>
</template>

<script setup lang="ts">
import { ref } from '@vue/runtime-dom'

interface RecommendationResult {
  displayName: string;
}

const result: RecommendationResult[][]|null = ref(null)
const hasError = ref(false)

function showLarge (index: number, contentIndex: number) {
  return (contentIndex === 0 && index !== 1) || (contentIndex === 1 && index === 1)
}

function recommend () {
  hasError.value = false
  fetch('/api/content/recommend/popular')
    .then(response => response.json())
    .then(data => { result.value = data })
    .catch(() => { hasError.value = true })
}

recommend()
</script>

<style scoped>
.b-size {
  max-width: 100%;
  height: 339px;
}
.-large  {
  height: 479px;
}
.content-button {
  padding: 8px;
  line-height: 1.2;
}
.c-image {
  height: 100%;
  width: 100%;
  display: block;
  background-size: cover;
  transition: all 0.6s;
}
.b-size:hover .c-image {
  transform: scale(1.1);
  transition: all .5s;
}
</style>
