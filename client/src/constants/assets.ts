import type { Asset } from '../types';

const getIconUrl = (symbol: string) =>
  `https://cdn.jsdelivr.net/gh/atomiclabs/cryptocurrency-icons@1a63530be6e374711a8554f31b17e4cb92c25fa5/128/color/${symbol.toLowerCase()}.png`;

export const ASSETS = [
  { id: 'bitcoin', name: 'Bitcoin', ticker: 'BTC', icon: getIconUrl('btc') },
  { id: 'ethereum', name: 'Ethereum', ticker: 'ETH', icon: getIconUrl('eth') },
  { id: 'binancecoin', name: 'BNB', ticker: 'BNB', icon: getIconUrl('bnb') },
  {
    id: 'solana',
    name: 'Solana',
    ticker: 'SOL',
    icon: 'https://cryptologos.cc/logos/solana-sol-logo.png',
  },
  { id: 'ripple', name: 'Ripple', ticker: 'XRP', icon: getIconUrl('xrp') },
  { id: 'dogecoin', name: 'Dogecoin', ticker: 'DOGE', icon: getIconUrl('doge') },
  {
    id: 'the-open-network',
    name: 'Toncoin',
    ticker: 'TON',
    icon: 'https://cryptologos.cc/logos/toncoin-ton-logo.png',
  },
  { id: 'cardano', name: 'Cardano', ticker: 'ADA', icon: getIconUrl('ada') },
  {
    id: 'shiba-inu',
    name: 'Shiba Inu',
    ticker: 'SHIB',
    icon: 'https://cryptologos.cc/logos/shiba-inu-shib-logo.png',
  },
  {
    id: 'avalanche-2',
    name: 'Avalanche',
    ticker: 'AVAX',
    icon: 'https://cryptologos.cc/logos/avalanche-avax-logo.png',
  },
  { id: 'tron', name: 'TRON', ticker: 'TRX', icon: getIconUrl('trx') },
  {
    id: 'polkadot',
    name: 'Polkadot',
    ticker: 'DOT',
    icon: 'https://cryptologos.cc/logos/polkadot-new-dot-logo.png',
  },
  {
    id: 'bitcoin-cash',
    name: 'Bitcoin Cash',
    ticker: 'BCH',
    icon: getIconUrl('bch'),
  },
  { id: 'chainlink', name: 'Chainlink', ticker: 'LINK', icon: getIconUrl('link') },
  {
    id: 'polygon-ecosystem-token',
    name: 'Polygon',
    ticker: 'POL',
    icon: '/icons/polygon.png',
  },
  {
    id: 'near',
    name: 'NEAR Protocol',
    ticker: 'NEAR',
    icon: 'https://cryptologos.cc/logos/near-protocol-near-logo.png',
  },
  {
    id: 'internet-computer',
    name: 'Internet Computer',
    ticker: 'ICP',
    icon: 'https://cryptologos.cc/logos/internet-computer-icp-logo.png',
  },
  { id: 'litecoin', name: 'Litecoin', ticker: 'LTC', icon: getIconUrl('ltc') },
  {
    id: 'uniswap',
    name: 'Uniswap',
    ticker: 'UNI',
    icon: 'https://cryptologos.cc/logos/uniswap-uni-logo.png',
  },
  {
    id: 'aptos',
    name: 'Aptos',
    ticker: 'APT',
    icon: 'https://cryptologos.cc/logos/aptos-apt-logo.png',
  },
] satisfies readonly Asset[];
