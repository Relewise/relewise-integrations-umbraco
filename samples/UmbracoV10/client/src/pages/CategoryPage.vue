<template>
<section class="bgwhite p-t-55 p-b-65">
    <div class="container">
        <div class="row">
            <div class="col-sm-6 col-md-4 col-lg-3 p-b-50">
                <div class="leftbar p-r-20 p-r-0-sm">
                    <template v-if="result?.facets.category">
                        <h4 class="m-text14 p-b-7">
                            Categories
                        </h4>

                        <ul class="p-b-54">
                            <li class="p-t-4">
                                <a href="#" class="s-text13" @click.prevent="category2Id = null; goToPage(1)" :class="{'active1': result?.facets.category.every(x => !x.selected)}">
                                    All
                                </a>
                            </li>

                            <li class="p-t-4" v-for="cat in result?.facets.category" :key="cat.displayName">
                                <a href="#" class="s-text13" @click.prevent="category2Id = cat.value; goToPage(1)" :class="{'active1': cat.selected}">
                                    {{cat.displayName}} ({{cat.hits}})
                                </a>
                            </li>
                        </ul>
                    </template>

                    <template v-if="result?.facets.country">
                        <h4 class="m-text14 p-b-7">
                            Country
                        </h4>

                        <ul class="p-b-54">
                            <li class="p-t-4">
                                <a href="#" class="s-text13" @click.prevent="country = null; goToPage(1)" :class="{'active1': result?.facets.country.every(x => !x.selected)}">
                                    All
                                </a>
                            </li>

                            <li class="p-t-4" v-for="ctry in result?.facets.country" :key="ctry.displayName">
                                <a href="#" class="s-text13" @click.prevent="country = ctry.value; goToPage(1)" :class="{'active1': ctry.selected}">
                                    {{ctry.displayName}} ({{ctry.hits}})
                                </a>
                            </li>
                        </ul>
                    </template>

                </div>
            </div>

            <div class="col-sm-6 col-md-8 col-lg-9 p-b-50">
                <!--  -->
                <div class="flex-sb-m flex-w p-b-35">
                    <div class="flex-w">
                        <div class="w-size12 m-t-5 m-b-5 m-r-10">
                            <select name="sorting" v-model="sorting" @click="goToPage(1)">
                                <option :value="null">Popularity</option>
                                <option :value="'priceLow'">Price: low to high</option>
                                <option :value="'priceHigh'">Price: high to low</option>
                            </select>
                        </div>
                    </div>

                    <span class="s-text8 p-t-5 p-b-5">
                        Showing {{ page * pageSize - 11 }}-{{ showingMax }} of {{ result?.hits }} results
                    </span>
                </div>

                <!-- Product -->
                <div class="row">
                    <div class="col-sm-12 col-md-6 col-lg-4 p-b-40" v-for="product in result?.results" :key="product.productId">
                        <!-- Block2 -->
                        <ProductBlock :product="product" />
                    </div>
                </div>

                <!-- Pagination -->
                <div class="pagination flex-m flex-w p-t-26" v-if="pages > 1">
                    <a href="#" v-for="index in pages" @click.prevent="goToPage(index)" :key="index" class="item-pagination flex-c-m trans-0-4" :class="{'active-pagination': page == index }">
                        {{ index }}
                    </a>
                </div>
            </div>
        </div>
    </div>
</section>
</template>

<script setup lang="ts">
import trackingService from '@/services/tracking.service'
import { ProductResult } from '@relewise/client'
import { Ref, ref , computed } from 'vue'
import ProductBlock from '../components/ProductBlock.vue'

interface FacetValue {
    value :string;
    displayName:string;
    hits:number;
    selected: boolean;
}

interface ProductsResult {
  hits: number;
  results: ProductResult[];
  facets: { [key: string]: FacetValue[] };
}

const category = document.getElementById('category-page')?.attributes.getNamedItem('category')?.value
const pageSize = 12
const result: Ref<ProductsResult|null> = ref(null)
const page: Ref<number> = ref(1)
const pages: Ref<number> = ref(0)
const category2Id: Ref<string|null> = ref(null)
const country: Ref<string|null> = ref(null)
const sorting: Ref<string|null> = ref(null)
const showingMax = computed(() => Math.min(page.value * pageSize, result.value?.hits ?? 0))

const hasError = ref(false)

function goToPage (nextPage: number) {
  page.value = nextPage
  search()
  window.scrollTo({ top: 0, behavior: 'smooth' })
}

function search () {
  const urlParams = new URLSearchParams()
  urlParams.set('page', String(page.value))
  urlParams.set('pageSize', String(pageSize))
  urlParams.set('displayedAt', 'category page')

  if (category) {
    urlParams.set('categoryId', category)
  }

  if (category2Id.value) {
    urlParams.set('category2Id', category2Id.value)
  }

  if (country.value) {
    urlParams.set('country', country.value)
  }

  if (sorting.value) {
    urlParams.set('sortBy', sorting.value)
  }

  fetch('/api/catalog/search?' + urlParams.toString())
    .then(response => response.json())
    .then(data => {
      result.value = data
      const n = data.hits % pageSize
      pages.value = Number((n > 0 ? (data.hits / pageSize) + 1 : data.hits / pageSize).toFixed())
      hasError.value = false
    })
    .catch(() => { hasError.value = true })
}

search()

trackingService.trackProductCategoryView(category ?? '')

</script>
