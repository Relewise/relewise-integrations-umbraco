import { computed, reactive } from 'vue'

interface ICookieConsentState {
  hasClickedOnCookieBanner: boolean;
  data: UserCookies;
}

class CookieConsentService {
    private static readonly CookieVersion: string = '1';

    private state = reactive<ICookieConsentState>({
      hasClickedOnCookieBanner: false,
      data: { userId: this.uniqueId(), cookies: { functional: false, marketing: false, statistical: false }, version: CookieConsentService.CookieVersion }
    });

    constructor () {
      const cookiesCookie = this.getCookie('_cookieconsent')
      this.state.hasClickedOnCookieBanner = cookiesCookie != null
      if (cookiesCookie) {
        const parsedCookie = JSON.parse(cookiesCookie)
        if (parsedCookie.version === CookieConsentService.CookieVersion) {
          this.state.data = parsedCookie
        }
      }
    }

    get hasClickedOnCookieBanner () {
      return computed(() => this.state.hasClickedOnCookieBanner)
    }

    get cookies () {
      return computed(() => this.state.data.cookies)
    }

    get userIdIfHasConsent () {
      return this.state.data.cookies.marketing
        ? this.state.data.userId
        : null
    }

    acceptAllCookies () {
      this.state.hasClickedOnCookieBanner = true
      this.state.data.cookies.functional = true
      this.state.data.cookies.marketing = true
      this.state.data.cookies.statistical = true
      this.setCookie('_cookieconsent', JSON.stringify(this.state.data), 365)
    }

    acceptCookies () {
      this.state.hasClickedOnCookieBanner = true
      this.setCookie('_cookieconsent', JSON.stringify(this.state.data), 365)
    }

    rejectCookies () {
      this.state.hasClickedOnCookieBanner = true
      this.state.data.cookies.functional = false
      this.state.data.cookies.marketing = false
      this.state.data.cookies.statistical = false
      this.setCookie('_cookieconsent', JSON.stringify(this.state.data), 365)
    }

    private setCookie (name: string, value: string, days: number) {
      let expires = ''
      if (days) {
        const date = new Date()
        date.setTime(date.getTime() + (days * 24 * 60 * 60 * 1000))
        expires = '; expires=' + date.toUTCString()
      }
      document.cookie = name + '=' + encodeURIComponent((value || '')) + expires + ';samesite=lax; Secure; path=/'
    }

    private getCookie (name: string) {
      const nameEQ = name + '='
      const ca = document.cookie.split(';')
      for (let i = 0; i < ca.length; i++) {
        let c = ca[i]
        while (c.charAt(0) === ' ') c = c.substring(1, c.length)
        if (c.indexOf(nameEQ) === 0) return decodeURIComponent(c.substring(nameEQ.length, c.length))
      }
      return null
    }

    private uniqueId () {
      const a = new Uint32Array(3)
      window.crypto.getRandomValues(a)
      return (performance.now().toString(36) + Array.from(a).map(A => A.toString(36)).join('')).replace(/\./g, '')
    }
}

export default new CookieConsentService()
