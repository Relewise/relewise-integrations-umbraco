import { createApp } from 'vue'
import App from './App.vue'
import RecommendationContentPopular from './components/RecommendationContentPopular.vue'
import NewsletterSubscribe from './components/NewsletterSubscribe.vue'
import UpdateConsent from './UpdateConsent.vue'
import SuggestiveSearch from './SuggestiveSearch.vue'
import ContentSearch from './ContentSearch.vue'

createApp(App).mount('#cookie-consent')
createApp(UpdateConsent).mount('#update-cookie-consent')
createApp(SuggestiveSearch).mount('#suggestive-search')
createApp(ContentSearch).mount('#content-search')
createApp(RecommendationContentPopular).mount('#recommend-popular')
createApp(NewsletterSubscribe).mount('#newsletter-subscribe')
