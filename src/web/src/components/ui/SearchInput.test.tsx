import { render, screen, fireEvent, act } from '@testing-library/react';
import SearchInput from './SearchInput';

describe('SearchInput', () => {
  beforeEach(() => {
    jest.useFakeTimers();
  });
  afterEach(() => {
    jest.useRealTimers();
  });

  it('renders an input element', () => {
    render(<SearchInput onChange={jest.fn()} />);
    expect(screen.getByRole('searchbox')).toBeInTheDocument();
  });

  it('debounces onChange by 300ms', () => {
    const onChange = jest.fn();
    render(<SearchInput onChange={onChange} debounceMs={300} />);
    const input = screen.getByRole('searchbox');
    fireEvent.change(input, { target: { value: 'hello' } });
    expect(onChange).not.toHaveBeenCalled();
    act(() => { jest.advanceTimersByTime(300); });
    expect(onChange).toHaveBeenCalledWith('hello');
  });

  it('shows clear button when value is present', () => {
    render(<SearchInput onChange={jest.fn()} defaultValue="test" />);
    expect(screen.getByRole('button', { name: /clear/i })).toBeInTheDocument();
  });

  it('does not show clear button when value is empty', () => {
    render(<SearchInput onChange={jest.fn()} />);
    expect(screen.queryByRole('button', { name: /clear/i })).not.toBeInTheDocument();
  });
});
