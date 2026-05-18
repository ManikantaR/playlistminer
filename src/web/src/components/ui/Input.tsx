import { InputHTMLAttributes } from 'react';
import { clsx } from 'clsx';

interface Props extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  error?: string;
}

export default function Input({ label, error, className, id, ...props }: Props) {
  return (
    <div className="flex flex-col gap-1">
      {label && (
        <label htmlFor={id} className="text-sm font-medium text-gray-700 dark:text-gray-300">
          {label}
        </label>
      )}
      <input
        id={id}
        className={clsx(
          'px-3 py-2 rounded border text-sm focus:outline-none focus:ring-2 focus:ring-blue-500',
          error
            ? 'border-red-400 focus:ring-red-500'
            : 'border-gray-300 dark:border-gray-600 dark:bg-gray-800 dark:text-white',
          className,
        )}
        {...props}
      />
      {error && <p className="text-xs text-red-500">{error}</p>}
    </div>
  );
}
