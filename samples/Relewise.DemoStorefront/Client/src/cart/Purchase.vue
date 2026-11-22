<script setup>
import { ref } from 'vue';
import { sendCart, shop } from '../shell/shopState.js';

const props = defineProps({ initial: { type: Object, required: true } });
const quantity = ref(1);
const busy = ref(false);
const error = ref('');
const notice = ref('');
let commandId = crypto.randomUUID();
async function add() {
    if (busy.value) return;
    busy.value = true;
    error.value = notice.value = '';
    try {
        const result = await sendCart(props.initial.url, { ...props.initial.fields, quantity: quantity.value, commandId });
        commandId = crypto.randomUUID();
        if (result.added) shop.added = { item: result.added, cart: result.cart };
        else notice.value = props.initial.labels.CartUpdated;
    } catch (failure) {
        error.value = failure instanceof SyntaxError || failure instanceof TypeError ? props.initial.labels.PurchaseFailed : failure.message;
    } finally {
        busy.value = false;
    }
}
</script>

<template>
    <form class="add-cart" :aria-busy="busy" @submit.prevent="add">
        <label>{{ initial.labels.Quantity }}<input v-model="quantity" type="number" name="quantity" min="1" :max="initial.maximum" required :disabled="busy"></label>
        <button :disabled="busy">{{ busy ? initial.labels.AddingToCart : initial.labels.AddCart }}</button>
    </form>
    <p v-if="error" class="purchase-error" role="alert">{{ error }}</p>
    <p v-if="notice" role="status">{{ notice }}</p>
</template>
