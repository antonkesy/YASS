import { describe, expect, it } from 'vitest';
import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import { expiredSessions, expiresAt } from '$lib/sessions/retention';
import { session } from './fixtures';

describe('expiresAt', () => {
	const made = '2026-01-31T10:00:00Z';

	it('adds the timeframe in its own unit', () => {
		expect(expiresAt(made, 30, 'minutes').toISOString()).toBe('2026-01-31T10:30:00.000Z');
		expect(expiresAt(made, 2, 'hours').toISOString()).toBe('2026-01-31T12:00:00.000Z');
		expect(expiresAt(made, 7, 'days').toISOString()).toBe('2026-02-07T10:00:00.000Z');
	});

	it('counts months on the calendar, not as thirty days', () => {
		// January 31st plus a month lands in March, as JavaScript dates roll it; the point is
		// that three months from now is the same date three months on, not 90 days.
		expect(expiresAt('2026-03-15T10:00:00Z', 3, 'months').toISOString()).toBe(
			'2026-06-15T10:00:00.000Z'
		);
	});
});

describe('expiredSessions', () => {
	const now = new Date('2026-09-10T12:00:00Z');
	const sessions = [
		session('old', { CreatedAt: '2026-09-01T12:00:00Z' }),
		session('fresh', { CreatedAt: '2026-09-10T11:00:00Z' }),
		session('busy', { CreatedAt: '2026-09-01T12:00:00Z', State: SessionState.Scanning }),
		session('saving', { CreatedAt: '2026-09-01T12:00:00Z', State: SessionState.Exporting })
	];

	it('names only the scans past the timeframe that nothing is still doing', () => {
		const expired = expiredSessions(sessions, { autoClear: { amount: 7, unit: 'days' } }, now);
		expect(expired.map((s) => s.Id)).toEqual(['old']);
	});

	it('names nothing when the preference is off or nonsensical', () => {
		expect(expiredSessions(sessions, { autoClear: null }, now)).toEqual([]);
		expect(expiredSessions(sessions, { autoClear: { amount: 0, unit: 'days' } }, now)).toEqual([]);
	});

	it('treats the boundary as expired', () => {
		const at = { autoClear: { amount: 1, unit: 'hours' as const } };
		expect(expiredSessions(sessions, at, now).map((s) => s.Id)).toEqual(['old', 'fresh']);
	});
});
