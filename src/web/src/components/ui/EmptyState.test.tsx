import { render, screen, fireEvent } from '@testing-library/react';
import EmptyState from './EmptyState';

describe('EmptyState', () => {
  it('renders title and message', () => {
    render(<EmptyState title="No videos" message="No videos found." />);
    expect(screen.getByText('No videos')).toBeInTheDocument();
    expect(screen.getByText('No videos found.')).toBeInTheDocument();
  });

  it('renders optional action button', () => {
    const action = jest.fn();
    render(
      <EmptyState
        title="Empty"
        message="Nothing here"
        action={{ label: 'Add item', onClick: action }}
      />,
    );
    const btn = screen.getByRole('button', { name: 'Add item' });
    expect(btn).toBeInTheDocument();
    fireEvent.click(btn);
    expect(action).toHaveBeenCalled();
  });

  it('does not render action button when not provided', () => {
    render(<EmptyState title="Empty" message="Nothing" />);
    expect(screen.queryByRole('button')).not.toBeInTheDocument();
  });
});
