<template>
  <CookieConsent :show-update-consent="true"/>
</template>

<script lang="ts">
import { defineComponent } from 'vue'
import CookieConsent from './components/CookieConsent.vue'

export default defineComponent({
  name: 'UpdateConsent',
  components: {
    CookieConsent
  }
})
</script>
