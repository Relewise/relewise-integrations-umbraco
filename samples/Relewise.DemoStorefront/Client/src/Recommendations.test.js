import { afterEach, describe, expect, it, vi } from 'vitest';
import { createApp, nextTick } from 'vue';
import Recommendations from './Recommendations.vue';

let app;
const initial = {
    endpoint: '/en/recommendations/product-key',
    labels: { SimilarProducts: 'Similar products', RecentlyViewedProducts: 'You recently viewed', LoadingRecommendations: 'Loading recommendations', Equipment: 'Equipment', PopularProducts: 'Popular products', PopularCategories: 'Popular categories' }
};
const card = id => ({ key: id, entityId: id, url: '/en/' + id, title: id, product: true, imageHtml: '<svg></svg>', price: 'EUR 40', availability: 'In stock' });
const flush = async () => { await new Promise(resolve => setImmediate(resolve)); await nextTick(); };
function mount() {
    const root = document.createElement('div');
    document.body.append(root);
    app = createApp(Recommendations, { initial });
    app.mount(root);
}
afterEach(() => {
    app?.unmount();
    app = null;
    document.body.innerHTML = '';
    vi.unstubAllGlobals();
});
describe('Product recommendations', () => {
    it('keeps the two purposes separate, reuses cards and preserves recommendation order', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ similar: [card('second'), card('first')], recentlyViewed: [card('viewed')] }) }));
        mount();
        await flush();
        expect([...document.querySelectorAll('h2')].map(node => node.textContent)).toEqual(['Similar products', 'You recently viewed']);
        expect([...document.querySelectorAll('.card')].map(node => node.dataset.entity)).toEqual(['second', 'first', 'viewed']);
        expect(document.body.textContent).toContain('EUR 40');
        expect(fetch.mock.calls[0][0]).toBe(initial.endpoint);
        expect(fetch.mock.calls[0][1].cache).toBe('no-store');
    });
    it('does not invent history or render empty headings', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({ similar: [card('alternative')], recentlyViewed: [] }) }));
        mount();
        await flush();
        expect(document.querySelectorAll('h2')).toHaveLength(1);
        expect(document.body.textContent).not.toContain('You recently viewed');
    });
    it.each(['empty', 'unavailable', 'network'])('hides optional sections when %s without an error banner', async state => {
        vi.stubGlobal('fetch', vi.fn().mockImplementation(async () => {
            if (state === 'network') throw new TypeError('Unavailable');
            return { ok: state !== 'unavailable', json: async () => ({ similar: [], recentlyViewed: [] }) };
        }));
        mount();
        await flush();
        expect(document.querySelectorAll('h2')).toHaveLength(0);
        expect(document.querySelector('[role=alert]')).toBeNull();
        expect(document.querySelector('[role=status]')).toBeNull();
    });
    it('cancels outstanding requests when navigating away', async () => {
        vi.stubGlobal('fetch', vi.fn().mockImplementation(() => new Promise(() => {})));
        mount();
        const signal = fetch.mock.calls[0][1].signal;
        app.unmount();
        app = null;
        expect(signal.aborted).toBe(true);
    });
    it('renders popularity separately from history and links categories to their landing pages', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({
            similar: [], recentlyViewed: [card('viewed')], popular: [card('popular')],
            categories: [{ key: 'packs', entityId: 'category-packs', title: 'Packs and bags', url: '/en/packs/', imageHtml: '<svg></svg>' }]
        }) }));
        mount();
        await flush();
        expect([...document.querySelectorAll('h2')].map(node => node.textContent)).toEqual(['Popular products', 'Popular categories', 'You recently viewed']);
        expect(document.querySelector('.category-recommendation a').getAttribute('href')).toBe('/en/packs/');
        expect(document.querySelector('.category-recommendation').dataset.entity).toBe('category-packs');
    });

    it('shows a clearly labelled popular fallback without inventing viewing history', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({
            similar: [], recentlyViewed: [], popular: [card('popular')], categories: []
        }) }));
        mount();
        await flush();
        expect([...document.querySelectorAll('h2')].map(node => node.textContent)).toEqual(['Popular products']);
    });

    it('hides all recommendation sections for a fresh Dataset with no activity', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => ({
            similar: [], recentlyViewed: [], popular: [], categories: []
        }) }));
        mount();
        await flush();
        expect(document.querySelector('section')).toBeNull();
    });

    it.each([null, { similar: null, recentlyViewed: {}, popular: [null], categories: 'invalid' }])('ignores malformed optional sections without breaking the page', async data => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => data }));
        mount();
        await flush();
        expect(document.querySelector('section')).toBeNull();
        expect(document.querySelector('[role=alert]')).toBeNull();
        expect(document.querySelector('[role=status]')).toBeNull();
    });

    it('ignores a response that finishes parsing after the component was unmounted', async () => {
        let finishJson;
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: () => new Promise(resolve => { finishJson = resolve; }) }));
        mount();
        await flush();
        app.unmount();
        app = null;
        finishJson({ popular: [card('late')] });
        await flush();
        expect(document.querySelector('section')).toBeNull();
    });

    it('hides recommendations if the response is not valid JSON', async () => {
        vi.stubGlobal('fetch', vi.fn().mockResolvedValue({ ok: true, json: async () => { throw new SyntaxError('Invalid JSON'); } }));
        mount();
        await flush();
        expect(document.querySelector('section')).toBeNull();
        expect(document.querySelector('[role=status]')).toBeNull();
    });

});
