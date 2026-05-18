import { clsx } from 'clsx';

interface Props {
  children: React.ReactNode;
  className?: string;
}

export default function Card({ children, className }: Props) {
  return (
    <div className={clsx('bg-white dark:bg-gray-800 rounded-lg shadow p-6', className)}>
      {children}
    </div>
  );
}
