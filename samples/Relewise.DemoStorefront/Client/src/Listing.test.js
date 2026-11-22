import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { createApp, nextTick } from 'vue';
import Listing from './Listing.vue';

function fixture(overrides = {}) {
    return {
        path: '/en/store', endpoint: '/en/store/results', suggestionsEndpoint: '/en/store/suggestions',
        collection: null, currency: 'EUR', editorial: false, provider: 'Relewise', parameters: {},
        labels: {
            Filters: 'Filters', FilterAndSort: 'Filter and sort', SortBy: 'Sort by', ClearFilters: 'Clear filters',
            SearchProvider: 'Search provider', Search: 'Search', SearchProducts: 'Search products',
            UpdatingResults: 'Updating results', ResultsUpdated: 'Results updated', TryAgain: 'Try again',
            ListingUpdateFailed: 'Previous results remain displayed. Try again.', PriceRangeInvalid: 'Invalid price range',
            CustomPriceRange: 'Custom price range', MinimumPrice: 'Min', MaximumPrice: 'Max',
            RemoveFilter: 'Remove filter: {0}',
            Recommended: 'Default order', RecommendedRelewise: 'Recommended', NameAscending: 'Name A–Z', NameDescending: 'Name Z–A',
            PriceAscending: 'Price ascending', PriceDescending: 'Price descending', Equipment: 'Equipment'
        },
        facets: [
            { name: 'category', label: 'Categories', choices: [{ value: 'camping', label: 'Camping', count: 1, selected: false }, { value: 'packs', label: 'Packs', count: 0, selected: false }] },
            { name: 'price', label: 'Price', choices: [{ value: 'low', label: 'Under EUR 50', count: 1, selected: false }] },
            { name: 'available', label: 'Availability', choices: [{ value: 'true', label: 'In stock', count: 1, selected: false }] }
        ],
        chips: [], cards: [{ key: 'one', entityId: 'product-one', url: '/en/product/', title: 'Trail pack', summary: 'Outdoor pack', imageHtml: '<svg aria-hidden="true"></svg>', product: true, price: 'EUR 40', availability: 'In stock' }],
        heading: 'Browse', resultLabel: '1 result', rangeLabel: 'Showing 1–1 of 1', showResultsLabel: 'Show 1 product', total: 1, page: 1, lastPage: 1,
        resetUrl: '/en/store', clearQueryUrl: '/en/store', ...overrides
    };
}
let app;
const field = selector => document.querySelector(selector);
const flush = async () => { await new Promise(resolve => setImmediate(resolve)); await nextTick(); };
function mount(initial = fixture()) {
    const root = document.createElement('div');
    document.body.append(root);
    app = createApp(Listing, { initial });
    app.mount(root);
}
async function change(selector, value) {
    const element = field(selector);
    if (element.type === 'checkbox') element.checked = value;
    else element.value = value;
    element.dispatchEvent(new Event('input', { bubbles: true }));
    element.dispatchEvent(new Event('change', { bubbles: true }));
    await nextTick();
}
async function key(name) {
    field('[name=sort]').dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true }));
    await nextTick();
}
beforeEach(() => {
    vi.stubGlobal('matchMedia', () => ({ matches: false, addEventListener: vi.fn(), removeEventListener: vi.fn() }));
    vi.stubGlobal('fetch', vi.fn());
    vi.spyOn(window, 'scrollTo').mockImplementation(() => {});
    HTMLElement.prototype.scrollIntoView = vi.fn();
    history.replaceState({}, '', '/en/store');
});
afterEach(() => {
    app?.unmount();
    document.body.replaceChildren();
    vi.restoreAllMocks();
    vi.unstubAllGlobals();
});
const respond = data => ({ ok: true, json: async () => data });

