import { render, screen, fireEvent } from '@testing-library/react';
import Badge from './Badge';

describe('Badge', () => {
  it('renders label text', () => {
    render(<Badge label="Science" source="Manual" />);
    expect(screen.getByText('Science')).toBeInTheDocument();
  });

  it('applies green color for Manual source', () => {
    render(<Badge label="Test" source="Manual" />);
    const badge = screen.getByText('Test').closest('span');
    expect(badge?.className).toMatch(/green/);
  });

  it('applies blue color for RuleBased source', () => {
    render(<Badge label="Test" source="RuleBased" />);
    const badge = screen.getByText('Test').closest('span');
    expect(badge?.className).toMatch(/blue/);
  });

  it('applies purple color for TfIdf source', () => {
    render(<Badge label="Test" source="TfIdf" />);
    const badge = screen.getByText('Test').closest('span');
    expect(badge?.className).toMatch(/purple/);
  });

  it('applies orange color for Ollama source', () => {
    render(<Badge label="Test" source="Ollama" />);
    const badge = screen.getByText('Test').closest('span');
    expect(badge?.className).toMatch(/orange/);
  });

  it('applies dashed border for Suggested source', () => {
    render(<Badge label="Test" source="Suggested" />);
    const badge = screen.getByText('Test').closest('span');
    expect(badge?.className).toMatch(/dashed/);
  });

  it('shows confidence percentage when provided', () => {
    render(<Badge label="Test" source="TfIdf" confidence={0.85} />);
    expect(screen.getByText(/85%/)).toBeInTheDocument();
  });

  it('renders remove button when onRemove provided', () => {
    const onRemove = jest.fn();
    render(<Badge label="Test" source="Manual" onRemove={onRemove} />);
    const removeBtn = screen.getByRole('button', { name: /remove/i });
    expect(removeBtn).toBeInTheDocument();
    fireEvent.click(removeBtn);
    expect(onRemove).toHaveBeenCalledTimes(1);
  });
});
