// API base URL is resolved at runtime via public/env.js (window.__env.apiBaseUrl).
// Empty string means requests go to the same origin (appropriate when using proxy.conf.js in dev).
export const environment = {
  production: false,
  apiBaseUrl: ''
};