describe('reactive storefront listing', () => {
    it('counts active filters and returns to already updated results without another request', async () => {
        mount(fixture({ chips: [{ label: 'EUR 25–75', url: '/en/store' }, { label: 'On sale', url: '/en/store' }] }));
        expect(field('.mobile-listing-actions a').textContent).toContain('Filters (2)');
        field('.mobile-listing-actions a').click();
        await nextTick();
        expect(field('#listing-filters').open).toBe(true);
        field('.mobile-show-results').click();
        await nextTick();
        await nextTick();
        expect(field('#listing-filters').open).toBe(false);
        expect(document.activeElement).toBe(field('#listing-results'));
        expect(fetch).not.toHaveBeenCalled();
        expect(field('.listing-count').textContent).toContain('Showing 1–1 of 1');
    });

    it('shows the server-calculated savings on sale cards', () => {
        const initial = fixture();
        initial.cards[0].listPrice = 'EUR 50';
        initial.cards[0].savings = 'Save 20%';
        mount(initial);
        expect(field('.offer-label').textContent).toBe('Save 20%');
        expect(field('del').textContent).toBe('EUR 50');
    });

    it('uses initial server data without another search and disables zero-count options', () => {
        mount();
        expect(fetch).not.toHaveBeenCalled();
        expect(field('[value=packs]').disabled).toBe(true);
        expect(document.body.textContent).toContain('Trail pack');
        expect([...document.querySelectorAll('button')].map(button => button.textContent)).toEqual([]);
    });

    it('applies a checkbox immediately and leaves a selected zero-count option removable', async () => {
        const data = fixture({ parameters: { category: ['camping'] }, total: 0, resultLabel: '0 results', cards: [] });
        data.facets[0].choices[0].count = 0;
        fetch.mockResolvedValue(respond(data));
        mount();
        await change('[value=camping]', true);
        await flush();
        expect(new URL(fetch.mock.calls[0][0]).searchParams.get('category')).toBe('camping');
        expect(field('[value=camping]').checked).toBe(true);
        expect(field('[value=camping]').disabled).toBe(false);
        expect(field('[role=status]').textContent).toContain('0 results');
        expect(location.search).toBe('?category=camping');
    });


    it('combines multiple Brands with other filters and displays the actual card Brand', async () => {
        const initial = fixture();
        initial.facets.unshift({ name: 'brand', label: 'Brands', choices: [
            { value: 'ridgeway', label: 'Ridgeway', count: 1 },
            { value: 'embercamp', label: 'Embercamp', count: 1 }
        ] });
        initial.cards[0].brand = 'Ridgeway';
        fetch.mockImplementation(async url => {
            const data = structuredClone(initial);
            const values = new URL(url).searchParams;
            data.parameters = { brand: values.getAll('brand'), available: values.getAll('available') };
            return respond(data);
        });
        mount(initial);
        await change('[value=ridgeway]', true);
        await flush();
        await change('[value=embercamp]', true);
        await flush();
        await change('[name=available]', true);
        await flush();
        const values = new URL(fetch.mock.calls.at(-1)[0]).searchParams;
        expect(values.getAll('brand')).toEqual(['ridgeway', 'embercamp']);
        expect(values.get('available')).toBe('true');
        expect(field('.card-body small').textContent).toBe('Ridgeway');
    });

    it('combines independent Show only choices and keeps selected zero-count sales removable', async () => {
        const initial = fixture();
        initial.facets[2] = { name: 'showOnly', label: 'Show only', choices: [
            { value: 'true', queryName: 'available', label: 'In stock', count: 1 },
            { value: 'true', queryName: 'onSale', label: 'On sale', count: 1 }
        ] };
        const updated = structuredClone(initial);
        updated.parameters = { available: ['true'], onSale: ['true'] };
        updated.facets[2].choices[1].count = 0;
        fetch.mockResolvedValue(respond(updated));
        mount(initial);
        await change('[name=available]', true);
        await change('[name=onSale]', true);
        await flush();
        const values = new URL(fetch.mock.calls.at(-1)[0]).searchParams;
        expect(values.get('available')).toBe('true');
        expect(values.get('onSale')).toBe('true');
        expect(field('[name=onSale]').checked).toBe(true);
        expect(field('[name=onSale]').disabled).toBe(false);
        expect(field('[name=available]').checked).toBe(true);
    });

    it('waits for complete valid price edits when moving between the bounds', async () => {
        fetch.mockResolvedValue(respond(fixture({ parameters: { minPrice: ['100'], maxPrice: ['150'] } })));
        mount();
        const upper = field('[name=maxPrice]');
        // Native decimal-step validation is exercised in Chrome; isolate our cross-field rule here.
        vi.spyOn(field('form'), 'reportValidity').mockImplementation(() => !upper.validity.customError);
        await change('[name=minPrice]', '100');
        field('[name=minPrice]').dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: upper }));
        expect(fetch).not.toHaveBeenCalled();
        await change('[name=maxPrice]', '50');
        upper.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: document.body }));
        expect(upper.validity.customError).toBe(true);
        expect(fetch).not.toHaveBeenCalled();
        await change('[name=maxPrice]', '150');
        upper.dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: document.body }));
        await flush();
        expect(new URL(fetch.mock.calls[0][0]).searchParams.get('maxPrice')).toBe('150');
    });

    it('clears presets during custom edits and sends one committed range', async () => {
        fetch.mockResolvedValue(respond(fixture({ parameters: { minPrice: ['25'], maxPrice: ['75'] } })));
        mount(fixture({ parameters: { price: ['low'] } }));
        vi.spyOn(field('form'), 'reportValidity').mockImplementation(() => !field('[name=maxPrice]').validity.customError);
        await change('[name=minPrice]', '25');
        await change('[name=maxPrice]', '75');
        expect(field('[value=low]').checked).toBe(false);
        expect(fetch).not.toHaveBeenCalled();
        field('[name=maxPrice]').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
        await flush();
        const values = new URL(fetch.mock.calls[0][0]).searchParams;
        expect(values.get('minPrice')).toBe('25');
        expect(values.get('maxPrice')).toBe('75');
        expect(values.has('price')).toBe(false);
    });

    it('selects a preset after invalid custom edits without sending the invalid range', async () => {
        fetch.mockResolvedValue(respond(fixture({ parameters: { price: ['low'] } })));
        mount();
        vi.spyOn(field('form'), 'reportValidity').mockImplementation(() => !field('[name=maxPrice]').validity.customError);
        await change('[name=minPrice]', '100');
        await change('[name=maxPrice]', '50');
        field('[name=maxPrice]').dispatchEvent(new FocusEvent('focusout', { bubbles: true, relatedTarget: field('[value=low]') }));
        expect(fetch).not.toHaveBeenCalled();
        await change('[value=low]', true);
        await flush();
        expect(field('[name=minPrice]').value).toBe('');
        expect(field('[name=maxPrice]').value).toBe('');
        expect(field('[name=maxPrice]').validity.customError).toBe(false);
        expect(new URL(fetch.mock.calls[0][0]).searchParams.has('minPrice')).toBe(false);
    });

    it('accepts equal custom bounds as an exact price', async () => {
        fetch.mockResolvedValue(respond(fixture({ parameters: { minPrice: ['50'], maxPrice: ['50'] } })));
        mount();
        vi.spyOn(field('form'), 'reportValidity').mockImplementation(() => !field('[name=maxPrice]').validity.customError);
        await change('[name=minPrice]', '50');
        await change('[name=maxPrice]', '50');
        field('[name=maxPrice]').dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
        await flush();
        expect(field('[name=maxPrice]').validity.customError).toBe(false);
        expect(fetch).toHaveBeenCalledTimes(1);
    });

    it('lets keyboard users commit sorting instead of interrupting arrow navigation', async () => {
        fetch.mockResolvedValue(respond(fixture({ parameters: { sort: ['price-desc'] } })));
        mount();
        await key('ArrowDown');
        await change('[name=sort]', 'price-desc');
        expect(fetch).not.toHaveBeenCalled();
        await key('Enter');
        await flush();
        expect(fetch).toHaveBeenCalledTimes(1);
        expect(location.search).toBe('?sort=price-desc');
    });

    it('aborts old requests and ignores stale responses even if a transport still delivers them', async () => {
        const completions = [];
        fetch.mockImplementation(() => new Promise(resolve => completions.push(resolve)));
        mount();
        await change('[value=camping]', true);
        await change('[name=available]', true);
        expect(fetch.mock.calls[0][1].signal.aborted).toBe(true);
        completions[1](respond(fixture({ parameters: { category: ['camping'], available: ['true'] }, resultLabel: 'Latest results' })));
        await flush();
        completions[0](respond(fixture({ resultLabel: 'Stale results' })));
        await flush();
        expect(field('[role=status]').textContent).toContain('Latest results');
        expect(document.body.textContent).not.toContain('Stale results');
        expect(field('[name=available]').checked).toBe(true);
    });

    it('preserves results on failure, hides technical details and allows retry', async () => {
        fetch.mockRejectedValueOnce(new Error('Private upstream detail')).mockResolvedValueOnce(respond(fixture({ parameters: { sort: ['name'] } })));
        mount();
        await change('[name=sort]', 'name');
        await flush();
        expect(document.body.textContent).toContain('Trail pack');
        expect(field('[role=alert]').textContent).toContain('Previous results remain displayed');
        expect(document.body.textContent).not.toContain('Private upstream detail');
        expect(location.search).toBe('');
        field('[role=alert] button').click();
        await flush();
        expect(field('[role=alert]')).toBeNull();
        expect(location.search).toBe('?sort=name');
    });
    it('restores history state without adding another history entry', async () => {
        fetch.mockResolvedValue(respond(fixture({ parameters: { sort: ['name'] } })));
        mount();
        const push = vi.spyOn(history, 'pushState');
        history.replaceState({ fieldworkScroll: { x: 0, y: 140 } }, '', '/en/store?sort=name');
        window.dispatchEvent(new PopStateEvent('popstate', { state: history.state }));
        await flush();
        expect(field('[name=sort]').value).toBe('name');
        expect(push).not.toHaveBeenCalled();
        expect(window.scrollTo).toHaveBeenCalledWith(0, 140);
    });

    it('cancels pending work when unmounted', async () => {
        fetch.mockImplementation(() => new Promise(() => {}));
        mount();
        await change('[name=available]', true);
        app.unmount();
        app = null;
        expect(fetch.mock.calls[0][1].signal.aborted).toBe(true);
    });


});
