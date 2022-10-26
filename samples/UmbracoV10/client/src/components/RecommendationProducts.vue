<template>
  <section class="newproduct bgwhite p-b-105" v-if="result && result.length > 0">
    <div class="container">
        <div class="sec-title p-b-60">
            <h3 class="m-text5 t-center">
                {{ title }}
            </h3>
        </div>

        <!-- Slide2 -->
        <div class="wrap-slick2" :class="`wrap-slick2${type ?? ''}`">
            <div class="" :class="`slick2${type ?? ''}`">
                <div class="item-slick2 p-l-15 p-r-15" v-for="product in result" :key="product.productId">
                  <ProductBlock :product="product"></ProductBlock>
                </div>
          </div>
      </div>
    </div>
  </section>

  <div v-if="hasError" class="relewise-error">
    <h2>Recommendation API request failed</h2>
    <p class="relewise-error">The recommendation API request failed. This is likely due to a misconfiguration in the Relewise-appsettings section. Please verify that the dataset-id and API-key has been correctly configured</p>
  </div>
</template>

<script setup lang="ts">
import { ProductRecommendationResponse, ProductResult } from '@relewise/client';
import { nextTick, ref, Ref, defineProps, toRefs, PropType } from 'vue'
import ProductBlock from './ProductBlock.vue'

const props = defineProps({
  title: String,
  type: String,
  recommendations: { type: Object as PropType<ProductRecommendationResponse> }
})

const { title, type, recommendations } = toRefs(props)

const result: Ref<ProductResult[]|null> = ref(null)
const hasError = ref(false)

function recommend () {
  hasError.value = false

  if (recommendations && recommendations.value !== undefined) {
    result.value = recommendations.value.recommendations;
    nextTick(() => slider())
  } else {
    fetch('/api/catalog/recommend/popular')
      .then(response => response.json())
      .then(data => {
        result.value = data
        hasError.value = false
        nextTick(() => slider())
      })
      .catch(() => { hasError.value = true })
  }
}

function slider () {
  const elementId = `.slick2${type?.value ?? ''}`

  /* eslint-disable */
  $(elementId).slick({
    slidesToShow: 4,
    slidesToScroll: 4,
    infinite: true,
    autoplay: false,
    autoplaySpeed: 6000,
    arrows: true,
    appendArrows: $(`.wrap-slick2${type.value ?? ''}`),
    prevArrow: '<button class="arrow-slick2 prev-slick2"><i class="fa  fa-angle-left" aria-hidden="true"></i></button>',
    nextArrow: '<button class="arrow-slick2 next-slick2"><i class="fa  fa-angle-right" aria-hidden="true"></i></button>',
    responsive: [
      {
        breakpoint: 1200,
        settings: {
          slidesToShow: 4,
          slidesToScroll: 4
        }
      },
      {
        breakpoint: 992,
        settings: {
          slidesToShow: 3,
          slidesToScroll: 3
        }
      },
      {
        breakpoint: 768,
        settings: {
          slidesToShow: 2,
          slidesToScroll: 2
        }
      },
      {
        breakpoint: 576,
        settings: {
          slidesToShow: 1,
          slidesToScroll: 1
        }
      }
    ]
  })
}

recommend()
</script>
