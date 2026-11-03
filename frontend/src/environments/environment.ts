// Production build: the API is served from the same origin (nginx proxies /api and /hubs)
export const environment = {
  production: true,
  apiUrl: '/api',
  hubUrl: '/hubs/notifications',
};
