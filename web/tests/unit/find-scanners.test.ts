import { beforeEach, describe, expect, it, vi } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/svelte';
import { get } from 'svelte/store';
import FindScannersButton from '$lib/components/FindScannersButton.svelte';
import { scannerPrefs } from '$lib/presets';
import { toast } from '$lib/toast';

const refreshDevices = vi.fn();
vi.mock('$lib/hub/hub', () => ({ getHub: () => ({ hub: { refreshDevices } }) }));

describe('FindScannersButton', () => {
	beforeEach(() => {
		refreshDevices.mockReset();
		toast.dismiss();
	});

	it('looks again, hands back what it found and remembers it', async () => {
		const found = [{ Id: 'epson', Name: 'Epson DS-530', Driver: 'sane' }];
		refreshDevices.mockResolvedValue(found);
		const onfound = vi.fn();
		render(FindScannersButton, { onfound });

		await fireEvent.click(screen.getByTestId('find-scanners'));

		await waitFor(() => expect(onfound).toHaveBeenCalledWith(found));
		expect(get(scannerPrefs).known.epson).toBeDefined();
		expect(get(toast)?.message).toBe('Found 1 scanner');
		expect(screen.getByTestId('find-scanners')).toBeEnabled();
	});

	it('says when there is still nothing, and when the call fails', async () => {
		refreshDevices.mockResolvedValueOnce([]);
		render(FindScannersButton, { onfound: vi.fn() });
		await fireEvent.click(screen.getByTestId('find-scanners'));
		await waitFor(() => expect(get(toast)?.message).toBe('No scanner found'));

		refreshDevices.mockRejectedValueOnce(new Error('SANE crashed'));
		await fireEvent.click(screen.getByTestId('find-scanners'));
		await waitFor(() => expect(get(toast)?.message).toMatch(/SANE crashed|went wrong/i));
	});

	it('shows it is looking and does not start a second look meanwhile', async () => {
		let finish: (v: unknown[]) => void = () => {};
		refreshDevices.mockReturnValue(new Promise((r) => (finish = r)));
		render(FindScannersButton, { onfound: vi.fn() });
		const button = screen.getByTestId('find-scanners');

		await fireEvent.click(button);
		expect(button).toBeDisabled();
		expect(button).toHaveTextContent('Looking…');
		await fireEvent.click(button);
		expect(refreshDevices).toHaveBeenCalledTimes(1);

		finish([]);
		await waitFor(() => expect(button).toHaveTextContent('Look for scanners'));
	});
});
