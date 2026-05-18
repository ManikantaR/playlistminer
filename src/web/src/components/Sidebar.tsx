'use client';
import Link from 'next/link';
import { usePathname } from 'next/navigation';
import { clsx } from 'clsx';
import {
  LayoutDashboard,
  Video,
  Tags,
  ListMusic,
  Lightbulb,
  Upload,
  RotateCcw,
  Settings,
} from 'lucide-react';

const links = [
  { href: '/', label: 'Dashboard', icon: LayoutDashboard },
  { href: '/videos', label: 'Videos', icon: Video },
  { href: '/suggestions', label: 'Suggestions', icon: Lightbulb },
  { href: '/playlists', label: 'Playlists', icon: ListMusic },
  { href: '/tags', label: 'Tags', icon: Tags },
  { href: '/import', label: 'Import', icon: Upload },
  { href: '/undo', label: 'Undo', icon: RotateCcw },
  { href: '/settings', label: 'Settings', icon: Settings },
];

export default function Sidebar() {
  const pathname = usePathname();
  return (
    <aside className="w-56 flex-shrink-0 bg-white dark:bg-gray-900 border-r border-gray-200 dark:border-gray-700 flex flex-col">
      <div className="px-6 py-5 border-b border-gray-200 dark:border-gray-700">
        <span className="font-bold text-lg text-blue-600 dark:text-blue-400">PlaylistMiner</span>
      </div>
      <nav className="flex-1 py-4 flex flex-col gap-1 px-3">
        {links.map(({ href, label, icon: Icon }) => (
          <Link
            key={href}
            href={href}
            className={clsx(
              'flex items-center gap-3 px-3 py-2 rounded-md text-sm font-medium transition-colors',
              pathname === href
                ? 'bg-blue-50 text-blue-700 dark:bg-blue-900/30 dark:text-blue-300'
                : 'text-gray-600 hover:bg-gray-100 dark:text-gray-400 dark:hover:bg-gray-800',
            )}
          >
            <Icon className="w-4 h-4" />
            {label}
          </Link>
        ))}
      </nav>
    </aside>
  );
}
