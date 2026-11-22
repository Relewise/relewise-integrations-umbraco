<script setup>
import { ref, shallowRef } from 'vue';
import { sendCart } from './shopState';

const props = defineProps({ initial: { type: Object, required: true } });
const cart = shallowRef(props.initial);
const busy = ref(false);
const error = ref('');
const message = ref('');
const emptyDialog = ref();
const region = ref();
async function mutate(action, line, quantity) {
    if (busy.value) return;
    busy.value = true;
    error.value = '';
    message.value = cart.value.labels.CartUpdating;
    const fields = { [cart.value.antiforgeryField]: cart.value.antiforgeryToken };
    if (line) {
        fields.product = line.productKey;
        fields.variant = line.variantKey ?? '';
        fields.quantity = quantity;
    }
    try {
        cart.value = (await sendCart(cart.value.cartUrl + '/' + action, fields)).cart;
        message.value = cart.value.labels.CartUpdated;
        if (emptyDialog.value?.open) emptyDialog.value.close();
    } catch (failure) {
        error.value = failure instanceof SyntaxError || failure instanceof TypeError ? cart.value.labels.PurchaseFailed : failure.message;
        message.value = '';
    } finally {
        busy.value = false;
    }
}
async function changeQuantity(event, line) {
    const input = event.target;
    if (!input.reportValidity()) return;
    await mutate('update', line, input.value);
    input.value = cart.value.lines.find(item => item.productKey === line.productKey && item.variantKey === line.variantKey)?.quantity ?? line.quantity;
}
async function proceed(event) {
    if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return;
    event.preventDefault();
    if (busy.value) return;
    const inputs = [...region.value.querySelectorAll('input[type=number]')];
    if (inputs.some(input => !input.reportValidity())) return;
    const available = cart.value.lines.filter(line => line.available);
    const edits = inputs.map((input, index) => ({ line: available[index], quantity: Number(input.value) }));
    for (const edit of edits) {
        if (edit.quantity !== edit.line.quantity) {
            await mutate('update', edit.line, edit.quantity);
            if (error.value) return;
        }
    }
    if (cart.value.ready) location.assign(cart.value.checkoutUrl);
}
</script>

<template>
    <p v-if="error" class="notice notice-error" role="alert">{{ error }}</p>
    <p class="visually-hidden" role="status">{{ message }}</p>
    <div v-if="cart.lines.length" ref="region" class="cart-layout" :aria-busy="busy">
        <section class="cart-items" :aria-label="cart.labels.CartItems">
            <article v-for="line in cart.lines" :key="line.productKey + '/' + line.variantKey" class="cart-row" :data-entity="line.productId">
                <a v-if="line.available" :href="line.url" class="illustration" :aria-label="line.name" v-html="line.imageHtml"/>
                <div v-else class="illustration" />
                <div class="cart-item-details">
                    <a v-if="line.available" class="text-link item-name" :href="line.url">{{ line.name }}</a><strong v-else>{{ line.name }}</strong>
                    <p v-if="line.available">{{ line.unitPrice }} {{ cart.labels.Each }}</p><p v-else>{{ cart.labels.UnavailableTotal }}</p>
                    <div class="cart-item-actions">
                        <label v-if="line.available">{{ cart.labels.Quantity }}<input :value="line.quantity" type="number" min="1" :max="line.maximum" :disabled="busy" @change="changeQuantity($event, line)"></label>
                        <button class="text-button remove-item" :disabled="busy" @click="mutate('update', line, 0)">{{ cart.labels.RemoveCart }}</button>
                    </div>
                </div>
                <strong>{{ line.lineTotal }}</strong>
            </article>
            <div class="cart-secondary-actions"><a class="text-link" :href="cart.continueUrl">← {{ cart.labels.ContinueShopping }}</a><button class="text-button empty-cart" :disabled="busy" @click="emptyDialog.showModal()">{{ cart.labels.EmptyCartAction }}</button></div>
        </section>
        <aside class="basket-summary">
            <h2>{{ cart.labels.OrderSummary }}</h2>
            <div class="cart-total"><span>{{ cart.labels.Total }}</span><strong>{{ cart.total }}</strong></div>
            <a v-if="cart.ready" class="button" :class="{ 'is-disabled': busy }" :aria-disabled="busy" :href="cart.checkoutUrl" @click="proceed">{{ cart.labels.Checkout }} →</a>
            <p v-else role="alert">{{ cart.labels.CartUnavailable }}</p>
        </aside>
    </div>
    <section v-else class="empty-cart-page"><p>{{ cart.labels.CartEmpty }}</p><a class="button" :href="cart.continueUrl">{{ cart.labels.ContinueShopping }}</a></section>
    <dialog ref="emptyDialog" class="confirm-dialog" aria-labelledby="empty-cart-title">
        <h2 id="empty-cart-title">{{ cart.labels.EmptyCartConfirm }}</h2>
        <p v-if="error" role="alert">{{ error }}</p>
        <div class="actions"><button class="secondary" :disabled="busy" @click="emptyDialog.close()">{{ cart.labels.Cancel }}</button><button :disabled="busy" @click="mutate('clear')">{{ cart.labels.EmptyCartAction }}</button></div>
    </dialog>
</template>
