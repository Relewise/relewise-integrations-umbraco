<script setup>
defineProps({ title: { type: String, required: true }, links: { type: Array, required: true }, collapsible: Boolean });
</script>

<template>
    <component :is="collapsible ? 'details' : 'section'" class="footer-group">
        <summary v-if="collapsible">{{ title }}</summary>
        <h2 v-else>{{ title }}</h2>
        <ul>
            <li v-for="link in links" :key="link.url">
                <a v-if="link.enabled !== false" :href="link.url">{{ link.label }}</a>
                <span v-else aria-disabled="true">{{ link.label }}</span>
            </li>
        </ul>
    </component>
</template>
