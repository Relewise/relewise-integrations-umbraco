import { createApp } from 'vue';
import Header from './shell/Header.vue';
import Footer from './shell/Footer.vue';
import Listing from './browsing/Listing.vue';
import Purchase from './cart/Purchase.vue';
import Cart from './cart/Cart.vue';
import Recommendations from './recommendations/Recommendations.vue';

for (const [rootId, dataId, component] of [
    ['storefront-shell', 'shell-data', Header],
    ['storefront-footer', 'shell-data', Footer],
    ['storefront-listing', 'listing-data', Listing],
    ['purchase-controls', 'purchase-data', Purchase],
    ['cart-page', 'cart-data', Cart],
    ['product-recommendations', 'recommendation-data', Recommendations]
]) {
    const root = document.getElementById(rootId);
    if (root) createApp(component, { initial: JSON.parse(document.getElementById(dataId).textContent) }).mount(root);
}
// Native order submission retains the server's checkout token and redirects to its receipt.
const checkout = document.querySelector('.checkout-summary form');
checkout?.addEventListener('submit', () => { checkout.querySelector('button').disabled = true; });
