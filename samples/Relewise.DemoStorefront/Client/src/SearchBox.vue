<script setup>
import { onUnmounted, ref, watch } from 'vue';
import { shop } from './shopState';

const props = defineProps({ initial: { type: Object, required: true } });
const suggestions = ref([]);
let pending;
watch(() => [shop.searchQuery, shop.searchScope], ([query, scope], _, cleanup) => {
    suggestions.value = [];
    if (!props.initial.searchConfigured) return;
    const term = query.trim();
    if (term.length < 2 || term.length > 80) return;
    const controller = new AbortController();
    pending = controller;
    const timer = setTimeout(async () => {
        const url = new URL(props.initial.suggestionsUrl, location.origin);
        url.searchParams.set('q', term);
        url.searchParams.set('type', scope);
        try {
            const response = await fetch(url, { signal: controller.signal });
            if (!response.ok) return;
            const values = await response.json();
            if (!controller.signal.aborted) suggestions.value = values;
        } catch {
            // A prediction failure never prevents explicit search.
        }
    }, 300);
    cleanup(() => { clearTimeout(timer); controller.abort(); });
});
onUnmounted(() => pending?.abort());
function submit() {
    if (!props.initial.searchConfigured) return;
    pending?.abort();
    if (props.initial.searchPage && location.pathname === props.initial.searchUrl) {
        window.dispatchEvent(new CustomEvent('fieldwork:search', { detail: { query: shop.searchQuery, scope: shop.searchScope } }));
        return;
    }
    const url = new URL(props.initial.searchUrl, location.origin);
    url.searchParams.set('q', shop.searchQuery);
    if (shop.searchScope === 'article') url.searchParams.set('type', 'article');
    location.assign(url);
}
</script>

<template>
    <form class="global-search" role="search" :action="initial.searchUrl" @submit.prevent="submit">
        <label class="visually-hidden" for="global-query">{{ shop.searchScope === 'article' ? initial.labels.SearchJournal : initial.labels.SearchProducts }}</label>
        <input id="global-query" v-model="shop.searchQuery" name="q" type="search" list="global-predictions" autocomplete="off" :disabled="!initial.searchConfigured" :placeholder="!initial.searchConfigured ? initial.labels.SearchDisabled : shop.searchScope === 'article' ? initial.labels.SearchJournal : initial.labels.SearchProducts">
        <button :disabled="!initial.searchConfigured" :aria-label="initial.labels.Search"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="10.5" cy="10.5" r="6.5"/><path d="m16 16 5 5"/></svg></button>
        <datalist id="global-predictions"><option v-for="term in suggestions" :key="term" :value="term" /></datalist>
    </form>
</template>
