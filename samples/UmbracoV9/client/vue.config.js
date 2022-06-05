const { defineConfig } = require('@vue/cli-service')
module.exports = defineConfig({
  configureWebpack: {
    devServer: {
      port: 9292,
      headers: {
        'Access-Control-Allow-Origin': '*'
      }
    }
  },
  transpileDependencies: true
})
