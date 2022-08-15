import { ProductResult } from '@relewise/client'
import { computed, reactive } from '@vue/runtime-dom'
import trackingService from './tracking.service'

export interface ILineItem {
    product: ProductResult;
    quantity: number
}

export interface IBasket {
    lineItems: ILineItem[];
}

interface IBasketState {
    model: IBasket;
}

class BasketService {
    private state = reactive<IBasketState>({
      model: { lineItems: [] }
    });

    constructor () {
      const storedBasket = localStorage.getItem('boozyBasket')
      if (storedBasket !== null) {
        this.state.model = JSON.parse(storedBasket)
      }
    }

    get model () {
      return computed(() => this.state.model)
    }

    async addProduct ({ product, quantityDelta }: {product: ProductResult, quantityDelta: number}) {
      const productIndex = this.state.model.lineItems.findIndex(x => x.product.productId === product.productId)

      if (productIndex > -1) {
        const lineItem = this.state.model.lineItems[productIndex]
        lineItem.quantity = lineItem.quantity + quantityDelta
        if (lineItem.quantity <= 0) {
          this.state.model.lineItems.splice(productIndex, 1)
        }
      } else {
        this.model.value.lineItems.push({ product: product, quantity: quantityDelta })
      }

      await this.basketModified()
    }

    async remove (lineItem: ILineItem) {
      this.state.model.lineItems.splice(this.state.model.lineItems.indexOf(lineItem), 1)
      await this.basketModified()
    }

    async clear () {
      await trackingService.trackOrder(this.model.value.lineItems)
      this.state.model = { lineItems: [] }
      localStorage.setItem('boozyBasket', JSON.stringify(this.model.value))
    }

    private async basketModified () {
      await trackingService.trackCart(this.model.value.lineItems)
      localStorage.setItem('boozyBasket', JSON.stringify(this.model.value))
    }
}

export default new BasketService()
