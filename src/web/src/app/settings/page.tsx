'use client';
import { useSyncStatus } from '@/hooks/useSync';
import Card from '@/components/ui/Card';
import { useState } from 'react';

export default function SettingsPage() {
  const { data: syncStatus } = useSyncStatus();
  const [tfIdfThreshold, setTfIdfThreshold] = useState(0.3);
  const [ollamaThreshold, setOllamaThreshold] = useState(0.5);
  const [autoAcceptThreshold, setAutoAcceptThreshold] = useState(0.9);

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <h1 className="text-2xl font-bold">Settings</h1>

      <Card>
        <h2 className="text-lg font-semibold mb-3">YouTube Connection</h2>
        <div className="flex items-center gap-3">
          <div
            className={`w-3 h-3 rounded-full ${
              syncStatus ? 'bg-green-500' : 'bg-gray-300'
            }`}
          />
          <span className="text-sm">
            {syncStatus ? 'Connected' : 'Not connected'}
          </span>
        </div>
        {syncStatus?.lastSync && (
          <p className="text-sm text-gray-500 mt-2">
            Last sync: {new Date(syncStatus.lastSync).toLocaleString()}
          </p>
        )}
      </Card>

      <Card>
        <h2 className="text-lg font-semibold mb-3">Sync Schedule</h2>
        <p className="text-sm text-gray-600 dark:text-gray-400">
          Syncs run automatically via the background worker. Configure the schedule in
          your <code className="bg-gray-100 dark:bg-gray-700 px-1 rounded">appsettings.json</code>.
        </p>
      </Card>

      <Card>
        <h2 className="text-lg font-semibold mb-4">Confidence Thresholds</h2>
        <div className="space-y-5">
          <div>
            <label className="flex justify-between text-sm mb-1">
              <span>TF-IDF Threshold</span>
              <span className="font-medium">{tfIdfThreshold.toFixed(2)}</span>
            </label>
            <input
              type="range"
              min={0}
              max={1}
              step={0.05}
              value={tfIdfThreshold}
              onChange={(e) => setTfIdfThreshold(Number(e.target.value))}
              className="w-full"
              aria-label="TF-IDF threshold"
            />
          </div>
          <div>
            <label className="flex justify-between text-sm mb-1">
              <span>Ollama Threshold</span>
              <span className="font-medium">{ollamaThreshold.toFixed(2)}</span>
            </label>
            <input
              type="range"
              min={0}
              max={1}
              step={0.05}
              value={ollamaThreshold}
              onChange={(e) => setOllamaThreshold(Number(e.target.value))}
              className="w-full"
              aria-label="Ollama threshold"
            />
          </div>
          <div>
            <label className="flex justify-between text-sm mb-1">
              <span>Auto-Accept Threshold</span>
              <span className="font-medium">{autoAcceptThreshold.toFixed(2)}</span>
            </label>
            <input
              type="range"
              min={0}
              max={1}
              step={0.05}
              value={autoAcceptThreshold}
              onChange={(e) => setAutoAcceptThreshold(Number(e.target.value))}
              className="w-full"
              aria-label="Auto-accept threshold"
            />
          </div>
        </div>
      </Card>
    </div>
  );
}
