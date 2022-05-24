const { defineConfig } = require('@vue/cli-service')
module.exports = defineConfig({
  configureWebpack: {
    devServer: {
      headers: {
        'Access-Control-Allow-Origin': '*'
      }
    },
    resolve: {
      alias: {
        vue: 'vue/dist/vue.runtime.esm-bundler.js'
      }
    }
  },
  transpileDependencies: true
})
