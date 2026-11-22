import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createApp, nextTick } from 'vue';
import Header from './Header.vue';
import Purchase from './Purchase.vue';
import Cart from './Cart.vue';
import SearchBox from './SearchBox.vue';
import { shop } from './shopState';

const labels = Object.fromEntries(['Cart', 'Search', 'SearchProducts', 'SearchJournal', 'Close', 'Checkout', 'ViewCart', 'ContinueShopping', 'Quantity', 'Total', 'AddCart', 'AddingToCart', 'PurchaseFailed', 'CartUpdated', 'CartUpdating', 'CartEmpty', 'CartItems', 'OrderSummary', 'RemoveCart', 'EmptyCartAction', 'EmptyCartConfirm', 'Cancel', 'Menu', 'ChooseMarket', 'Apply', 'AddedToCart', 'EditCart'].map(key => [key, key]));
const item = { productKey: '11111111-1111-1111-1111-111111111111', variantKey: '22222222-2222-2222-2222-222222222222', productId: 'PRODUCT', variantId: 'FOREST', name: 'Trail flask Forest', quantity: 1, maximum: 5, unitPrice: 'EUR 34.95', lineTotal: 'EUR 34.95', imageHtml: '<svg></svg>', url: '/en/flask/forest/', available: true };
const basket = (count = 1) => ({ labels, lines: count ? [{ ...item, quantity: count }] : [], count, total: 'EUR ' + (count * 34.95).toFixed(2), ready: count > 0, cartUrl: '/en/cart', checkoutUrl: '/en/checkout', continueUrl: '/en/store', antiforgeryField: '__RequestVerificationToken', antiforgeryToken: 'test-token' });
const shell = (checkout = false) => ({ labels, count: 0, checkout, searchConfigured: true, shoppingConfigured: true, query: '', searchScope: 'product', searchPage: false, homeUrl: '/en/', cartUrl: '/en/cart', searchUrl: '/en/search', suggestionsUrl: '/en/store/suggestions', marketUrl: '/store/market', market: 'eu', marketLabel: 'Europe · EUR', markets: [{ id: 'eu', label: 'Europe' }], links: [{ url: '/en/store', label: 'Shop', active: false }, { url: '/en/offers', label: 'Offers', active: true }] });
const purchase = () => ({ labels, url: '/en/cart/add', maximum: 5, fields: { product: item.productKey, variant: item.variantKey, __RequestVerificationToken: 'test-token' } });
const response = (data, ok = true) => ({ ok, json: async () => data });
const apps = [];
function mount(component, initial) {
    const root = document.createElement('div');
    document.body.append(root);
    const app = createApp(component, { initial });
    apps.push(app);
    app.mount(root);
    return root;
}
const flush = async () => { await new Promise(resolve => setImmediate(resolve)); await nextTick(); };
beforeEach(() => {
    Object.assign(shop, { count: 0, added: null, searchQuery: '', searchScope: 'product' });
    vi.stubGlobal('fetch', vi.fn());
    HTMLDialogElement.prototype.showModal = function () { this.setAttribute('open', ''); };
    HTMLDialogElement.prototype.close = function () { this.removeAttribute('open'); };
    history.replaceState({}, '', '/en/store');
});
afterEach(() => {
    for (const app of apps.splice(0)) app.unmount();
    document.body.replaceChildren();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
});

