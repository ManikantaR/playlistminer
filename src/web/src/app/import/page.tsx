'use client';
import { useState, useCallback } from 'react';
import { API_BASE } from '@/lib/api-client';
import Button from '@/components/ui/Button';
import Card from '@/components/ui/Card';
import { Upload, FileText } from 'lucide-react';
import toast from 'react-hot-toast';
import type { ImportResult } from '@/types';

export default function ImportPage() {
  const [file, setFile] = useState<File | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [isUploading, setIsUploading] = useState(false);
  const [result, setResult] = useState<ImportResult | null>(null);

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault();
    setIsDragging(false);
    const dropped = e.dataTransfer.files[0];
    if (dropped?.name.endsWith('.csv')) setFile(dropped);
    else toast.error('Please drop a .csv file');
  }, []);

  const handleUpload = async () => {
    if (!file) return;
    setIsUploading(true);
    try {
      const form = new FormData();
      form.append('file', file);
      const res = await fetch(`${API_BASE}/api/import/takeout`, {
        method: 'POST',
        body: form,
      });
      if (!res.ok) throw new Error(`Upload failed: ${res.status}`);
      const data = (await res.json()) as ImportResult;
      setResult(data);
      toast.success('Import completed!');
    } catch {
      toast.error('Import failed. Please try again.');
    } finally {
      setIsUploading(false);
    }
  };

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      <h1 className="text-2xl font-bold">Import</h1>

      <Card>
        <h2 className="text-lg font-semibold mb-4">Import YouTube Takeout CSV</h2>
        <div
          onDragOver={(e) => { e.preventDefault(); setIsDragging(true); }}
          onDragLeave={() => setIsDragging(false)}
          onDrop={handleDrop}
          className={`border-2 border-dashed rounded-lg p-10 text-center transition-colors ${
            isDragging
              ? 'border-blue-500 bg-blue-50 dark:bg-blue-900/20'
              : 'border-gray-300 dark:border-gray-600'
          }`}
        >
          <Upload className="w-10 h-10 mx-auto mb-3 text-gray-400" />
          {file ? (
            <div className="flex items-center justify-center gap-2">
              <FileText className="w-5 h-5 text-blue-600" />
              <span className="text-sm font-medium">{file.name}</span>
            </div>
          ) : (
            <p className="text-gray-500 text-sm">Drag and drop a CSV file here, or</p>
          )}
          <label className="mt-3 inline-block cursor-pointer">
            <span className="text-blue-600 hover:underline text-sm">Browse file</span>
            <input
              type="file"
              accept=".csv"
              className="sr-only"
              onChange={(e) => {
                const f = e.target.files?.[0];
                if (f) setFile(f);
              }}
            />
          </label>
        </div>
        <div className="mt-4 flex justify-end">
          <Button onClick={handleUpload} disabled={!file || isUploading}>
            {isUploading ? 'Importing…' : 'Import'}
          </Button>
        </div>
      </Card>

      {result && (
        <Card>
          <h2 className="text-lg font-semibold mb-4">Import Results</h2>
          <dl className="grid grid-cols-2 gap-4">
            <div>
              <dt className="text-sm text-gray-500">Videos Found</dt>
              <dd className="text-2xl font-bold">{result.videosFound}</dd>
            </div>
            <div>
              <dt className="text-sm text-gray-500">Imported</dt>
              <dd className="text-2xl font-bold text-green-600">{result.videosImported}</dd>
            </div>
            <div>
              <dt className="text-sm text-gray-500">Skipped</dt>
              <dd className="text-2xl font-bold text-yellow-600">{result.skipped}</dd>
            </div>
            <div>
              <dt className="text-sm text-gray-500">Errors</dt>
              <dd className="text-2xl font-bold text-red-600">{result.errors}</dd>
            </div>
          </dl>
          <p className="text-xs text-gray-400 mt-3">Batch ID: {result.batchId}</p>
        </Card>
      )}
    </div>
  );
}
