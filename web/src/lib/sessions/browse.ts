import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { sessionStatus, sessionTitle, type SessionStatus } from '$lib/format';

export type SortOrder = 'newest' | 'oldest' | 'name';

export const sortLabels: Record<SortOrder, string> = {
	newest: 'Newest first',
	oldest: 'Oldest first',
	name: 'Name A–Z'
};

/** The same orders named for the button that opens the menu, where space is tight. */
export const sortShortLabels: Record<SortOrder, string> = {
	newest: 'Newest',
	oldest: 'Oldest',
	name: 'Name'
};

/** Statuses the filter row offers, in the order the mockup lists them. */
export const filterableStatuses = [
	'draft',
	'exported',
	'saved',
	'downloaded',
	'discarded'
] as const satisfies readonly SessionStatus[];

export type StatusFilter = 'all' | (typeof filterableStatuses)[number];

export function matchesQuery(session: SessionDto, query: string, now?: Date): boolean {
	const q = query.trim().toLowerCase();
	if (q === '') return true;
	return sessionTitle(session, now).toLowerCase().includes(q);
}

export function browse(
	sessions: readonly SessionDto[],
	options: { query?: string; status?: StatusFilter; order?: SortOrder; now?: Date } = {}
): SessionDto[] {
	const { query = '', status = 'all', order = 'newest', now } = options;

	const kept = sessions.filter(
		(s) => matchesQuery(s, query, now) && (status === 'all' || sessionStatus(s) === status)
	);

	// Sorted on a copy: the caller's array is store-owned.
	return [...kept].sort((a, b) => {
		if (order === 'name') return sessionTitle(a, now).localeCompare(sessionTitle(b, now));
		const diff = Date.parse(a.CreatedAt) - Date.parse(b.CreatedAt);
		return order === 'oldest' ? diff : -diff;
	});
}

/** How many sessions carry each status, for the counts on the filter chips. */
export function statusCounts(sessions: readonly SessionDto[]): Record<string, number> {
	const counts: Record<string, number> = {};
	for (const session of sessions) {
		const status = sessionStatus(session);
		counts[status] = (counts[status] ?? 0) + 1;
	}
	return counts;
}
