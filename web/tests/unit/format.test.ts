import { describe, expect, it } from 'vitest';
import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
import { activePageCount, formatTime, scannerStateLabel, sessionStateLabel } from '$lib/format';

describe('format', () => {
	it('labels states', () => {
		expect(sessionStateLabel(SessionState.Review)).toBe('Ready to review');
		expect(sessionStateLabel(SessionState.Failed)).toBe('Failed');
		expect(scannerStateLabel(ScannerState.Ready)).toBe('Ready');
		expect(scannerStateLabel(undefined)).toBe('Checking…');
	});

	it('counts active pages', () => {
		expect(
			activePageCount({
				Id: '',
				CreatedAt: '',
				State: SessionState.Review,
				OptionsUsed: {},
				Pages: [
					{ Index: 0, Rotation: 0, Deleted: true, IsBlank: true, Hash: '', ThumbnailReady: false },
					{ Index: 1, Rotation: 0, Deleted: false, IsBlank: false, Hash: '', ThumbnailReady: false }
				]
			})
		).toBe(1);
	});

	it('shows only the time for today and the date otherwise', () => {
		const now = new Date('2026-09-24T12:00:00');
		expect(formatTime('2026-09-24T09:05:00', now)).not.toMatch(/Sep/);
		expect(formatTime('2026-09-20T09:05:00', now)).toMatch(/Sep|20/);
		expect(formatTime('garbage', now)).toBe('garbage');
	});
});
