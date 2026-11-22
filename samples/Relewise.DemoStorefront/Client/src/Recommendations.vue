<script setup>
import { onMounted, onUnmounted, ref } from 'vue';
import ListingCard from './ListingCard.vue';

const props = defineProps({ initial: { type: Object, required: true } });
const groups = ref({ similar: [], recentlyViewed: [], popular: [], categories: [] });
const loading = ref(true);
const controller = new AbortController();
onMounted(async () => {
    try {
        const response = await fetch(props.initial.endpoint, {
            signal: controller.signal,
            headers: { Accept: 'application/json' },
            cache: 'no-store'
        });
        if (response.ok) {
            const data = await response.json();
            const sections = {};
            for (const key of Object.keys(groups.value)) {
                sections[key] = Array.isArray(data?.[key]) ? data[key].filter(item => item && typeof item === 'object') : [];
            }
            if (!controller.signal.aborted) groups.value = sections;
        }
    } catch {
        // Optional recommendations must never interrupt the Product page or shopping flow.
    } finally {
        if (!controller.signal.aborted) loading.value = false;
    }
});
onUnmounted(() => controller.abort());
</script>

<template>
    <p v-if="loading" class="visually-hidden" role="status">{{ initial.labels.LoadingRecommendations }}</p>
    <section v-if="groups.similar.length" class="product-recommendations page-section" aria-labelledby="similar-products-heading">
        <h2 id="similar-products-heading">{{ initial.labels.SimilarProducts }}</h2>
        <div class="recommendation-grid"><ListingCard v-for="card in groups.similar" :key="card.key" :card="card" :labels="initial.labels" /></div>
    </section>
    <section v-if="groups.popular.length" class="product-recommendations page-section" aria-labelledby="popular-products-heading">
        <h2 id="popular-products-heading">{{ initial.labels.PopularProducts }}</h2>
        <div class="recommendation-grid"><ListingCard v-for="card in groups.popular" :key="card.key" :card="card" :labels="initial.labels" /></div>
    </section>
    <section v-if="groups.categories.length" class="category-recommendations page-section" aria-labelledby="popular-categories-heading">
        <h2 id="popular-categories-heading">{{ initial.labels.PopularCategories }}</h2>
        <div class="recommendation-grid">
            <article v-for="category in groups.categories" :key="category.key" class="card category-recommendation" :data-entity="category.entityId">
                <a :href="category.url"><div class="illustration" v-html="category.imageHtml" /><div class="card-body"><h3>{{ category.title }} <span aria-hidden="true">→</span></h3></div></a>
            </article>
        </div>
    </section>
    <section v-if="groups.recentlyViewed.length" class="product-recommendations page-section" aria-labelledby="recent-products-heading">
        <h2 id="recent-products-heading">{{ initial.labels.RecentlyViewedProducts }}</h2>
        <div class="recommendation-grid"><ListingCard v-for="card in groups.recentlyViewed" :key="card.key" :card="card" :labels="initial.labels" /></div>
    </section>
</template>
