const { env } = require('process');

const target = env["services__api__https__0"] ?? env["services__api__http__0"] ?? 'https://localhost:7259';

const PROXY_CONFIG = [
  {
    context: [
      "/weatherforecast",
      "/api",
      "/GetResource",
      "/TextControl",
      "/DocumentViewer",
    ],
    target,
    secure: false
  },
  {
    context: [
      "/TXWebSocket",
    ],
    target,
    secure: false,
    ws: true
  }
]

module.exports = PROXY_CONFIG;
