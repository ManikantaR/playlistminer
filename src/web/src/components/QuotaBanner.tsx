'use client';
import { useQuotaStatus } from '@/hooks/useQuota';

export default function QuotaBanner() {
  const { data } = useQuotaStatus();

  if (!data?.isExhausted) return null;

  const resetsAt = new Date(data.resetsAt).toLocaleString();

  return (
    <div className="bg-amber-50 dark:bg-amber-900/30 border border-amber-300 dark:border-amber-700 text-amber-800 dark:text-amber-200 px-4 py-3 text-sm flex items-center gap-2">
      <svg className="w-5 h-5 shrink-0" fill="none" viewBox="0 0 24 24" strokeWidth={1.5} stroke="currentColor">
        <path strokeLinecap="round" strokeLinejoin="round" d="M12 9v3.75m-9.303 3.376c-.866 1.5.217 3.374 1.948 3.374h14.71c1.73 0 2.813-1.874 1.948-3.374L13.949 3.378c-.866-1.5-3.032-1.5-3.898 0L2.697 16.126ZM12 15.75h.007v.008H12v-.008Z" />
      </svg>
      <span>
        <strong>YouTube API quota exhausted.</strong> Syncs and imports are paused until the quota resets at {resetsAt}.
      </span>
    </div>
  );
}
