<script setup>
import { computed, nextTick, onMounted, onUnmounted, reactive, ref, shallowRef } from 'vue';
import ListingCard from './ListingCard.vue';
import { shop } from './shopState';

const props = defineProps({ initial: { type: Object, required: true } });
const model = shallowRef(props.initial);
const labels = props.initial.labels;
const form = ref();
const budget = ref();
const results = ref();
const panel = ref();
const setBudget = element => { budget.value = element; };
const narrow = window.matchMedia('(max-width: 850px)');
const filtersOpen = ref(!narrow.matches);
const choices = reactive({ category: [], brand: [], price: [], available: [], onSale: [] });
const query = ref('');
const sort = ref('');
const minimum = ref('');
const maximum = ref('');
const busy = ref(false);
const error = ref(false);
const announcement = ref('');
let pending;
let revision = 0;
let retryUrl;
let budgetDirty = false;
let keyboardSort = false;
let pendingSort = false;
let originalSort = '';

function synchronize(data, keepQuery = false, keepBudget = false) {
    for (const name of Object.keys(choices)) choices[name] = [...(data.parameters[name] ?? [])];
    if (!keepQuery) query.value = data.parameters.q?.[0] ?? '';
    sort.value = data.parameters.sort?.[0] ?? '';
    originalSort = sort.value;
    if (!keepBudget) {
        minimum.value = data.parameters.minPrice?.[0] ?? '';
        maximum.value = data.parameters.maxPrice?.[0] ?? '';
        budgetDirty = false;
    }
    if (keepBudget && budgetDirty) choices.price = [];
    if (data.searchPage) {
        shop.searchQuery = query.value;
        shop.searchScope = data.editorial ? 'article' : 'product';
    }
    keyboardSort = pendingSort = false;
}
synchronize(props.initial);

