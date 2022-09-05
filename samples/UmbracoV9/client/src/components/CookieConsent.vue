<template>
  <div v-if="!hasClickedOnCookieBanner || showUpdateConsent">
    <div class="modal-backdrop fade show" v-if="!hasClickedOnCookieBanner"></div>
    <div tabindex="-1" style="display: block;" :class="{ 'modal': !hasClickedOnCookieBanner }">
        <div class="modal-dialog">
          <div class="" :class="{ 'modal-content': !hasClickedOnCookieBanner }">
            <form class="modal-body">
              <img src="/images/icons/logo.svg" alt="Boozy" v-if="!hasClickedOnCookieBanner">
              <p class="text-black m-t-20">Some are used for statistical purposes and others are set up by third party services. By clicking ‘OK’, you accept the use of cookies.
  We and our partners use technologies, including cookies, to collect information about you for various purposes, including:</p>
              <ul class="m-t-10 disc">
                <li>Functionality</li>
                <li>Statistics</li>
                <li>Marketing</li>
              </ul>
              <p class="m-t-10 m-b-10 text-black">By clicking 'Accept All' you consent to all these purposes. You can also choose to indicate what purposes you will consent to using the 'check boxes' explored the object, then press 'UPDATE CONSENT'.
  You can always withdraw your consent by clicking on "Cookie Policy" in the Footer of our website.</p>

              <div class="form-group form-check">
                <input class="form-check-input" type="checkbox" id="functionalCookies" checked="checked" disabled="disabled">
                <label class="form-check-label" for="functionalCookies">
                  Strictly necessary
                </label>
              </div>

              <div class="form-group form-check">
                <input class="form-check-input" type="checkbox" v-model="cookies.functional" id="functionalCookies">
                <label class="form-check-label" for="functionalCookies">
                  Functionality
                </label>
              </div>

              <div class="form-check">
                <input class="form-check-input" type="checkbox" v-model="cookies.marketing" id="marketingCookies">
                <label class="form-check-label" for="marketingCookies">
                  Marketing
                </label>
              </div>

              <div class="form-check">
                <input class="form-check-input" type="checkbox" v-model="cookies.statistical" id="statsCookies">
                <label class="form-check-label" for="statsCookies">
                  Statistics
                </label>
              </div>
            </form>
            <div class="modal-footer">
              <button @click="acceptCookies" class="flex-c-m size2 m-text2 bg3 hov1 trans-0-4" v-if="hasAcceptedAnyCookie">Update consent</button>
              <button @click="rejectCookies" class="flex-c-m size2 m-text2 bg3 hov1 trans-0-4" v-else>Necessary only</button>

              <button @click="acceptAllCookies" class="flex-c-m size1 bg1 bo-rad-20 hov1 s-text1 trans-0-4">Accept All</button>
            </div>
            <div v-if="showUpdateConsent && updatedConsent" class="success">Successfully updated your cookies consent</div>
          </div>
        </div>
      </div>
  </div>
</template>

<script setup lang="ts">
import { ref, toRefs, defineProps } from 'vue'
import { computed } from 'vue'
import cookieConsentService from '../services/cookie-consent.service'

const hasClickedOnCookieBanner = ref(cookieConsentService.hasClickedOnCookieBanner)
const cookies = ref(cookieConsentService.cookies)
const updatedConsent = ref(false)
const hasAcceptedAnyCookie = computed(() => cookies.value.functional || cookies.value.marketing || cookies.value.statistical)

const props = defineProps({
  showUpdateConsent: Boolean
})

const { showUpdateConsent } = toRefs(props)

function acceptAllCookies () {
  cookieConsentService.acceptAllCookies()
  showSuccessMessage()
}
function acceptCookies () {
  cookieConsentService.acceptCookies()
  showSuccessMessage()
}
function rejectCookies () {
  cookieConsentService.rejectCookies()
  showSuccessMessage()
}

function showSuccessMessage () {
  updatedConsent.value = true
  setTimeout(() => { updatedConsent.value = false }, 3000)
}
</script>

<style scoped lang="scss">
.modal-backdrop {
  background-color: #000;
      z-index: 2000;
}
.modal  {
  z-index: 2001;
}
.modal-body {
  padding-left: 25px;
  padding-right: 25px;
}
.form-check-input {
  left: 20px;
}
.success {
  color: rgb(23, 94, 23);
  text-align: center;
}
</style>
