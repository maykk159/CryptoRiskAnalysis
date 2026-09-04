import React, { useState, useRef, useEffect } from 'react';
import { ASSETS } from '../constants/assets';
import { ChevronDown } from 'lucide-react';
import clsx from 'clsx';

interface AssetSelectorProps {
    selectedAsset: string;
    onSelectAsset: (asset: string) => void;
}

const CoinIcon: React.FC<{ iconUrl: string; name: string; ticker: string }> = ({ iconUrl, name, ticker }) => {
    const [failedIconUrl, setFailedIconUrl] = useState<string | null>(null);
    const hasError = failedIconUrl === iconUrl;

    if (hasError) {
        return (
            <div className="w-6 h-6 rounded-full bg-gray-700 flex items-center justify-center text-xs font-bold text-gray-300 shrink-0">
                {ticker.slice(0, 2).toUpperCase()}
            </div>
        );
    }

    return (
        <img 
            src={iconUrl} 
            alt={`${name} icon`} 
            className="w-6 h-6 object-contain shrink-0" 
            onError={() => setFailedIconUrl(iconUrl)}
        />
    );
};

export const AssetSelector: React.FC<AssetSelectorProps> = ({ selectedAsset, onSelectAsset }) => {
    const [isOpen, setIsOpen] = useState(false);
    const dropdownRef = useRef<HTMLDivElement>(null);

    const selectedAssetData = ASSETS.find(a => a.id === selectedAsset) || ASSETS[0];

    useEffect(() => {
        const handleClickOutside = (event: MouseEvent) => {
            if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
                setIsOpen(false);
            }
        };

        document.addEventListener('mousedown', handleClickOutside);
        return () => {
            document.removeEventListener('mousedown', handleClickOutside);
        };
    }, []);

    const handleSelect = (assetId: string) => {
        onSelectAsset(assetId);
        setIsOpen(false);
    };

    const handleKeyDown = (e: React.KeyboardEvent, assetId?: string) => {
        if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            if (assetId) {
                handleSelect(assetId);
            } else {
                setIsOpen(!isOpen);
            }
        } else if (e.key === 'Escape') {
            setIsOpen(false);
        } else if (e.key === 'ArrowDown' || e.key === 'ArrowUp') {
            // Optional: prevent default scrolling when using arrows on the dropdown button
            e.preventDefault();
            if (!isOpen) setIsOpen(true);
        }
    };

    return (
        <div className="mb-6 relative" ref={dropdownRef}>
            <label id="asset-select-label" className="block text-sm font-medium text-gray-300 mb-2">
                Select Crypto Asset
            </label>
            
            <button
                type="button"
                className="w-full flex items-center justify-between px-4 py-3 rounded-lg bg-gray-800 border border-gray-700 text-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-colors"
                onClick={() => setIsOpen(!isOpen)}
                onKeyDown={(e) => handleKeyDown(e)}
                aria-haspopup="listbox"
                aria-expanded={isOpen}
                aria-labelledby="asset-select-label"
            >
                <div className="flex items-center gap-3">
                    <CoinIcon iconUrl={selectedAssetData.icon} name={selectedAssetData.name} ticker={selectedAssetData.ticker} />
                    <span className="font-medium text-left">{selectedAssetData.name} <span className="text-gray-400 font-normal">({selectedAssetData.ticker})</span></span>
                </div>
                <ChevronDown className={clsx("w-5 h-5 text-gray-400 transition-transform duration-200 shrink-0", isOpen && "transform rotate-180")} />
            </button>

            {isOpen && (
                <div 
                    className="absolute z-10 w-full mt-2 bg-gray-800 border border-gray-700 rounded-lg shadow-xl max-h-60 overflow-y-auto focus:outline-none"
                    role="listbox"
                    aria-labelledby="asset-select-label"
                >
                    {ASSETS.map((asset) => (
                        <div
                            key={asset.id}
                            role="option"
                            aria-selected={selectedAsset === asset.id}
                            tabIndex={0}
                            className={clsx(
                                "flex items-center gap-3 px-4 py-3 cursor-pointer transition-colors focus:outline-none",
                                selectedAsset === asset.id 
                                    ? "bg-blue-600/20 text-white" 
                                    : "text-gray-300 hover:bg-gray-700 focus:bg-gray-700"
                            )}
                            onClick={() => handleSelect(asset.id)}
                            onKeyDown={(e) => handleKeyDown(e, asset.id)}
                        >
                            <CoinIcon iconUrl={asset.icon} name={asset.name} ticker={asset.ticker} />
                            <span className="font-medium">{asset.name} <span className="text-gray-400 font-normal">({asset.ticker})</span></span>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
};
