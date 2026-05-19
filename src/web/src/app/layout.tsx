import type { Metadata } from 'next';
import './globals.css';
import Providers from './providers';
import Sidebar from '@/components/Sidebar';
import DarkModeToggle from '@/components/DarkModeToggle';
import QuotaBanner from '@/components/QuotaBanner';

export const metadata: Metadata = {
  title: 'PlaylistMiner',
  description: 'YouTube playlist management and tagging',
};

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="en" suppressHydrationWarning>
      <body className="bg-gray-50 dark:bg-gray-950 text-gray-900 dark:text-gray-100 min-h-screen">
        <Providers>
          <div className="flex h-screen overflow-hidden">
            <Sidebar />
            <div className="flex-1 flex flex-col overflow-hidden">
              <header className="h-14 flex items-center justify-end px-6 border-b border-gray-200 dark:border-gray-700 bg-white dark:bg-gray-900">
                <DarkModeToggle />
              </header>
              <QuotaBanner />
              <main className="flex-1 overflow-y-auto p-6">{children}</main>
            </div>
          </div>
        </Providers>
      </body>
    </html>
  );
}
