import { describe, expect, it } from 'vitest';
import { SessionEventKind, SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import { page, session } from './fixtures';
import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
import {
	activePageCount,
	formatTime,
	scannerStateLabel,
	sessionStateLabel,
	statusLabels
} from '$lib/format';

describe('format', () => {
	it('labels states', () => {
		expect(sessionStateLabel(session('s', { State: SessionState.Review }))).toBe('Ready to review');
		expect(sessionStateLabel(session('s', { State: SessionState.Failed }))).toBe('Failed');
		expect(scannerStateLabel(ScannerState.Ready)).toBe('Ready');
		expect(scannerStateLabel(undefined)).toBe('Checking…');
	});

	it('says how a finished scan was saved', () => {
		const done = (Kind: SessionEventKind) =>
			session('s', { State: SessionState.Done, Events: [{ Kind, At: '' }] });
		expect(sessionStateLabel(done(SessionEventKind.Downloaded))).toBe('Downloaded');
		expect(sessionStateLabel(done(SessionEventKind.Exported))).not.toBe('Downloaded');
		expect(sessionStateLabel(done(SessionEventKind.Exported))).toBe(statusLabels.exported);
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