const sorts = computed(() => [
    ['', 'RecommendedRelewise'], ['name', 'NameAscending'], ['name-desc', 'NameDescending'],
    ...(!model.value.editorial ? [['price-asc', 'PriceAscending'], ['price-desc', 'PriceDescending']] : [])
]);
const activeFilterCount = computed(() => model.value.chips.length);
function parameters() {
    const values = new URLSearchParams();
    for (const name of ['type', 'provider']) {
        for (const value of model.value.parameters[name] ?? []) values.append(name, value);
    }
    if (query.value) values.set('q', query.value);
    if (sort.value) values.set('sort', sort.value);
    for (const [name, valuesForName] of Object.entries(choices)) {
        for (const value of valuesForName) values.append(name, value);
    }
    if (minimum.value !== '') values.set('minPrice', minimum.value);
    if (maximum.value !== '') values.set('maxPrice', maximum.value);
    return values;
}
function requestUrl() {
    const url = new URL(model.value.path, location.origin);
    url.search = parameters().toString();
    return url;
}
function canonicalUrl(data) {
    const url = new URL(data.path, location.origin);
    const values = new URLSearchParams();
    for (const [name, items] of Object.entries(data.parameters)) {
        for (const value of items) values.append(name, value);
    }
    url.search = values.toString();
    return url;
}
async function load(target, { historyMode = 'push', scrollResults = false, scroll } = {}) {
    pending?.abort();
    const controller = new AbortController();
    pending = controller;
    const version = ++revision;
    const url = new URL(target, location.origin);
    retryUrl = url;
    const endpoint = new URL(model.value.endpoint, location.origin);
    endpoint.search = url.search;
    if (props.initial.collection) endpoint.searchParams.set('collection', props.initial.collection);
    endpoint.searchParams.set('view', props.initial.view ?? 'store');
    const position = scroll ?? { x: window.scrollX, y: window.scrollY };
    const previousFocus = document.activeElement;
    const sentQuery = query.value;
    const sentMinimum = minimum.value;
    const sentMaximum = maximum.value;
    busy.value = true;
    error.value = false;
    announcement.value = labels.UpdatingResults;
    try {
        const response = await fetch(endpoint, { signal: controller.signal, headers: { Accept: 'application/json' } });
        if (!response.ok) throw new Error('Listing request failed');
        const data = await response.json();
        if (version !== revision) return;
        model.value = data;
        synchronize(data, historyMode !== 'none' && query.value !== sentQuery,
            historyMode !== 'none' && (minimum.value !== sentMinimum || maximum.value !== sentMaximum));
        if (historyMode === 'push') {
            history.replaceState({ ...history.state, fieldworkScroll: { x: window.scrollX, y: window.scrollY } }, '');
            history.pushState({ fieldworkScroll: position }, '', canonicalUrl(data));
        }
        await nextTick();
        if (!previousFocus.isConnected) results.value?.querySelector('h2')?.focus({ preventScroll: true });
        if (scrollResults) results.value?.scrollIntoView({ block: 'start' });
        else window.scrollTo(position.x, position.y);
        announcement.value = labels.ResultsUpdated + ': ' + data.resultLabel;
    } catch (failure) {
        if (controller.signal.aborted || version !== revision) return;
        error.value = true;
        announcement.value = labels.ListingUpdateFailed;
    } finally {
        if (version === revision) {
            busy.value = false;
            pending = null;
        }
    }
}
function validate() {
    const upper = budget.value?.querySelector('[name=maxPrice]');
    if (upper) upper.setCustomValidity(minimum.value !== '' && maximum.value !== '' && Number(minimum.value) > Number(maximum.value) ? labels.PriceRangeInvalid : '');
    return form.value.reportValidity();
}
function apply(scrollResults = false) {
    if (validate()) load(requestUrl(), { scrollResults });
}
function toggle(name) {
    if (name === 'price') {
        minimum.value = maximum.value = '';
        budgetDirty = false;
    }
    apply();
}
function editBudget() {
    budgetDirty = true;
    choices.price = [];
    const upper = budget.value.querySelector('[name=maxPrice]');
    upper.setCustomValidity(minimum.value !== '' && maximum.value !== '' && Number(minimum.value) > Number(maximum.value) ? labels.PriceRangeInvalid : '');
}
function leaveBudget(event) {
    if (event.relatedTarget?.name === 'price') return;
    if (budgetDirty && !budget.value.contains(event.relatedTarget)) apply();
}
function sortKey(event) {
    if (['ArrowDown', 'ArrowUp', 'Home', 'End', 'PageUp', 'PageDown'].includes(event.key)) keyboardSort = true;
    if (event.key === 'Escape') {
        sort.value = originalSort;
        keyboardSort = pendingSort = false;
    }
    if (event.key === 'Enter') {
        event.preventDefault();
        keyboardSort = pendingSort = false;
        apply();
    }
}
function changeSort() {
    pendingSort = keyboardSort;
    if (!keyboardSort) apply();
}
function leaveSort() {
    if (pendingSort) {
        keyboardSort = pendingSort = false;
        apply();
    }
}
function follow(event, url, scrollResults = false) {
    if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    load(url, { scrollResults });
}
async function openFilters(event) {
    event.preventDefault();
    filtersOpen.value = true;
    await nextTick();
    panel.value?.scrollIntoView({ block: 'start' });
}
async function showResults(event) {
    event.preventDefault();
    if (!validate() || busy.value) return;
    filtersOpen.value = false;
    await nextTick();
    results.value?.scrollIntoView({ block: 'start' });
    results.value?.focus({ preventScroll: true });
}
const resize = () => { filtersOpen.value = !narrow.matches; };
const popstate = event => load(location.href, { historyMode: 'none', scroll: event.state?.fieldworkScroll });
function searchRequest(event) {
    if (!props.initial.searchPage) return;
    const url = new URL(props.initial.path, location.origin);
    if (event.detail.query) url.searchParams.set('q', event.detail.query);
    if (event.detail.scope === 'article') url.searchParams.set('type', 'article');
    load(url, { scrollResults: true });
}
function scopeUrl(type) {
    const url = new URL(model.value.path, location.origin);
    if (query.value) url.searchParams.set('q', query.value);
    if (type === 'article') url.searchParams.set('type', 'article');
    return url.pathname + url.search;
}
onMounted(() => {
    narrow.addEventListener('change', resize);
    window.addEventListener('popstate', popstate);
    window.addEventListener('fieldwork:search', searchRequest);
});
onUnmounted(() => {
    pending?.abort();
    narrow.removeEventListener('change', resize);
    window.removeEventListener('popstate', popstate);
    window.removeEventListener('fieldwork:search', searchRequest);
});
</script>

