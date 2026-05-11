// API base URL is resolved at runtime via public/env.js (window.__env.apiBaseUrl).
// Empty string means requests go to the same origin (appropriate behind a reverse proxy in production).
// To override at deploy time, modify the env.js file in the built dist/public/ folder.
export const environment = {
  production: true,
  apiBaseUrl: ''
};
