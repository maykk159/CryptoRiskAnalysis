import { useEffect, useId, useRef, useState, type KeyboardEvent } from 'react';
import { Check, ChevronDown, Search, X } from 'lucide-react';
import { ASSETS } from '../constants/assets';
import { CryptoAssetIcon } from './CryptoAssetIcon';

interface AssetSelectorProps {
  selectedAsset: string;
  onSelectAsset: (asset: string) => void;
}

export function AssetSelector({ selectedAsset, onSelectAsset }: AssetSelectorProps) {
  const [isOpen, setIsOpen] = useState(false);
  const [search, setSearch] = useState('');
  const [activeId, setActiveId] = useState(selectedAsset);
  const id = useId();
  const rootRef = useRef<HTMLDivElement>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const optionRefs = useRef<Map<string, HTMLDivElement>>(new Map());
  const selected = ASSETS.find(asset => asset.id === selectedAsset) ?? {
    id: selectedAsset,
    name: selectedAsset.charAt(0).toUpperCase() + selectedAsset.slice(1),
    ticker: selectedAsset.toUpperCase(),
    icon: '',
  };
  const query = search.trim().toLowerCase();
  const filtered = ASSETS.filter(asset =>
    [asset.name, asset.ticker, asset.id].some(value => value.toLowerCase().includes(query))
  );
  const active = filtered.find(asset => asset.id === activeId) ?? filtered[0];

  useEffect(() => {
    if (isOpen && active) optionRefs.current.get(active.id)?.scrollIntoView?.({ block: 'nearest' });
  }, [isOpen, active]);

  const open = () => {
    setSearch('');
    setActiveId(selectedAsset);
    setIsOpen(true);
  };
  const close = () => {
    setIsOpen(false);
    setSearch('');
  };
  const select = (assetId: string) => {
    onSelectAsset(assetId);
    close();
    inputRef.current?.focus();
  };
  const handleKeyDown = (event: KeyboardEvent<HTMLInputElement>) => {
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      if (!isOpen) {
        open();
        return;
      }
      if (!filtered.length) return;
      const index = filtered.findIndex(asset => asset.id === active?.id);
      setActiveId(
        filtered[(index + (event.key === 'ArrowDown' ? 1 : -1) + filtered.length) % filtered.length]
          .id
      );
    } else if (event.key === 'Enter' && isOpen && active) {
      event.preventDefault();
      select(active.id);
    } else if (event.key === 'Escape' && isOpen) {
      event.preventDefault();
      close();
    } else if (event.key === 'Tab') close();
  };

  return (
    <div
      ref={rootRef}
      className="relative min-w-0 flex-1"
      onBlur={event => {
        if (!event.currentTarget.contains(event.relatedTarget)) close();
      }}
    >
      <label htmlFor={id} className="field-label">
        Select Crypto Asset
      </label>
      <div className="flex min-h-[54px] items-center gap-3 rounded-lg border border-control bg-panel px-3 focus-within:border-accent">
        {isOpen ? (
          <Search size={20} className="shrink-0 text-muted" aria-hidden="true" />
        ) : (
          <CryptoAssetIcon asset={selected} />
        )}
        <input
          ref={inputRef}
          id={id}
          role="combobox"
          autoComplete="off"
          spellCheck={false}
          aria-expanded={isOpen}
          aria-controls={`${id}-listbox`}
          aria-autocomplete="list"
          aria-activedescendant={isOpen && active ? `${id}-${active.id}` : undefined}
          value={isOpen ? search : `${selected.name} (${selected.ticker})`}
          placeholder="Search name, ticker or asset ID"
          className="min-h-11 w-full min-w-0 bg-transparent text-sm text-ink placeholder:text-muted"
          onFocus={event => {
            if (!isOpen) event.currentTarget.select();
          }}
          onClick={() => {
            if (!isOpen) open();
          }}
          onChange={event => {
            setSearch(event.target.value);
            setIsOpen(true);
            setActiveId('');
          }}
          onKeyDown={handleKeyDown}
        />
        {isOpen && search ? (
          <button
            type="button"
            className="flex min-h-11 min-w-11 items-center justify-center rounded-md text-secondary hover:bg-raised"
            aria-label="Clear asset search"
            onClick={() => {
              setSearch('');
              setActiveId(selectedAsset);
              inputRef.current?.focus();
            }}
          >
            <X size={18} />
          </button>
        ) : (
          <button
            type="button"
            tabIndex={-1}
            aria-label={isOpen ? 'Close asset list' : 'Open asset list'}
            className="flex min-h-11 min-w-11 items-center justify-center rounded-md text-secondary hover:bg-raised"
            onClick={() => {
              if (isOpen) close();
              else open();
              inputRef.current?.focus();
            }}
          >
            <ChevronDown size={18} aria-hidden="true" />
          </button>
        )}
      </div>
      {isOpen && (
        <div className="absolute left-0 right-0 z-30 mt-2 rounded-xl border border-control bg-raised p-1 shadow-lg">
          <div
            id={`${id}-listbox`}
            role="listbox"
            aria-label="Crypto assets"
            className="max-h-72 overflow-y-auto overscroll-contain"
          >
            {filtered.map(asset => (
              <div
                key={asset.id}
                id={`${id}-${asset.id}`}
                role="option"
                aria-selected={selectedAsset === asset.id}
                ref={element => {
                  if (element) optionRefs.current.set(asset.id, element);
                  else optionRefs.current.delete(asset.id);
                }}
                onMouseDown={event => event.preventDefault()}
                onClick={() => select(asset.id)}
                className={`flex min-h-12 cursor-pointer items-center gap-3 rounded-lg px-3 py-2 ${active?.id === asset.id ? 'bg-selection ring-1 ring-inset ring-accent' : 'hover:bg-panel'}`}
              >
                <CryptoAssetIcon asset={asset} />
                <span className="min-w-0 flex-1 break-words font-medium">{asset.name}</span>
                <span className="max-w-[25%] break-words text-xs text-secondary">
                  {asset.ticker}
                </span>
                <span className="w-4 shrink-0">
                  {selectedAsset === asset.id && (
                    <Check size={16} className="text-accent" aria-hidden="true" />
                  )}
                </span>
              </div>
            ))}
          </div>
          {!filtered.length && (
            <p role="status" className="px-4 py-6 text-sm text-secondary">
              No assets found. Try another name or clear your search.
            </p>
          )}
          <p className="border-t border-line px-3 py-2 text-xs text-muted">
            Search locally · {filtered.length} assets
          </p>
        </div>
      )}
    </div>
  );
}
