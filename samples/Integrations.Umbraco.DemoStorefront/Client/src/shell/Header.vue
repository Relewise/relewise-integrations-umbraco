<script setup>
import { nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import SearchBox from './SearchBox.vue';
import { shop } from './shopState.js';

const props = defineProps({ initial: { type: Object, required: true } });
shop.count = props.initial.count;
shop.searchQuery = props.initial.query;
shop.searchScope = props.initial.searchScope;
const menu = ref(false);
const menuButton = ref();
const navigation = ref();
const market = ref();
const confirmation = ref();
const returnUrl = ref('');
let opener;
async function toggleMenu() {
    menu.value = !menu.value;
    await nextTick();
    if (menu.value) navigation.value.querySelector('a')?.focus();
    else menuButton.value?.focus();
}
function escapeMenu() {
    if (menu.value) {
        menu.value = false;
        menuButton.value?.focus();
    }
}
onMounted(() => {
    if (!props.initial.checkout) window.addEventListener('fieldwork:choose-market', chooseMarket);
});
onUnmounted(() => window.removeEventListener('fieldwork:choose-market', chooseMarket));
function chooseMarket() {
    returnUrl.value = location.pathname + location.search;
    market.value.showModal();
}
watch(() => shop.added, async value => {
    if (!value) return;
    opener = document.querySelector('#purchase-controls button') ?? document.activeElement;
    await nextTick();
    if (!confirmation.value.open) confirmation.value.showModal();
});
function closeConfirmation() {
    confirmation.value.close();
    shop.added = null;
    opener?.focus({ preventScroll: true });
}
</script>

<template>
    <header @keydown.esc="escapeMenu" class="site-header" :class="{ 'checkout-header': initial.checkout }">
        <div class="header-main">
            <button v-if="!initial.checkout" ref="menuButton" class="menu-toggle icon-button" :aria-label="initial.labels.Menu" :aria-expanded="menu" aria-controls="primary-navigation" @click="toggleMenu"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 6h18M3 12h18M3 18h18"/></svg></button>
            <a :href="initial.homeUrl" class="logo">FIELDWORK<span>{{ initial.labels.OutdoorGoods }}</span></a>
            <SearchBox v-if="!initial.checkout" :initial="initial" />
            <div v-if="!initial.checkout" class="header-actions">
                <button class="market-trigger" aria-haspopup="dialog" aria-controls="market-dialog" @click="chooseMarket"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"/><ellipse cx="12" cy="12" rx="4" ry="9"/><path d="M3 12h18"/></svg>{{ initial.marketLabel }}<span class="market-chevron" aria-hidden="true" /></button>
                <a v-if="initial.shoppingConfigured" :href="initial.cartUrl" class="header-cart" :aria-label="`${initial.labels.Cart} (${shop.count})`"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 7h14l1 14H4L5 7Zm3 0V5a4 4 0 0 1 8 0v2"/></svg><span>{{ initial.labels.Cart }}</span><strong :key="shop.count" class="cart-count">{{ shop.count }}</strong></a>
                <span v-else class="header-cart" aria-disabled="true" :title="initial.labels.ShoppingDisabled">{{ initial.labels.Cart }}</span>
            </div>
            <a v-else class="text-link checkout-return" :href="initial.cartUrl">← {{ initial.labels.EditCart }}</a>
        </div>
        <nav v-if="!initial.checkout" id="primary-navigation" ref="navigation" class="primary-navigation" :class="{ 'is-open': menu }" :aria-label="initial.labels.MainNavigation">
            <template v-for="link in initial.links" :key="link.url">
                <a v-if="link.enabled !== false" :href="link.url" :aria-current="link.active ? 'page' : undefined">{{ link.label }}</a>
                <span v-else class="navigation-disabled" aria-disabled="true" :title="initial.labels.SearchDisabled">{{ link.label }}</span>
            </template>
            <button class="mobile-market" aria-haspopup="dialog" aria-controls="market-dialog" @click="chooseMarket">{{ initial.marketLabel }} <span class="market-chevron" aria-hidden="true" /></button>
        </nav>
    </header>
    <dialog v-if="!initial.checkout" id="market-dialog" ref="market" class="market-dialog" aria-labelledby="market-title">
        <button class="dialog-close icon-button" :aria-label="initial.labels.Close" @click="market.close()">×</button>
        <h2 id="market-title">{{ initial.labels.ChooseMarket }}</h2>
        <form :action="initial.marketUrl" class="market-form">
            <input type="hidden" name="returnUrl" :value="returnUrl">
            <label v-for="choice in initial.markets" :key="choice.id"><input type="radio" name="market" :value="choice.id" :checked="choice.id === initial.market">{{ choice.label }}</label>
            <button>{{ initial.labels.Apply }}</button>
        </form>
    </dialog>
    <dialog ref="confirmation" class="cart-confirmation" aria-labelledby="added-title" @cancel.prevent="closeConfirmation">
        <button class="dialog-close icon-button" :aria-label="initial.labels.Close" @click="closeConfirmation">×</button>
        <template v-if="shop.added">
            <h2 id="added-title">{{ initial.labels.AddedToCart }}</h2>
            <div class="added-item"><div class="illustration" v-html="shop.added.item.imageHtml"/><div><strong>{{ shop.added.item.name }}</strong><p>{{ initial.labels.Quantity }}: {{ shop.added.item.quantity }}</p><strong>{{ shop.added.item.lineTotal }}</strong></div></div>
            <div class="cart-total"><span>{{ initial.labels.Total }}</span><strong>{{ shop.added.cart.total }}</strong></div>
            <a class="button" :href="shop.added.cart.cartUrl">{{ initial.labels.ViewCart }}</a>
            <a v-if="shop.added.cart.ready" class="button secondary" :href="shop.added.cart.checkoutUrl">{{ initial.labels.Checkout }}</a>
            <button class="text-button" @click="closeConfirmation">{{ initial.labels.ContinueShopping }}</button>
        </template>
    </dialog>
</template>
