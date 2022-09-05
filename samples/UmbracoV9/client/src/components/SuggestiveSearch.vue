<template>
    <div class="search-product pos-relative bo4 of-hidden">
        <input id="search-input" class="s-text7 size13 p-l-8 p-r-30" type="text" v-model="term" @keyup="debouncer(() => search(), 500)" @keydown.enter="goToSearch(term)" name="search-content" placeholder="Search..." @focus="enterSearch" @blur="leaveSearch">

        <button class="flex-c-m size25 ab-r-m color2 color0-hov trans-0-4">
            <i class="fs-14 fa fa-search" aria-hidden="true"></i>
        </button>
    </div>
    <div v-if="result || predictions" class="results fs-14">
        <div class="dis-flex w-100">
            <div class="w-25 dis-flex flex-col">
                <template v-if="predictions && predictions.length > 0">
                    <div class="result__element"><strong>Suggestions</strong></div>

                    <a href="#" class="result__element" style="height: 40px;" v-for="prediction in predictions" :key="prediction.term" @click="goToSearch(prediction.term)">
                        {{ prediction.term }}
                        </a>
                </template>
            </div>
            <div class="result__list">
                <template v-if="products && products.hits > 0">
                    <div class="result__element"><strong>Products</strong></div>

                    <template v-for="product in products?.results" :key="product.productId">
                        <div class="result__element">
                            <div class="dis-flex flex-m">
                                <img width="40" height="40" :src="product.data.ImageUrl?.value ?? '/images/item-02.jpg'" :alt="product.displayName" class="m-r-8" />
                                <a :href="`/product/${product.productId}`">{{product.displayName}}</a>
                            </div>
                        </div>
                    </template>
                </template>
                <template v-if="result && result.length > 0">
                    <div class="result__element"><strong>Contents</strong></div>
                    <template v-for="content in result" :key="content.contentId">
                        <div class="result__element">
                            <div class="dis-flex flex-m">
                                <img width="40" height="40" :src="content.data.splashImage_Block?.value ?? '/images/item-02.jpg'" :alt="content.displayName" class="m-r-8" />
                                <a :href="content.data.url.value">{{content.displayName}}</a>
                            </div>
                        </div>
                    </template>
                </template>
                <template v-else-if="result && result.length === 0 && products && products.hits === 0">
                    <div class="result__element">No results found</div>
                </template>
            </div>
        </div>
    </div>
    <div v-if="hasError" class="results p-t-8 p-b-8 p-l-8 p-r-8 relewise-error">
        <h4>Search API request failed</h4>
        <p class="relewise-error">The search API request failed. This is likely due to a misconfiguration in the Relewise-appsettings section. Please verify that the dataset-id and API-key has been correctly configured</p>
    </div>
</template>

<script setup lang="ts">
import { ProductSearchResponse } from '@relewise/client'
import { ref, Ref } from 'vue'

interface ContentResult {
  displayName: string;
  contentId: string;
  data: {[key: string]: string};
}

interface PredictionResult {
  term: string;
}

const term = ref('')
const result: Ref<ContentResult[]|null> = ref(null)
const products: Ref<ProductSearchResponse|null> = ref(null)
const predictions: Ref<PredictionResult[]|null> = ref(null)
const debouncer = createDebounce()
const hasError = ref(false)

function search () {
  if (!term.value) return

  fetch('/api/global/search?q=' + term.value)
    .then(response => response.json())
    .then(data => {
      result.value = data.contents.results
      predictions.value = data.predictions.predictions
      products.value = data.products
      hasError.value = false
    })
    .catch(() => { hasError.value = true })
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
        width: 602px;
        border-bottom-left-radius: 6px;
        border-bottom-right-radius: 6px;
    }

    #search-input {
        -webkit-transition: all .5s;
        -moz-transition: all .5s;
        transition: all .5s;
        width: 215px;
        max-width: inherit;
    }

    #search-input.active {
        width: 600px;
        max-width: inherit;
    }

    .flex {
        display: flex;
    }

    .w-25 {
        width: 25%;
    }

    .w-100 {
        width: 100%;
    }

    .result__list {
        width: 75%;
        display: flex;
        flex-direction: column;
        border-left: 1px solid #e6e6e6;
    }

    .result__element {
        padding: 8px;
        border-bottom: 1px solid #e6e6e6;

        &:last-to-type  {
            border-bottom: 0;
        }
    }
</style>
