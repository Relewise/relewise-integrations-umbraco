import { Tracker, User, UserFactory } from '@relewise/client'
import { ILineItem } from './basket.service'
import cookieConsentService from './cookie-consent.service'

class TrackingService {
  private readonly tracker: Tracker|null = null;
  private readonly NoTrackerWarning = "Warning: The 'Browser' tracker is missing - This could be due to missing configuration in appSettings";

  constructor () {
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const trackerConfig = (window as any).__tracker
    if (trackerConfig) {
      this.tracker = new Tracker(trackerConfig.datasetId, trackerConfig.apiKey)
    }
  }

  public trackProductCategoryView (id: string) {
    if (this.tracker === null) {
      console.warn(this.NoTrackerWarning)
      return
    }

    this.tracker.trackProductCategoryView({ idPath: [id], user: this.getUser() })
  }

  public async trackProductView (id: string) {
    if (this.tracker === null) {
      console.warn(this.NoTrackerWarning)
      return
    }

    this.tracker.trackProductView({ productId: id, user: this.getUser() })
  }

  public async trackCart (lineItems: ILineItem[]) {
    if (this.tracker === null) {
      console.warn(this.NoTrackerWarning)
      return
    }

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
    if (this.tracker === null) {
      console.warn(this.NoTrackerWarning)
      return
    }

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
