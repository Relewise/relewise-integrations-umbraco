import { Tracker, User, UserFactory } from '@relewise/client'
import { ILineItem } from './basket.service'
import cookieConsentService from './cookie-consent.service'

class TrackingService {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    private readonly tracker = new Tracker((window as any).__tracker.datasetId, (window as any).__tracker.apiKey);

    public trackProductCategoryView (id: string) {
      this.tracker.trackProductCategoryView({ idPath: [id], user: this.getUser() })
    }

    public async trackProductView (id: string) {
      this.tracker.trackProductView({ productId: id, user: this.getUser() })
    }

    public async trackCart (lineItems: ILineItem[]) {
      const items = lineItems.map(x => ({
        productId: x.product.productId ?? '',
        quantity: x.quantity,
        lineTotal: x.quantity * (x.product.salesPrice ?? 0)
      }))
      const subTotal = items.reduce((total, i) => total + i.lineTotal, 0)

      this.tracker.trackCart({
        lineItems: items,
        subtotal: { currency: 'USD', amount: subTotal },
        user: this.getUser()
      })
    }

    public async trackOrder (lineItems: ILineItem[]) {
      const items = lineItems.map(x => ({
        productId: x.product.productId ?? '',
        quantity: x.quantity,
        lineTotal: x.quantity * (x.product.salesPrice ?? 0)
      }))
      const subTotal = items.reduce((total, i) => total + i.lineTotal, 0)

      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      const trackingNumberGenerator = (crypto as any)
      await this.tracker.trackOrder({
        lineItems: items,
        subtotal: { currency: 'USD', amount: subTotal },
        user: this.getUser(),
        trackingNumber: trackingNumberGenerator.randomUUID()
      })
    }

    private getUser (): User {
      const userId = cookieConsentService.userIdIfHasConsent

      return userId !== null
        ? UserFactory.byTemporaryId(userId)
        : UserFactory.anonymous()
    }
}

export default new TrackingService()
