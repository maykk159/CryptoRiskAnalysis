import React, { useState, useRef, useEffect } from 'react';
import { ASSETS } from '../constants/assets';
import { ChevronDown } from 'lucide-react';
import clsx from 'clsx';

interface AssetSelectorProps {
  selectedAsset: string;
  onSelectAsset: (asset: string) => void;
}

const CoinIcon: React.FC<{ iconUrl: string; name: string; ticker: string }> = ({
  iconUrl,
  name,
  ticker,
}) => {
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
  const selectedIndex = Math.max(
    0,
    ASSETS.findIndex(asset => asset.id === selectedAsset)
  );
  const [activeIndex, setActiveIndex] = useState(selectedIndex);
  const dropdownRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);
  const optionRefs = useRef<Array<HTMLDivElement | null>>([]);

  const selectedAssetData = ASSETS.find(a => a.id === selectedAsset) || ASSETS[0];

  useEffect(() => {
    if (isOpen) {
      optionRefs.current[activeIndex]?.focus();
    }
  }, [activeIndex, isOpen]);

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
    triggerRef.current?.focus();
  };

  const openDropdown = (index = selectedIndex) => {
    setActiveIndex(index);
    setIsOpen(true);
  };

  const closeDropdown = (restoreFocus: boolean) => {
    setIsOpen(false);
    if (restoreFocus) {
      triggerRef.current?.focus();
    }
  };

  const moveActiveOption = (offset: number) => {
    setActiveIndex(current => (current + offset + ASSETS.length) % ASSETS.length);
  };

  const handleTriggerKeyDown = (event: React.KeyboardEvent<HTMLButtonElement>) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (isOpen) {
        moveActiveOption(event.key === 'ArrowDown' ? 1 : -1);
      } else {
        openDropdown(selectedIndex);
      }
    } else if (event.key === 'Escape' && isOpen) {
      event.preventDefault();
      closeDropdown(true);
    }
  };

  const handleOptionKeyDown = (event: React.KeyboardEvent<HTMLDivElement>) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      moveActiveOption(event.key === 'ArrowDown' ? 1 : -1);
    } else if (event.key === 'Home') {
      event.preventDefault();
      setActiveIndex(0);
    } else if (event.key === 'End') {
      event.preventDefault();
      setActiveIndex(ASSETS.length - 1);
    } else if (event.key === 'Enter' || event.key === ' ') {
      event.preventDefault();
      handleSelect(ASSETS[activeIndex].id);
    } else if (event.key === 'Escape') {
      event.preventDefault();
      closeDropdown(true);
    } else if (event.key === 'Tab') {
      setIsOpen(false);
    }
  };

  return (
    <div className="mb-6 relative" ref={dropdownRef}>
      <label id="asset-select-label" className="block text-sm font-medium text-gray-300 mb-2">
        Select Crypto Asset
      </label>

      <button
        ref={triggerRef}
        type="button"
        className="w-full flex items-center justify-between px-4 py-3 rounded-lg bg-gray-800 border border-gray-700 text-white focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-transparent transition-colors"
        onClick={() => (isOpen ? closeDropdown(false) : openDropdown())}
        onKeyDown={handleTriggerKeyDown}
        aria-haspopup="listbox"
        aria-expanded={isOpen}
        aria-controls="asset-select-listbox"
        aria-labelledby="asset-select-label asset-select-value"
      >
        <div className="flex items-center gap-3">
          <CoinIcon
            iconUrl={selectedAssetData.icon}
            name={selectedAssetData.name}
            ticker={selectedAssetData.ticker}
          />
          <span id="asset-select-value" className="font-medium text-left">
            {selectedAssetData.name}{' '}
            <span className="text-gray-400 font-normal">({selectedAssetData.ticker})</span>
          </span>
        </div>
        <ChevronDown
          className={clsx(
            'w-5 h-5 text-gray-400 transition-transform duration-200 shrink-0',
            isOpen && 'transform rotate-180'
          )}
        />
      </button>

      {isOpen && (
        <div
          id="asset-select-listbox"
          className="absolute z-10 w-full mt-2 bg-gray-800 border border-gray-700 rounded-lg shadow-xl max-h-60 overflow-y-auto focus:outline-none"
          role="listbox"
          aria-labelledby="asset-select-label"
        >
          {ASSETS.map((asset, index) => (
            <div
              key={asset.id}
              id={`asset-option-${asset.id}`}
              ref={element => {
                optionRefs.current[index] = element;
              }}
              role="option"
              aria-selected={selectedAsset === asset.id}
              tabIndex={activeIndex === index ? 0 : -1}
              className={clsx(
                'flex items-center gap-3 px-4 py-3 cursor-pointer transition-colors focus:outline-none',
                selectedAsset === asset.id
                  ? 'bg-blue-600/20 text-white'
                  : 'text-gray-300 hover:bg-gray-700 focus:bg-gray-700'
              )}
              onClick={() => handleSelect(asset.id)}
              onKeyDown={handleOptionKeyDown}
            >
              <CoinIcon iconUrl={asset.icon} name={asset.name} ticker={asset.ticker} />
              <span className="font-medium">
                {asset.name} <span className="text-gray-400 font-normal">({asset.ticker})</span>
              </span>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};
