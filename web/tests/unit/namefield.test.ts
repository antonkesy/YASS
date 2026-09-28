import { describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen } from '@testing-library/svelte';
import NameField from '$lib/components/NameField.svelte';

describe('NameField', () => {
	it('commits a typed name on blur', async () => {
		const onrename = vi.fn();
		render(NameField, { value: null, placeholder: '10:00', onrename });
		const input = screen.getByTestId('name-input');

		await fireEvent.focus(input);
		await fireEvent.input(input, { target: { value: 'Tax return 2025' } });
		await fireEvent.blur(input);

		expect(onrename).toHaveBeenCalledWith('Tax return 2025');
	});

	it('commits on Enter', async () => {
		const onrename = vi.fn();
		render(NameField, { value: null, placeholder: '10:00', onrename });
		const input = screen.getByTestId('name-input');

		await fireEvent.focus(input);
		await fireEvent.input(input, { target: { value: 'Invoice' } });
		await fireEvent.keyDown(input, { key: 'Enter' });
		await fireEvent.blur(input);

		expect(onrename).toHaveBeenCalledWith('Invoice');
	});

	it('trims, and says nothing when the name did not change', async () => {
		const onrename = vi.fn();
		render(NameField, { value: 'Invoice', placeholder: '10:00', onrename });
		const input = screen.getByTestId('name-input');

		await fireEvent.focus(input);
		await fireEvent.input(input, { target: { value: '  Invoice  ' } });
		await fireEvent.blur(input);

		expect(onrename).not.toHaveBeenCalled();
	});

	it('puts the stored name back on Escape', async () => {
		const onrename = vi.fn();
		render(NameField, { value: 'Invoice', placeholder: '10:00', onrename });
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
		render(NameField, { value: 'Invoice', placeholder: '10:00', onrename });
		const input = screen.getByTestId('name-input');

		await fireEvent.focus(input);
		await fireEvent.input(input, { target: { value: '   ' } });
		await fireEvent.blur(input);

		expect(onrename).toHaveBeenCalledWith('');
	});
});
