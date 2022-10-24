<template>
<div class="container-table-cart pos-relative">
    <div class="wrap-table-shopping-cart bgwhite">
        <table class="table-shopping-cart">
            <tbody>
                <tr class="table-head">
                    <th class="column-1"></th>
                    <th class="column-2">Product</th>
                    <th class="column-3">Price</th>
                    <th class="column-4 p-l-70">Quantity</th>
                    <th class="column-5">Total</th>
                </tr>

                <tr class="table-row" v-for="item in lineItems" :key="item.product.productId">
                    <td class="column-1">
                        <div class="cart-img-product b-rad-4 o-f-hidden" @click="removeLineItem(item)">
                            <img :src="item.product?.data?.ImageUrl?.value ?? '/images/item-02.jpg'" :alt="item.product?.displayName ?? ''">
                        </div>
                    </td>
                    <td class="column-2">{{item.product.displayName}}</td>
                    <td class="column-3">${{item.product.salesPrice}}</td>
                    <td class="column-4">
                        <div class="flex-w bo5 of-hidden w-size17">
                            <button class="btn-num-product-down color1 flex-c-m size7 bg8 eff2" @click="updateProduct(item, -1)">
                                <i class="fs-12 fa fa-minus" aria-hidden="true"></i>
                            </button>

                            <input class="size8 m-text18 t-center num-product" type="number" name="num-product1" v-model="item.quantity" :disabled="true">

                            <button class="btn-num-product-up color1 flex-c-m size7 bg8 eff2" @click="updateProduct(item, 1)">
                                <i class="fs-12 fa fa-plus" aria-hidden="true"></i>
                            </button>
                        </div>
                    </td>
                    <td class="column-5">${{item.product.salesPrice * item.quantity}}</td>
                </tr>
            </tbody>
        </table>
    </div>
</div>

<div class="bo9 w-size18 p-l-40 p-r-40 p-t-30 p-b-38 m-t-30 m-b-30 m-r-0 m-l-auto p-lr-15-sm">
    <h5 class="m-text20 p-b-24">
        Cart Totals
    </h5>

    <!--  -->
    <div class="flex-w flex-sb-m p-t-26 p-b-30">
        <span class="m-text22 w-size19 w-full-sm">
            Total:
        </span>

        <span class="m-text21 w-size20 w-full-sm">
            ${{ lineItems.reduce((total, i) => total + ((i.product.salesPrice ?? 0) * i.quantity), 0)}}
        </span>
    </div>

    <div class="size15 trans-0-4">
        <!-- Button -->
        <button class="flex-c-m sizefull bg1 bo-rad-23 hov1 s-text1 trans-0-4" @click="completePurchase()">
            Proceed to Checkout
        </button>
    </div>
</div>

<RecommendationProducts v-if="!recommendationsLoading" :recommendations="recommendations" type="purchasedwith" title="Others are also looking at" />

</template>

<script setup lang="ts">
import RecommendationProducts from '../components/RecommendationProducts.vue'
import basketService, { ILineItem } from '@/services/basket.service'
import { computed, ComputedRef, ref, Ref } from 'vue'
import { ProductRecommendationResponse } from '@relewise/client';

const recommendations: Ref<ProductRecommendationResponse|null> = ref(null);

const recommendationsLoading = ref(true)
const lineItems: ComputedRef<ILineItem[]> = computed(() => basketService.model.value.lineItems)

async function updateProduct (item: ILineItem, quantity: number) {
  await basketService.addProduct({ product: item.product, quantityDelta: quantity })
  loadRecommendations();
}
async function removeLineItem (item: ILineItem) {
  await basketService.remove(item)
  loadRecommendations();
}

async function completePurchase () {
  await basketService.clear()
  window.location.href = '/basket/confirmation'
}
//{method: 'POST',headers: {
    // 'Content-Type': 'application/json'
    // }, body: JSON.stringify(basketService.model.value.lineItems.map(l => l.product.productId!)) }
async function loadRecommendations() {
    recommendationsLoading.value = true;
    const params = new URLSearchParams(basketService.model.value.lineItems.map(l => ['ProductIds', l.product.productId]))
    fetch(`/api/catalog/recommend/basket?` + params.toString())
      .then(response => response.json())
      .then(data => {
        console.log(data)
        recommendations.value = data
        recommendationsLoading.value = false;
      })
}

loadRecommendations();
</script>