describe('shopping shell and cart interactions', () => {
    it('disables search and shopping in content preview without sending requests', async () => {
        const initial = shell();
        initial.searchConfigured = initial.shoppingConfigured = false;
        initial.labels.SearchDisabled = 'Search not enabled';
        initial.links[1].enabled = false;
        mount(Header, initial);
        expect(document.querySelector('#global-query').disabled).toBe(true);
        expect(document.querySelector('.global-search button').disabled).toBe(true);
        expect(document.querySelector('a.header-cart')).toBeNull();
        expect(document.querySelector('.header-cart').getAttribute('aria-disabled')).toBe('true');
        expect(document.querySelector('a[href="/en/offers"]')).toBeNull();
        expect(document.querySelector('.navigation-disabled').textContent).toBe('Offers');
        document.querySelector('.global-search').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        await flush();
        expect(fetch).not.toHaveBeenCalled();
    });
    it('has one global search, separate navigation, and only the actual Offers link active', () => {
        mount(Header, shell());
        expect(document.querySelectorAll('[role=search]')).toHaveLength(1);
        expect(document.querySelectorAll('.primary-navigation [aria-current=page]')).toHaveLength(1);
        expect(document.querySelector('.primary-navigation [aria-current=page]').textContent).toBe('Offers');
        expect(document.querySelector('.header-main .primary-navigation')).toBeNull();
    });

    it('removes shopping distractions during checkout', () => {
        mount(Header, shell(true));
        expect(document.querySelector('[role=search]')).toBeNull();
        expect(document.querySelector('.primary-navigation')).toBeNull();
        expect(document.querySelector('.market-trigger')).toBeNull();
        expect(document.querySelector('.checkout-return').getAttribute('href')).toBe('/en/cart');
    });

    it('submits a global search without carrying category, Offers or price restrictions', async () => {
        history.replaceState({}, '', '/en/offers?category=camping&maxPrice=50');
        const assign = vi.spyOn(location, 'assign').mockImplementation(() => {});
        shop.searchQuery = 'flask';
        mount(SearchBox, shell());
        document.querySelector('form').dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        await flush();
        const url = new URL(assign.mock.calls[0][0]);
        expect(url.pathname).toBe('/en/search');
        expect([...url.searchParams.keys()]).toEqual(['q']);
        expect(url.searchParams.get('q')).toBe('flask');
    });

    it('sends canonical node keys and CSRF, prevents duplicate clicks, updates the count and opens confirmation', async () => {
        let complete;
        fetch.mockImplementation(() => new Promise(resolve => { complete = resolve; }));
        mount(Header, shell());
        mount(Purchase, purchase());
        const form = document.querySelector('.add-cart');
        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        expect(fetch).toHaveBeenCalledTimes(1);
        const fields = fetch.mock.calls[0][1].body;
        expect(fields.get('product')).toBe(item.productKey);
        expect(fields.get('variant')).toBe(item.variantKey);
        expect(fields.get('__RequestVerificationToken')).toBe('test-token');
        expect(fields.get('commandId')).toMatch(/^[0-9a-f-]{36}$/);
        complete(response({ cart: basket(), added: item }));
        await flush();
        expect(document.querySelector('.header-cart').getAttribute('aria-label')).toBe('Cart (1)');
        expect(document.querySelector('.cart-confirmation').open).toBe(true);
        expect(document.querySelector('.cart-confirmation').textContent).toContain('Trail flask Forest');
    });

    it('retains the same add command when retrying an ambiguous network failure', async () => {
        fetch.mockRejectedValueOnce(new TypeError('Network failure')).mockResolvedValueOnce(response({ cart: basket(), added: item }));
        mount(Purchase, purchase());
        const form = document.querySelector('.add-cart');
        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        await flush();
        expect(document.querySelector('[role=alert]').textContent).toBe('PurchaseFailed');
        form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }));
        await flush();
        expect(fetch.mock.calls[0][1].body.get('commandId')).toBe(fetch.mock.calls[1][1].body.get('commandId'));
        expect(shop.count).toBe(1);
    });

    it('updates quantities from the server result and restores the old quantity after rejection', async () => {
        fetch.mockResolvedValueOnce(response({ cart: basket(3) })).mockResolvedValueOnce(response({ error: 'Only three available' }, false));
        mount(Cart, basket());
        let quantity = document.querySelector('input[type=number]');
        quantity.value = '3';
        quantity.dispatchEvent(new Event('change', { bubbles: true }));
        await flush();
        expect(shop.count).toBe(3);
        quantity = document.querySelector('input[type=number]');
        quantity.value = '4';
        quantity.dispatchEvent(new Event('change', { bubbles: true }));
        await flush();
        expect(document.querySelector('[role=alert]').textContent).toBe('Only three available');
        expect(quantity.value).toBe('3');
        expect(shop.count).toBe(3);
    });

    it('requires confirmation before emptying and updates the empty state', async () => {
        fetch.mockResolvedValue(response({ cart: basket(0) }));
        mount(Cart, basket());
        document.querySelector('.empty-cart').click();
        await nextTick();
        expect(fetch).not.toHaveBeenCalled();
        expect(document.querySelector('dialog').open).toBe(true);
        const buttons = document.querySelectorAll('dialog button');
        buttons[0].click();
        expect(fetch).not.toHaveBeenCalled();
        document.querySelector('.empty-cart').click();
        buttons[1].click();
        await flush();
        expect(shop.count).toBe(0);
        expect(document.querySelector('.cart-layout')).toBeNull();
        expect(document.querySelector('.empty-cart-page').textContent).toContain('CartEmpty');
        expect(document.querySelector('dialog').open).toBe(false);
    });
    it('validates a draft quantity before checkout and saves valid edits before navigating', async () => {
        const assign = vi.spyOn(location, 'assign').mockImplementation(() => {});
        fetch.mockResolvedValue(response({ cart: basket(2) }));
        mount(Cart, basket());
        const quantity = document.querySelector('input[type=number]');
        const checkout = document.querySelector('.basket-summary a');
        quantity.value = '99';
        checkout.click();
        await flush();
        expect(assign).not.toHaveBeenCalled();
        expect(fetch).not.toHaveBeenCalled();
        quantity.value = '2';
        checkout.click();
        await flush();
        expect(fetch.mock.calls[0][1].body.get('quantity')).toBe('2');
        expect(assign).toHaveBeenCalledWith('/en/checkout');
    });

    it('moves focus into mobile navigation and returns it on Escape', async () => {
        mount(Header, shell());
        const button = document.querySelector('.menu-toggle');
        button.click();
        await nextTick();
        expect(button.getAttribute('aria-expanded')).toBe('true');
        expect(document.activeElement).toBe(document.querySelector('.primary-navigation a'));
        document.activeElement.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
        await nextTick();
        expect(button.getAttribute('aria-expanded')).toBe('false');
        expect(document.activeElement).toBe(button);
    });

});
