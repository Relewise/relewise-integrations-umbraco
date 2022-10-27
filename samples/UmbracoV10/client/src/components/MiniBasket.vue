<template>
    <a href="/basket" class="header-wrapicon2">
        <img src="/images/icons/icon-header-02.png" class="header-icon1" alt="Go to checkout">
        <span class="header-icons-noti">{{result}}</span>
    </a>
</template>

<script setup lang="ts">
import basketService from '@/services/basket.service'
import { computed, ComputedRef } from 'vue'

const result: ComputedRef<number> = computed(() => basketService.model.value.lineItems.length)

</script>
