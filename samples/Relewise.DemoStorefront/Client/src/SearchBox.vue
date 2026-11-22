<script setup>
import { computed, nextTick, onMounted, onUnmounted, ref, watch } from 'vue';
import { shop } from './shopState';

const props = defineProps({ initial: { type: Object, required: true } });
const form = ref();
const input = ref();
const query = ref(shop.searchQuery);
const typedQuery = ref(query.value);
const suggestions = ref([]);
const focused = ref(false);
const dismissed = ref(false);
const active = ref(-1);
const mobile = ref(false);
const visibleSuggestions = computed(() => suggestions.value.slice(0, mobile.value ? 4 : 5));
const expanded = computed(() => focused.value && !dismissed.value && visibleSuggestions.value.length > 0);
let pending;
let narrow;
const resize = () => { mobile.value = narrow.matches; active.value = -1; };
function outside(event) {
    if (!form.value.contains(event.target)) close();
}
onMounted(() => {
    narrow = matchMedia('(max-width: 760px)');
    resize();
    narrow.addEventListener('change', resize);
    document.addEventListener('pointerdown', outside);
});
onUnmounted(() => {
    pending?.abort();
    narrow?.removeEventListener('change', resize);
    document.removeEventListener('pointerdown', outside);
});
watch(() => shop.searchQuery, value => {
    query.value = value;
    typedQuery.value = value;
});
watch(() => [typedQuery.value, shop.searchScope, focused.value], ([value, scope, hasFocus], _, cleanup) => {
    suggestions.value = [];
    active.value = -1;
    const term = value.trim();
    if (!props.initial.searchConfigured || !hasFocus || term.length < 2 || term.length > 80) return;
    const controller = new AbortController();
    pending = controller;
    const timer = setTimeout(async () => {
        const url = new URL(props.initial.suggestionsUrl, location.origin);
        url.searchParams.set('q', term);
        url.searchParams.set('type', scope);
        try {
            const response = await fetch(url, { signal: controller.signal, cache: 'no-store' });
            if (!response.ok) return;
            const values = await response.json();
            if (!controller.signal.aborted && Array.isArray(values)) {
                suggestions.value = [...new Set(values.filter(item => typeof item === 'string' && item.trim()))];
            }
        } catch {
            // Predictions are optional; submitting a search must remain available.
        }
    }, 250);
    cleanup(() => { clearTimeout(timer); controller.abort(); });
});
function close() {
    dismissed.value = true;
    active.value = -1;
    pending?.abort();
}
function edited() {
    typedQuery.value = query.value;
    shop.searchQuery = query.value;
    dismissed.value = false;
    active.value = -1;
}
function move(event) {
    if (event.isComposing) return;
    if (event.key === 'Escape') {
        event.preventDefault();
        event.stopPropagation();
        query.value = typedQuery.value;
        close();
    } else if ((event.key === 'ArrowDown' || event.key === 'ArrowUp') && visibleSuggestions.value.length) {
        event.preventDefault();
        dismissed.value = false;
        const count = visibleSuggestions.value.length;
        active.value = event.key === 'ArrowDown'
            ? (active.value + 2) % (count + 1) - 1
            : active.value < 0 ? count - 1 : active.value - 1;
        query.value = active.value < 0 ? typedQuery.value : visibleSuggestions.value[active.value];
    }
}
async function refine(term) {
    query.value = term;
    edited();
    await nextTick();
    input.value.focus();
}
function choose(term) {
    query.value = term;
    submit();
}
function submit() {
    if (!props.initial.searchConfigured) return;
    shop.searchQuery = query.value.trim();
    close();
    if (props.initial.searchPage && location.pathname === props.initial.searchUrl) {
        window.dispatchEvent(new CustomEvent('fieldwork:search', { detail: { query: shop.searchQuery, scope: shop.searchScope } }));
        return;
    }
    const url = new URL(props.initial.searchUrl, location.origin);
    url.searchParams.set('q', shop.searchQuery);
    if (shop.searchScope === 'article') url.searchParams.set('type', 'article');
    location.assign(url);
}
function parts(term) {
    const prefix = typedQuery.value.trim();
    return term.toLocaleLowerCase().startsWith(prefix.toLocaleLowerCase())
        ? [term.slice(0, prefix.length), term.slice(prefix.length)] : ['', term];
}
</script>

<template>
    <div v-if="expanded" class="search-scrim" aria-hidden="true" @pointerdown.prevent="close" />
    <form ref="form" class="global-search" role="search" :action="initial.searchUrl" @submit.prevent="submit" @focusout="event => { if (!form.contains(event.relatedTarget)) focused = false; }">
        <label class="visually-hidden" for="global-query">{{ shop.searchScope === 'article' ? initial.labels.SearchJournal : initial.labels.SearchProducts }}</label>
        <input id="global-query" ref="input" v-model="query" name="q" type="search" role="combobox" aria-autocomplete="list" aria-controls="global-predictions" :aria-expanded="expanded" :aria-activedescendant="expanded && active >= 0 ? `prediction-${active}` : undefined" autocomplete="off" enterkeyhint="search" :disabled="!initial.searchConfigured" :placeholder="!initial.searchConfigured ? initial.labels.SearchDisabled : shop.searchScope === 'article' ? initial.labels.SearchJournal : initial.labels.SearchProducts" @input="edited" @focus="focused = true; dismissed = false" @keydown="move">
        <button v-if="query && initial.searchConfigured" type="button" class="search-clear" @pointerdown.prevent :aria-label="initial.labels.ClearSearch" @click="refine('')"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m6 6 12 12M6 18 18 6"/></svg></button>
        <button class="search-submit" :disabled="!initial.searchConfigured" :aria-label="initial.labels.Search"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/></svg></button>
        <p class="visually-hidden" role="status">{{ expanded ? initial.labels.SuggestionsAvailable.replace('{0}', visibleSuggestions.length) : '' }}</p>
        <div v-if="expanded" class="search-predictions">
            <p id="prediction-heading">{{ initial.labels.SuggestedSearches }}</p>
            <div class="prediction-columns">
                <ul id="global-predictions" role="listbox" aria-labelledby="prediction-heading">
                    <li v-for="(term, index) in visibleSuggestions" :id="`prediction-${index}`" :key="term" role="option" :aria-selected="active === index" :class="{ 'is-active': active === index }" @pointerdown.prevent @click="choose(term)">
                        <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/></svg>
                        <span>{{ parts(term)[0] }}<strong>{{ parts(term)[1] }}</strong></span>
                    </li>
                </ul>
                <div class="prediction-refinements">
                    <button v-for="term in visibleSuggestions" :key="term" type="button" :aria-label="initial.labels.RefineSuggestion.replace('{0}', term)" :title="initial.labels.RefineSuggestion.replace('{0}', term)" @pointerdown.prevent @click="refine(term)"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M17 17 7 7m0 9V7h9"/></svg></button>
                </div>
            </div>
        </div>
    </form>
</template>
