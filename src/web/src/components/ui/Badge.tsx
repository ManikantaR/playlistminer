import { clsx } from 'clsx';
import type { TagSuggestion } from '@/types';

interface Props {
  label: string;
  source: TagSuggestion['source'];
  confidence?: number | null;
  onRemove?: () => void;
}

const sourceStyles: Record<TagSuggestion['source'], string> = {
  Manual: 'bg-green-100 text-green-800 border-green-300 dark:bg-green-900 dark:text-green-200',
  RuleBased: 'bg-blue-100 text-blue-800 border-blue-300 dark:bg-blue-900 dark:text-blue-200',
  TfIdf: 'bg-purple-100 text-purple-800 border-purple-300 dark:bg-purple-900 dark:text-purple-200',
  Ollama: 'bg-orange-100 text-orange-800 border-orange-300 dark:bg-orange-900 dark:text-orange-200',
  Gemini: 'bg-cyan-100 text-cyan-800 border-cyan-300 dark:bg-cyan-900 dark:text-cyan-200',
  OpenAI: 'bg-emerald-100 text-emerald-800 border-emerald-300 dark:bg-emerald-900 dark:text-emerald-200',
  Suggested: 'bg-gray-100 text-gray-700 border-gray-400 border-dashed dark:bg-gray-800 dark:text-gray-300',
};

export default function Badge({ label, source, confidence, onRemove }: Props) {
  return (
    <span
      className={clsx(
        'inline-flex items-center gap-1 px-2 py-0.5 rounded text-xs font-medium border',
        sourceStyles[source],
      )}
    >
      {label}
      {confidence != null && (
        <span className="opacity-70">{Math.round(confidence * 100)}%</span>
      )}
      {onRemove && (
        <button
          type="button"
          aria-label={`Remove ${label}`}
          onClick={onRemove}
          className="ml-0.5 hover:opacity-70 focus:outline-none"
        >
          ×
        </button>
      )}
    </span>
  );
}