<template>
    <p id="listing-status" :class="error ? 'notice notice-error' : 'visually-hidden'" :role="error ? 'alert' : 'status'" aria-live="polite">
        {{ announcement }} <button v-if="error" type="button" @click="load(retryUrl)">{{ labels.TryAgain }}</button>
    </p>
    <form ref="form" class="listing-search" :aria-busy="busy" @submit.prevent="apply(true)">
        <nav v-if="!model.editorial" class="mobile-listing-actions" :aria-label="labels.FilterAndSort">
            <a href="#listing-filters" @click="openFilters">{{ labels.FiltersShort ?? labels.Filters }}<template v-if="activeFilterCount"> ({{ activeFilterCount }})</template></a>
            <a href="#listing-sort">{{ labels.SortBy }}</a><span class="mobile-result-count">{{ model.resultLabel }}</span>
        </nav>
        <div class="listing-layout" :class="{ 'editorial-listing': model.editorial }">
            <aside v-if="!model.editorial" class="listing-sidebar">
                <details id="listing-filters" ref="panel" class="filter-panel" :open="filtersOpen" @toggle="filtersOpen = $event.target.open">
                    <summary>{{ labels.Filters }}</summary>
                    <div class="search-options">
                        <fieldset v-for="facet in model.facets" :key="facet.name">
                            <legend>{{ facet.label }}</legend>
                            <label v-for="choice in facet.choices" :key="(choice.queryName ?? facet.name) + '/' + choice.value" class="facet-option">
                                <input v-model="choices[choice.queryName ?? facet.name]" type="checkbox" :name="choice.queryName ?? facet.name" :value="choice.value" :disabled="choice.count === 0 && !choices[choice.queryName ?? facet.name].includes(choice.value)" @change="toggle(choice.queryName ?? facet.name)">
                                <span>{{ choice.label }}</span><small>({{ choice.count }})</small>
                            </label>
                            <template v-if="facet.name === 'price'">
                                <p class="custom-price-label">{{ labels.CustomPriceRange }} <small>({{ initial.currency }})</small></p>
                                <div :ref="setBudget" class="price-budget" @focusout="leaveBudget">
                                    <label>{{ labels.MinimumPrice }}<input v-model="minimum" name="minPrice" type="number" min="0" max="1000000" step="0.01" @input="editBudget" @keydown.enter.prevent="apply()"></label>
                                    <label>{{ labels.MaximumPrice }}<input v-model="maximum" name="maxPrice" type="number" min="0" max="1000000" step="0.01" @input="editBudget" @keydown.enter.prevent="apply()"></label>
                                </div>
                            </template>
                        </fieldset>
                        <div class="filter-actions"><a class="text-link" :href="model.resetUrl" @click="follow($event, model.resetUrl)">{{ labels.ClearFilters }}</a></div>
                        <a class="button mobile-show-results" href="#listing-results" :aria-disabled="busy" @click="showResults">{{ model.showResultsLabel }}</a>
                    </div>
                </details>
            </aside>
            <section id="listing-results" ref="results" class="listing-results" tabindex="-1" :aria-label="model.rangeLabel">
                <nav v-if="model.searchPage" class="search-scopes" :aria-label="labels.SearchResults">
                    <a :href="scopeUrl('product')" :aria-current="!model.editorial ? 'page' : undefined" @click="follow($event, scopeUrl('product'))">{{ labels.Products }}</a>
                    <a :href="scopeUrl('article')" :aria-current="model.editorial ? 'page' : undefined" @click="follow($event, scopeUrl('article'))">{{ labels.Journal }}</a>
                </nav>
                <div class="listing-toolbar">
                    <div class="listing-count"><span>{{ model.rangeLabel ?? model.resultLabel }}</span></div>
                    <div id="listing-sort" class="sort">
                        <label for="sort">{{ labels.SortBy }}</label>
                        <select id="sort" v-model="sort" name="sort" @change="changeSort" @keydown="sortKey" @pointerdown="keyboardSort = false" @blur="leaveSort">
                            <option v-for="[value, label] in sorts" :key="value" :value="value">{{ labels[label] }}</option>
                        </select>
                    </div>
                </div>
                <nav v-if="model.chips.length" class="applied-filters" :aria-label="labels.AppliedFilters">
                    <a v-for="chip in model.chips" :key="chip.label" class="filter-chip" :href="chip.url" :aria-label="labels.RemoveFilter.replace('{0}', chip.label)" @click="follow($event, chip.url)">{{ chip.label }} <span aria-hidden="true">×</span></a>
                    <a class="text-link" :href="model.resetUrl" @click="follow($event, model.resetUrl)">{{ labels.ClearFilters }}</a>
                </nav>
                <section v-if="model.searchPage" class="result-heading"><h2 tabindex="-1">{{ model.heading }}</h2></section>
                <section class="grid"><ListingCard v-for="card in model.cards" :key="card.key" :card="card" :labels="labels" /></section>
                <p v-if="model.updating" class="empty">{{ labels.Updating }}</p>
                <div v-if="model.total === 0" class="empty">
                    <h2>{{ labels.NoMatchesHeading }}</h2><p>{{ labels.NoMatchesHelp }}</p>
                    <div class="actions">
                        <a v-if="model.chips.length" class="button secondary" :href="model.resetUrl" @click="follow($event, model.resetUrl)">{{ labels.ClearFilters }}</a>
                        <a v-if="model.parameters.q?.[0]" class="text-link" :href="model.clearQueryUrl" @click="follow($event, model.clearQueryUrl)">{{ labels.BrowseWithoutQuery }}</a>
                    </div>
                </div>
                <nav v-if="model.lastPage > 1" class="pagination" :aria-label="labels.Pagination">
                    <a v-if="model.previousUrl" rel="prev" :href="model.previousUrl" @click="follow($event, model.previousUrl, true)">{{ labels.Previous }}</a>
                    <span>{{ model.rangeLabel ?? model.resultLabel }} · {{ model.pageLabel }}</span>
                    <a v-if="model.nextUrl" rel="next" :href="model.nextUrl" @click="follow($event, model.nextUrl, true)">{{ labels.Next }}</a>
                </nav>
            </section>
        </div>
    </form>
</template>
