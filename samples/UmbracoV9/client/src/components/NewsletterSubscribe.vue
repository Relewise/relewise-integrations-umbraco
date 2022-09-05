<template>
<form @submit.prevent="subscribe">
    <div class="effect1 w-size9">
        <input class="s-text7 bg6 w-full p-b-5" type="text" name="email" placeholder="email@example.com" v-model="emailAddress">
        <span class="effect1-line"></span>
    </div>

    <div class="w-size2 p-t-20">
        <!-- Button -->
        <button type="submit" class="flex-c-m size2 bg4 bo-rad-23 hov1 m-text3 trans-0-4">
            Subscribe
        </button>
    </div>

    <div class="m-t-6 success" v-if="subscribed">Welome to our newsletter!</div>
</form>
</template>

<script setup lang="ts">
import { ref } from 'vue'

const emailAddress = ref('')
const subscribed = ref(false)

function subscribe () {
  fetch('/api/newsletter/subscribe?emailAddress=' + emailAddress.value, { method: 'POST' })
    .then(() => {
      subscribed.value = true
      setTimeout(() => { subscribed.value = false }, 3000)
    })
}

</script>

<style scoped>
.success {
  color: #16a34a;
}
</style>
