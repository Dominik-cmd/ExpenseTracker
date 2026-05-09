const PROXY_CONFIG = [
  {
    context: ["/api"],
    target: "http://api:8080",
    secure: false,
    changeOrigin: true
  }
];

module.exports = PROXY_CONFIG;
