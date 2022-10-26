<template>
<div class="container bgwhite p-t-35 p-b-80">
    <div class="flex-w flex-sb">
        <div class="w-size13 p-t-30 respon5">
            <div class="wrap-slick3 flex-sb flex-w">
                <div class="wrap-slick3-dots"></div>

                <div class="slick3">
                    <div class="item-slick3" data-thumb="images/thumb-item-01.jpg">
                        <div class="wrap-pic-w">
                            <img :src="product?.data?.ImageUrl?.value ?? '/images/item-02.jpg'" :alt="product?.displayName ?? ''">
                        </div>
                    </div>
                </div>
            </div>
        </div>

        <div class="w-size14 p-t-30 respon5">
            <h4 class="product-detail-name m-text16 p-b-13">
                {{product?.displayName}}
            </h4>

            <span class="m-text17">
                ${{product?.salesPrice}}
            </span>

            <div class="p-b-20">
                <span class="s-text8 m-r-35">SKU: {{product?.productId}}</span>
            </div>
            <p class="s-text8 p-t-10">
                {{product?.data?.ShortDescription?.value}}
            </p>

            <div class="p-t-33 p-b-60">

                <div class="flex-r-m flex-w p-t-10">
                    <div class="w-size16 flex-m flex-w">
                        <div class="flex-w bo5 of-hidden m-r-22 m-t-10 m-b-10">
                            <button class="btn-num-product-down color1 flex-c-m size7 bg8 eff2" @click="quantity = quantity - 1;">
                                <i class="fs-12 fa fa-minus" aria-hidden="true"></i>
                            </button>

                            <input class="size8 m-text18 t-center num-product" type="number" name="num-product" v-model="quantity">

                            <button class="btn-num-product-up color1 flex-c-m size7 bg8 eff2" @click="quantity = quantity + 1">
                                <i class="fs-12 fa fa-plus" aria-hidden="true"></i>
                            </button>
                        </div>

                        <div class="btn-addcart-product-detail size9 trans-0-4 m-t-10 m-b-10">
                            <button class="flex-c-m sizefull bg1 bo-rad-23 hov1 s-text1 trans-0-4" @click="addToCart()">
                                Add to Cart
                            </button>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    </div>
</div>

<RecommendationProducts v-if="recommendationsLoaded" :recommendations="recommendations['viewedAfter']" type="viewedafter" title="Others are also looking at" />
<RecommendationProducts v-if="recommendationsLoaded" :recommendations="recommendations['purchasedWith']" type="purchasedwith" title="People also bought" />
</template>

<script setup lang="ts">
import RecommendationProducts from '../components/RecommendationProducts.vue'
import { ProductRecommendationResponse, ProductResult } from '@relewise/client'
import { Ref, ref } from 'vue'
import trackingService from '@/services/tracking.service'
import basketService from '@/services/basket.service'

const productId = document.getElementById('product-page')?.attributes.getNamedItem('product-id')?.value
const product: Ref<ProductResult|null> = ref(null)
const quantity: Ref<number> = ref(1)
const recommendations: Ref<Record<string, ProductRecommendationResponse>> = ref({});

const hasError = ref(false)
const recommendationsLoaded = ref(false)

function getProduct () {
  const urlParams = new URLSearchParams()
  urlParams.set('productId', productId ?? '')
  urlParams.set('page', '1')
  urlParams.set('pageSize', '1')
  urlParams.set('displayedAt', 'product page')

  fetch('/api/catalog/search?' + urlParams.toString())
    .then(response => response.json())
    .then(data => {
      product.value = data.results[0]
      hasError.value = false
    })
    .catch(() => { hasError.value = true })

    fetch(`/api/catalog/recommend/products?productId=` + productId)
      .then(response => response.json())
      .then(data => {
        recommendations.value = data
        recommendationsLoaded.value = true;
      })
}

async function addToCart () {
  await basketService.addProduct({ product: product.value, quantityDelta: quantity.value })
}

getProduct()
trackingService.trackProductView(productId)
</script>
