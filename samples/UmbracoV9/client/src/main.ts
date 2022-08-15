import { createApp } from 'vue'
import App from './App.vue'
import ProductRecommendationsWrapper from './components/ProductRecommendationsWrapper.vue'
import RecommendationContentPopular from './components/RecommendationContentPopular.vue'
import ProductRecommendations from './components/RecommendationProducts.vue'
import NewsletterSubscribe from './components/NewsletterSubscribe.vue'
import MiniBasket from './components/MiniBasket.vue'
import UpdateConsent from './components/UpdateConsent.vue'
import SuggestiveSearch from './components/SuggestiveSearch.vue'

import CategoryPage from './pages/CategoryPage.vue'
import BasketPage from './pages/BasketPage.vue'
import ProductPage from './pages/ProductPage.vue'
import SearchPage from './pages/SearchPage.vue'

createApp(SearchPage).mount('#content-search')
createApp(CategoryPage).mount('#category-page')
createApp(BasketPage).mount('#basket-page')
createApp(ProductPage).mount('#product-page')

createApp(App).mount('#cookie-consent')
createApp(UpdateConsent).mount('#update-cookie-consent')
createApp(SuggestiveSearch).mount('#suggestive-search')
createApp(MiniBasket).mount('#mini-basket')
createApp(RecommendationContentPopular).mount('#recommend-popular')
createApp(NewsletterSubscribe).mount('#newsletter-subscribe')
createApp(ProductRecommendations).mount('#product-recommendations')
createApp(ProductRecommendationsWrapper).mount('#product-recommendations-wrapper')
