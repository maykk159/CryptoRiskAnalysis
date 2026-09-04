// Using a reliable CDN for crypto icons with explicit overrides for newer coins missing from the atomiclabs repo
const getIconUrl = (symbol: string) => `https://cdn.jsdelivr.net/gh/atomiclabs/cryptocurrency-icons@1a63530be6e374711a8554f31b17e4cb92c25fa5/128/color/${symbol.toLowerCase()}.png`;

export const ASSETS = [
    { id: 'bitcoin', name: 'Bitcoin', ticker: 'BTC', symbol: 'btc', icon: getIconUrl('btc') },
    { id: 'ethereum', name: 'Ethereum', ticker: 'ETH', symbol: 'eth', icon: getIconUrl('eth') },
    { id: 'binancecoin', name: 'BNB', ticker: 'BNB', symbol: 'bnb', icon: getIconUrl('bnb') },
    { id: 'solana', name: 'Solana', ticker: 'SOL', symbol: 'sol', icon: 'https://cryptologos.cc/logos/solana-sol-logo.png' },
    { id: 'ripple', name: 'Ripple', ticker: 'XRP', symbol: 'xrp', icon: getIconUrl('xrp') },
    { id: 'dogecoin', name: 'Dogecoin', ticker: 'DOGE', symbol: 'doge', icon: getIconUrl('doge') },
    { id: 'the-open-network', name: 'Toncoin', ticker: 'TON', symbol: 'ton', icon: 'https://cryptologos.cc/logos/toncoin-ton-logo.png' },
    { id: 'cardano', name: 'Cardano', ticker: 'ADA', symbol: 'ada', icon: getIconUrl('ada') },
    { id: 'shiba-inu', name: 'Shiba Inu', ticker: 'SHIB', symbol: 'shib', icon: 'https://cryptologos.cc/logos/shiba-inu-shib-logo.png' },
    { id: 'avalanche-2', name: 'Avalanche', ticker: 'AVAX', symbol: 'avax', icon: 'https://cryptologos.cc/logos/avalanche-avax-logo.png' },
    { id: 'tron', name: 'TRON', ticker: 'TRX', symbol: 'trx', icon: getIconUrl('trx') },
    { id: 'polkadot', name: 'Polkadot', ticker: 'DOT', symbol: 'dot', icon: 'https://cryptologos.cc/logos/polkadot-new-dot-logo.png' },
    { id: 'bitcoin-cash', name: 'Bitcoin Cash', ticker: 'BCH', symbol: 'bch', icon: getIconUrl('bch') },
    { id: 'chainlink', name: 'Chainlink', ticker: 'LINK', symbol: 'link', icon: getIconUrl('link') },
    { id: 'polygon-ecosystem-token', name: 'Polygon', ticker: 'POL', symbol: 'pol', icon: 'https://cryptologos.cc/logos/polygon-ecosystem-token-pol-logo.png' },
    { id: 'near', name: 'NEAR Protocol', ticker: 'NEAR', symbol: 'near', icon: 'https://cryptologos.cc/logos/near-protocol-near-logo.png' },
    { id: 'internet-computer', name: 'Internet Computer', ticker: 'ICP', symbol: 'icp', icon: 'https://cryptologos.cc/logos/internet-computer-icp-logo.png' },
    { id: 'litecoin', name: 'Litecoin', ticker: 'LTC', symbol: 'ltc', icon: getIconUrl('ltc') },
    { id: 'uniswap', name: 'Uniswap', ticker: 'UNI', symbol: 'uni', icon: 'https://cryptologos.cc/logos/uniswap-uni-logo.png' },
    { id: 'aptos', name: 'Aptos', ticker: 'APT', symbol: 'apt', icon: 'https://cryptologos.cc/logos/aptos-apt-logo.png' },
];
