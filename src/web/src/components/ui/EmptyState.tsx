import { Inbox } from 'lucide-react';
import Button from './Button';

interface Props {
  title: string;
  message: string;
  action?: { label: string; onClick: () => void };
}

export default function EmptyState({ title, message, action }: Props) {
  return (
    <div className="flex flex-col items-center justify-center py-16 text-center">
      <Inbox className="w-12 h-12 text-gray-300 dark:text-gray-600 mb-4" aria-hidden />
      <h3 className="text-lg font-semibold text-gray-700 dark:text-gray-300 mb-2">{title}</h3>
      <p className="text-gray-500 dark:text-gray-400 mb-6">{message}</p>
      {action && (
        <Button onClick={action.onClick}>{action.label}</Button>
      )}
    </div>
  );
}
