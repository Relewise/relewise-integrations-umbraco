<script setup>
import { onMounted, onUnmounted, ref } from 'vue';
import FooterGroup from './FooterGroup.vue';

const props = defineProps({ initial: { type: Object, required: true } });
const information = ref();
const mobile = ref(false);
let narrow;
const resize = () => { mobile.value = narrow.matches; };
onMounted(() => {
    narrow = matchMedia('(max-width: 680px)');
    resize();
    narrow.addEventListener('change', resize);
});
onUnmounted(() => narrow?.removeEventListener('change', resize));
const logoUrl = '/images/relewise-logo-blue.svg';
const guide = 'https://github.com/Relewise/relewise-integrations-umbraco/tree/main/samples/Integrations.Umbraco.DemoStorefront';
const searchGuide = 'https://github.com/Relewise/relewise-integrations-umbraco/blob/main/docs/search.md';
function chooseMarket() {
    window.dispatchEvent(new CustomEvent('fieldwork:choose-market'));
}
</script>

<template>
    <footer class="site-footer" :class="{ 'checkout-footer': initial.checkout }">
        <div v-if="!initial.checkout" class="footer-top">
            <section class="footer-introduction">
                <a :href="initial.homeUrl" class="logo">FIELDWORK<span>{{ initial.labels.OutdoorGoods }}</span></a>
                <p>{{ initial.labels.FooterIntroduction }}</p>
                <button class="footer-market" :aria-label="`${initial.labels.ChangeMarket}: ${initial.marketLabel}`" aria-haspopup="dialog" aria-controls="market-dialog" @click="chooseMarket">
                    <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"/><ellipse cx="12" cy="12" rx="4" ry="9"/><path d="M3 12h18"/></svg>
                    {{ initial.marketLabel }}<span class="market-chevron" aria-hidden="true" />
                </button>
            </section>
            <nav class="footer-navigation" :aria-label="initial.labels.FooterNavigation">
                <FooterGroup :title="initial.labels.Shop" :links="initial.shopLinks" :collapsible="mobile" />
                <div class="footer-discovery">
                    <FooterGroup v-if="initial.brandLinks.length" :title="initial.labels.OurBrands" :links="initial.brandLinks" :collapsible="mobile" />
                    <FooterGroup :title="initial.labels.Explore" :links="[{ label: initial.labels.Journal, url: initial.journalUrl }]" :collapsible="mobile" />
                </div>
            </nav>
        </div>
        <div class="footer-demo" lang="en">
            <div class="footer-powered-by">
                <a href="https://www.relewise.com/" target="_blank" rel="noopener noreferrer"><img :src="logoUrl" alt="Relewise" width="162" height="23"></a>
                <p v-if="!initial.checkout">{{ initial.shoppingConfigured ? (initial.searchConfigured ? 'Demo storefront with search and recommendations powered by Relewise.' : 'Demo storefront with recommendations powered by Relewise.') : 'A Relewise demo storefront. Connect Relewise to explore search and recommendations.' }}</p>
            </div>
            <button class="demo-information-trigger" aria-haspopup="dialog" aria-controls="demo-information-dialog" @click="information.showModal()">
                <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M12 11v6M12 7v1"/></svg>Demo information
            </button>
            <p class="footer-demo-notice">{{ initial.checkout ? 'Demo checkout — no payment is taken or order fulfilled.' : 'Demo only. Products and brands are fictional. Checkout is simulated; no payment is taken or order fulfilled.' }}</p>
        </div>
    </footer>
    <dialog id="demo-information-dialog" lang="en" ref="information" class="demo-information-dialog" aria-labelledby="demo-information-title">
        <button class="dialog-close icon-button" aria-label="Close" @click="information.close()">×</button>
        <img class="demo-information-logo" :src="logoUrl" alt="Relewise" width="162" height="23">
        <h2 id="demo-information-title">Demo information</h2>
        <p>Demo only. Products and brands are fictional. Checkout is simulated; no payment is taken or order fulfilled.</p>
        <dl class="demo-configuration">
            <dt>Search provider</dt><dd>{{ initial.searchConfigured ? 'Relewise' : 'Not enabled' }}</dd>
            <dt>Recommendations</dt><dd>{{ initial.shoppingConfigured ? 'Relewise' : 'Not enabled' }}</dd>
            <dt>Behavioral tracking</dt><dd>{{ initial.shoppingConfigured ? 'Enabled' : 'Not enabled' }}</dd>
            <dt>Dataset ID</dt><dd>{{ initial.datasetId || 'Not configured' }}</dd>
        </dl>
        <nav class="demo-information-links" aria-label="Demo information">
            <a :href="guide" target="_blank" rel="noopener noreferrer">Demo storefront guide ↗</a>
            <a :href="`${guide}#connect-to-relewise`" target="_blank" rel="noopener noreferrer">Configure Relewise ↗</a>
            <a :href="searchGuide" target="_blank" rel="noopener noreferrer">Search provider guide ↗</a>
            <a v-if="initial.datasetId" :href="`https://my.relewise.com/${initial.datasetId}`" target="_blank" rel="noopener noreferrer">Open My Relewise ↗</a>
        </nav>
    </dialog>
</template>
