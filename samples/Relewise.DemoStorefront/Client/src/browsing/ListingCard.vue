<script setup>
defineProps({ card: { type: Object, required: true }, labels: { type: Object, required: true } });
</script>

<template>
    <article class="card" :data-node="card.key" :data-entity="card.entityId">
        <a :href="card.url">
            <!-- Only server-generated, escaped image/SVG markup; no authored HTML is accepted here. -->
            <div class="illustration" v-html="card.imageHtml" />
            <div class="card-body">
                <small>{{ card.brand ?? (card.product ? labels.Equipment : labels.FieldNotes) }}</small><h3>{{ card.title }}</h3>
                <template v-if="card.product">
                    <span class="card-price"><strong>{{ card.price }}</strong><template v-if="card.listPrice"><del>{{ card.listPrice }}</del><span class="offer-label">{{ card.savings ?? labels.OnOffer }}</span></template></span>
                    <span class="card-availability">{{ card.availability }}</span><span v-if="card.options" class="card-options">{{ card.options }}</span>
                </template>
                <strong v-else>{{ labels.ReadStory }}</strong>
                <p>{{ card.summary }}</p>
            </div>
        </a>
    </article>
</template>
