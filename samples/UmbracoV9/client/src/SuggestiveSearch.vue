<template>
  <div class="search-product pos-relative bo4 of-hidden">
    <input id="search-input" class="s-text7 size13 p-l-8 p-r-30" type="text" v-model="term" @keyup="debouncer(() => search(), 500)" @keydown.enter="goToSearch(term)" name="search-content" placeholder="Search..." @focus="enterSearch" @blur="leaveSearch">

    <button class="flex-c-m size25 ab-r-m color2 color0-hov trans-0-4">
      <i class="fs-14 fa fa-search" aria-hidden="true"></i>
    </button>
  </div>
  <div v-if="result || predictions" class="results fs-14">
    <template v-if="predictions">
    <strong class="result fs-12">Terms</strong>
    <a href="#" class="result fs-14" v-for="prediction in predictions" :key="prediction.term" @click="goToSearch(prediction.term)">
      {{ prediction.term }}
    </a>
    </template>

   <template v-if="result && result.length > 0">
   <strong class="result m-t-10 fs-12">Pages</strong>
    <a class="result fs-14" v-for="content in result" :key="content.contentId" :href="content.data.Url.value">
      {{ content.displayName }}
    </a>
    <a href="#" class="result fs-14" @click="goToSearch(term)">See all...</a>
    </template>
    <template v-else-if="result && result.length === 0">
      <div class="result fs-14">No results found</div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { ref } from '@vue/runtime-dom'

const term = ref('')
const result: any|null = ref(null)
const predictions: any|null = ref(null)
const debouncer = createDebounce()

function search () {
  fetch('/api/content/search?q=' + term.value)
    .then(response => response.json())
    .then(data => { result.value = data })

  fetch('/api/content/predict?q=' + term.value)
    .then(response => response.json())
    .then(data => { predictions.value = data })
}

function goToSearch (term: string) {
  window.location.href = '/search?q=' + term
}

function createDebounce () {
  let timeout: number|null = null
  return function (fnc: () => void, delayMs: number) {
    if (timeout) {
      clearTimeout(timeout)
    }

    timeout = setTimeout(() => {
      fnc()
    }, delayMs || 500)
  }
}

function leaveSearch () {
  setTimeout(() => {
    term.value = ''
    result.value = null
    predictions.value = null
    document.getElementById('search-input')?.classList.toggle('active')
  }, 200)
}

function enterSearch () {
  document.getElementById('search-input')?.classList.toggle('active')
}

</script>
<style scoped>
.results {
    position: absolute;
    display: flex;
    flex-wrap: wrap;
    border: 1px solid #e6e6e6;
    background-color: #fff;
    margin-top: -1px;
    width: 502px;
    border-bottom-left-radius: 6px;
    border-bottom-right-radius: 6px;
}
.result {
    width: 100%;
    line-height: 1.5;
    border-bottom: 1px solid #ddd;
    padding: 5px;
}

#search-input {
    -webkit-transition: all .5s;
    -moz-transition: all .5s;
    transition: all .5s;
    width: 215px;
    max-width: inherit;
}
#search-input.active {
    width: 500px;
    max-width: inherit;
}
</style>
