<template>
    <div class="block2">
        <div class="block2-img wrap-pic-w of-hidden pos-relative">
            <img :src="product?.data?.ImageUrl?.value ?? '/images/item-02.jpg'" :alt="product.displayName">

            <div class="block2-overlay trans-0-4">
                <div class="block2-btn-addcart w-size1 trans-0-4">
                    <!-- Button -->
                    <button class="flex-c-m size1 bg4 bo-rad-23 hov1 s-text1 trans-0-4" @click="addToBasket(product)">
                        Add to Cart
                    </button>
                </div>
            </div>
        </div>

        <div class="block2-txt p-t-10">
            <a class="block2-name dis-block s-text3 p-b-5" :href="`/product/${product.productId}`">
                {{product.displayName}}
            </a>

            <span class="block2-price m-text6 p-r-5">
                ${{product.salesPrice}}
            </span>
        </div>
    </div>
</template>

<script lang="ts" setup>
import basketService from '@/services/basket.service'
import { ProductResult } from '@relewise/client'
import { toRefs, defineProps } from 'vue'

const props = defineProps({
  product: ProductResult
})

const { product } = toRefs(props)

async function addToBasket (product: ProductResult) {
  // eslint-disable-next-line no-undef
  swal(product.displayName ?? 'no product name', 'is added to cart !', 'success')

  await basketService.addProduct({ product: product, quantityDelta: 1 })
}
</script>
