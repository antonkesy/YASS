import { describe, expect, it } from 'vitest';
import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import { page, session } from './fixtures';
import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
import {
	activePageCount,
	folderName,
	formatTime,
	scannerStateLabel,
	sessionStateLabel
} from '$lib/format';

describe('format', () => {
	it('labels states', () => {
		expect(sessionStateLabel(SessionState.Review)).toBe('Ready to review');
		expect(sessionStateLabel(SessionState.Failed)).toBe('Failed');
		expect(scannerStateLabel(ScannerState.Ready)).toBe('Ready');
		expect(scannerStateLabel(undefined)).toBe('Checking…');
	});

	it('counts active pages', () => {
		expect(
			activePageCount(
				session('s', {
					Pages: [page(0, { Deleted: true, IsBlank: true }), page(1)]
				})
			)
		).toBe(1);
	});

	it('shows only the time for today and the date otherwise', () => {
		const now = new Date('2026-09-24T12:00:00');
		expect(formatTime('2026-09-24T09:05:00', now)).not.toMatch(/Sep/);
		expect(formatTime('2026-09-20T09:05:00', now)).toMatch(/Sep|20/);
		expect(formatTime('garbage', now)).toBe('garbage');
	});
});

describe('folderName', () => {
	it('is the last segment of a path, whichever way the slashes lean', () => {
		expect(folderName('/srv/paperless/consume')).toBe('consume');
		expect(folderName('/srv/paperless/consume/')).toBe('consume');
		expect(folderName('C:\\scans\\inbox')).toBe('inbox');
	});

	it('falls back to the path itself when there is no segment to pick', () => {
		expect(folderName('/')).toBe('/');
		expect(folderName('')).toBe('');
	});
});
