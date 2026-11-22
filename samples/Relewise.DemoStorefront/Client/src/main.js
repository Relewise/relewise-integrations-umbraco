import { createApp } from 'vue';
import Header from './Header.vue';
import Listing from './Listing.vue';
import Purchase from './Purchase.vue';
import Cart from './Cart.vue';

for (const [rootId, dataId, component] of [
    ['storefront-shell', 'shell-data', Header],
    ['storefront-listing', 'listing-data', Listing],
    ['purchase-controls', 'purchase-data', Purchase],
    ['cart-page', 'cart-data', Cart]
]) {
    const root = document.getElementById(rootId);
    if (root) createApp(component, { initial: JSON.parse(document.getElementById(dataId).textContent) }).mount(root);
}
// Native order submission retains the server's checkout token and redirects to its receipt.
const checkout = document.querySelector('.checkout-summary form');
checkout?.addEventListener('submit', () => { checkout.querySelector('button').disabled = true; });
