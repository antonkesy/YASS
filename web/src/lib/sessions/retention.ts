import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import type { HistoryPrefs, RetentionUnit } from '$lib/presets';

export const retentionUnits: readonly RetentionUnit[] = ['minutes', 'hours', 'days', 'months'];

/**
 * When a scan made at `createdAt` is old enough to go, under the given timeframe. Counted in
 * UTC so a clock change in between does not move the answer by an hour.
 */
export function expiresAt(createdAt: string, amount: number, unit: RetentionUnit): Date {
	const at = new Date(createdAt);
	switch (unit) {
		case 'minutes':
			at.setUTCMinutes(at.getUTCMinutes() + amount);
			break;
		case 'hours':
			at.setUTCHours(at.getUTCHours() + amount);
			break;
		case 'days':
			at.setUTCDate(at.getUTCDate() + amount);
			break;
		case 'months':
			at.setUTCMonth(at.getUTCMonth() + amount);
			break;
	}
	return at;
}

/**
 * The scans the auto-clear preference says should go now. A scan still coming in or going out
 * is left alone whatever its age: deleting under a running export is how a save gets lost.
 */
export function expiredSessions(
	sessions: readonly SessionDto[],
	prefs: HistoryPrefs,
	now = new Date()
): SessionDto[] {
	const rule = prefs.autoClear;
	if (!rule || !(rule.amount > 0)) return [];
	return sessions.filter(
		(s) =>
			s.State !== SessionState.Scanning &&
			s.State !== SessionState.Exporting &&
			expiresAt(s.CreatedAt, rule.amount, rule.unit) <= now
	);
}
