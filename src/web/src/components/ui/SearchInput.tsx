'use client';
import { useState, useEffect, useRef } from 'react';
import { Search, X } from 'lucide-react';

interface Props {
  onChange: (value: string) => void;
  defaultValue?: string;
  placeholder?: string;
  debounceMs?: number;
}

export default function SearchInput({ onChange, defaultValue = '', placeholder = 'Search…', debounceMs = 300 }: Props) {
  const [value, setValue] = useState(defaultValue);
  const timerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    if (timerRef.current) clearTimeout(timerRef.current);
    timerRef.current = setTimeout(() => onChange(value), debounceMs);
    return () => { if (timerRef.current) clearTimeout(timerRef.current); };
  }, [value, debounceMs, onChange]);

  return (
    <div className="relative flex items-center">
      <Search className="absolute left-3 w-4 h-4 text-gray-400" aria-hidden />
      <input
        role="searchbox"
        type="search"
        value={value}
        placeholder={placeholder}
        onChange={(e) => setValue(e.target.value)}
        className="w-full pl-9 pr-8 py-2 rounded border border-gray-300 bg-white dark:bg-gray-800 dark:border-gray-600 dark:text-white focus:outline-none focus:ring-2 focus:ring-blue-500 text-sm"
      />
      {value && (
        <button
          type="button"
          aria-label="Clear search"
          onClick={() => setValue('')}
          className="absolute right-2 text-gray-400 hover:text-gray-600 focus:outline-none"
        >
          <X className="w-4 h-4" />
        </button>
      )}
    </div>
  );
}
