import { reactive } from 'vue';

export const shop = reactive({ count: 0, added: null, searchQuery: '', searchScope: 'product' });

export async function sendCart(url, fields) {
    const response = await fetch(url, {
        method: 'POST',
        headers: { Accept: 'application/json' },
        body: new URLSearchParams(fields)
    });
    const data = await response.json();
    if (!response.ok) throw new Error(data.error || 'Cart request failed');
    shop.count = data.cart.count;
    return data;
}
