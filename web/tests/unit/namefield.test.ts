import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/svelte';
import NameField from '$lib/components/NameField.svelte';

describe('NameField', () => {
	it('commits a typed name on blur', async () => {
		const onrename = vi.fn();
		render(NameField, { value: null, placeholder: '10:00', onrename, onsuggest: vi.fn() });
		const input = screen.getByTestId('name-input');

		await fireEvent.focus(input);
		await fireEvent.input(input, { target: { value: 'Tax return 2025' } });
		await fireEvent.blur(input);

		expect(onrename).toHaveBeenCalledWith('Tax return 2025');
	});

	it('commits on Enter', async () => {
		const onrename = vi.fn();
		render(NameField, { value: null, placeholder: '10:00', onrename, onsuggest: vi.fn() });
		const input = screen.getByTestId('name-input');

		await fireEvent.focus(input);
		await fireEvent.input(input, { target: { value: 'Invoice' } });
		await fireEvent.keyDown(input, { key: 'Enter' });
		await fireEvent.blur(input);

		expect(onrename).toHaveBeenCalledWith('Invoice');
	});

	it('trims, and says nothing when the name did not change', async () => {
		const onrename = vi.fn();
		render(NameField, { value: 'Invoice', placeholder: '10:00', onrename, onsuggest: vi.fn() });
		const input = screen.getByTestId('name-input');

		await fireEvent.focus(input);
		await fireEvent.input(input, { target: { value: '  Invoice  ' } });
		await fireEvent.blur(input);

		expect(onrename).not.toHaveBeenCalled();
	});

	it('puts the stored name back on Escape', async () => {
		const onrename = vi.fn();
		render(NameField, { value: 'Invoice', placeholder: '10:00', onrename, onsuggest: vi.fn() });
		const input = screen.getByTestId('name-input');

		await fireEvent.focus(input);
		await fireEvent.input(input, { target: { value: 'Something else' } });
		await fireEvent.keyDown(input, { key: 'Escape' });
		await fireEvent.blur(input);

		expect(onrename).not.toHaveBeenCalled();
		expect(input).toHaveValue('Invoice');
	});

	it('clearing the name commits an empty string, which falls back to the timestamp', async () => {
		const onrename = vi.fn();
		render(NameField, { value: 'Invoice', placeholder: '10:00', onrename, onsuggest: vi.fn() });
		const input = screen.getByTestId('name-input');

		await fireEvent.focus(input);
		await fireEvent.input(input, { target: { value: '   ' } });
		await fireEvent.blur(input);

		expect(onrename).toHaveBeenCalledWith('');
	});

	it('fills the sparkle as the weights come down, and says how far in the tooltip', () => {
		const { unmount } = render(NameField, {
			value: 'Invoice',
			placeholder: '10:00',
			busy: true,
			percent: 30,
			onrename: vi.fn(),
			onsuggest: vi.fn()
		});

		expect(screen.getByTestId('name-suggest-progress')).toHaveStyle({ height: '30%' });
		expect(screen.getByTestId('name-suggest')).toHaveAttribute(
			'title',
			'Downloading the model… 30%'
		);
		unmount();

		// A wait that is not a download has nothing to fill with, so the button only pulses.
		render(NameField, {
			value: 'Invoice',
			placeholder: '10:00',
			busy: true,
			onrename: vi.fn(),
			onsuggest: vi.fn()
		});
		expect(screen.queryByTestId('name-suggest-progress')).toBeNull();
	});

	it('shows the slot the template left, and asks for a name when it is clicked', async () => {
		const onsuggest = vi.fn();
		render(NameField, {
			value: '2026-09-24',
			placeholder: '10:00',
			generated: { before: '2026-09-24_', generated: '', after: '', filled: false },
			onrename: vi.fn(),
			onsuggest
		});

		const slot = screen.getByTestId('name-generated');
		expect(slot).toHaveTextContent('name it');

		await fireEvent.click(slot);
		expect(onsuggest).toHaveBeenCalled();
	});

	it('drops the slot along with the sparkle when a name cannot be asked for', () => {
		// The chip and the sparkle are one feature in two places: both are the same click. A
		// slot left behind where Find name is off would be an invitation nothing can honour.
		render(NameField, {
			value: '2026-09-24',
			placeholder: '10:00',
			generated: null,
			canSuggest: false,
			onrename: vi.fn(),
			onsuggest: vi.fn()
		});

		expect(screen.queryByTestId('name-generated')).toBeNull();
		expect(screen.queryByTestId('name-suggest')).toBeNull();
		expect(screen.getByTestId('name-input')).toHaveValue('2026-09-24');
	});
});
