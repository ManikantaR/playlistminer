'use client';
import { useState } from 'react';
import { useTags, useTagRules } from '@/hooks/useTags';
import { useCreateTag, useDeleteTag, useCreateTagRule, useDeleteTagRule } from '@/hooks/useMutations';
import Button from '@/components/ui/Button';
import EmptyState from '@/components/ui/EmptyState';
import { ChevronDown, ChevronRight, Plus, Trash2 } from 'lucide-react';
import toast from 'react-hot-toast';
import type { Tag } from '@/types';

function TagRulesRow({ tag }: { tag: Tag }) {
  const { data: rules } = useTagRules(tag.id);
  const createRule = useCreateTagRule();
  const deleteRule = useDeleteTagRule();
  const [keyword, setKeyword] = useState('');
  const [field, setField] = useState<'Title' | 'Description' | 'Both'>('Title');

  const handleAddRule = async () => {
    if (!keyword.trim()) return;
    try {
      await createRule.mutateAsync({ tagId: tag.id, data: { keyword, field, weight: 1 } });
      setKeyword('');
      toast.success('Rule added');
    } catch {
      toast.error('Failed to add rule');
    }
  };

  return (
    <div className="pl-6 pt-2 space-y-2">
      {rules && rules.length > 0 ? (
        <table className="w-full text-xs">
          <thead>
            <tr className="text-gray-500">
              <th className="text-left py-1">Keyword</th>
              <th className="text-left py-1">Field</th>
              <th className="text-left py-1">Weight</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rules.map((rule) => (
              <tr key={rule.id} className="border-t border-gray-100 dark:border-gray-700">
                <td className="py-1">{rule.keyword}</td>
                <td className="py-1">{rule.field}</td>
                <td className="py-1">{rule.weight}</td>
                <td className="py-1">
                  <button
                    aria-label="Delete rule"
                    onClick={() =>
                      deleteRule.mutate(
                        { tagId: tag.id, ruleId: rule.id },
                        { onSuccess: () => toast.success('Rule deleted') },
                      )
                    }
                    className="text-red-400 hover:text-red-600"
                  >
                    <Trash2 className="w-3 h-3" />
                  </button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : (
        <p className="text-xs text-gray-400">No rules.</p>
      )}
      <div className="flex gap-2 items-center flex-wrap">
        <input
          value={keyword}
          onChange={(e) => setKeyword(e.target.value)}
          placeholder="Keyword"
          className="px-2 py-1 text-xs rounded border border-gray-300 dark:border-gray-600 dark:bg-gray-700"
        />
        <select
          value={field}
          onChange={(e) => setField(e.target.value as 'Title' | 'Description' | 'Both')}
          className="px-2 py-1 text-xs rounded border border-gray-300 dark:border-gray-600 dark:bg-gray-700"
        >
          <option value="Title">Title</option>
          <option value="Description">Description</option>
          <option value="Both">Both</option>
        </select>
        <Button size="sm" variant="secondary" onClick={handleAddRule}>
          <Plus className="w-3 h-3 mr-1" /> Add
        </Button>
      </div>
    </div>
  );
}

export default function TagsPage() {
  const { data: tags, isLoading } = useTags();
  const createTag = useCreateTag();
  const deleteTag = useDeleteTag();
  const [expanded, setExpanded] = useState<number[]>([]);
  const [newName, setNewName] = useState('');
  const [newCategory, setNewCategory] = useState('');

  const grouped = tags?.reduce<Record<string, Tag[]>>((acc, tag) => {
    const cat = tag.category ?? 'Uncategorized';
    (acc[cat] ??= []).push(tag);
    return acc;
  }, {}) ?? {};

  const handleCreate = async () => {
    if (!newName.trim()) return;
    try {
      await createTag.mutateAsync({ name: newName, category: newCategory || undefined });
      setNewName('');
      setNewCategory('');
      toast.success('Tag created');
    } catch {
      toast.error('Failed to create tag');
    }
  };

  const toggleExpand = (id: number) =>
    setExpanded((prev) => (prev.includes(id) ? prev.filter((i) => i !== id) : [...prev, id]));

  if (isLoading) return <div className="animate-pulse text-gray-500 p-8">Loading…</div>;
  if (!tags || tags.length === 0) {
    return (
      <div className="max-w-3xl mx-auto space-y-4">
        <h1 className="text-2xl font-bold">Tags</h1>
        <EmptyState title="No tags" message="Create your first tag below." />
      </div>
    );
  }

  return (
    <div className="max-w-3xl mx-auto space-y-6">
      <h1 className="text-2xl font-bold">Tags</h1>

      <div className="bg-white dark:bg-gray-800 rounded-lg shadow p-4 flex gap-2 flex-wrap">
        <input
          value={newName}
          onChange={(e) => setNewName(e.target.value)}
          placeholder="Tag name"
          className="px-3 py-2 text-sm rounded border border-gray-300 dark:border-gray-600 dark:bg-gray-700 flex-1 min-w-32"
        />
        <input
          value={newCategory}
          onChange={(e) => setNewCategory(e.target.value)}
          placeholder="Category (optional)"
          className="px-3 py-2 text-sm rounded border border-gray-300 dark:border-gray-600 dark:bg-gray-700 flex-1 min-w-32"
        />
        <Button onClick={handleCreate} size="sm">
          <Plus className="w-4 h-4 mr-1" /> Create Tag
        </Button>
      </div>

      {Object.entries(grouped).map(([category, categoryTags]) => (
        <div key={category} className="bg-white dark:bg-gray-800 rounded-lg shadow overflow-hidden">
          <h2 className="px-4 py-3 font-semibold text-gray-600 dark:text-gray-300 bg-gray-50 dark:bg-gray-700/50 border-b dark:border-gray-700">
            {category}
          </h2>
          <div className="divide-y divide-gray-100 dark:divide-gray-700">
            {categoryTags.map((tag) => (
              <div key={tag.id}>
                <div className="flex items-center gap-3 px-4 py-3">
                  <button
                    onClick={() => toggleExpand(tag.id)}
                    aria-label={expanded.includes(tag.id) ? 'Collapse rules' : 'Expand rules'}
                    className="text-gray-400 hover:text-gray-600"
                  >
                    {expanded.includes(tag.id) ? (
                      <ChevronDown className="w-4 h-4" />
                    ) : (
                      <ChevronRight className="w-4 h-4" />
                    )}
                  </button>
                  <span className="flex-1 font-medium text-sm">{tag.name}</span>
                  <span className="text-xs text-gray-400">{tag.videoCount} videos</span>
                  <button
                    aria-label={`Delete tag ${tag.name}`}
                    onClick={() =>
                      deleteTag.mutate(tag.id, {
                        onSuccess: () => toast.success('Tag deleted'),
                        onError: () => toast.error('Failed to delete'),
                      })
                    }
                    className="text-red-400 hover:text-red-600 ml-2"
                  >
                    <Trash2 className="w-4 h-4" />
                  </button>
                </div>
                {expanded.includes(tag.id) && <TagRulesRow tag={tag} />}
              </div>
            ))}
          </div>
        </div>
      ))}
    </div>
  );
}
