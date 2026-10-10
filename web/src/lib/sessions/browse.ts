import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { sessionStatus, sessionTitle, type SessionStatus } from '$lib/format';
import { t } from '$lib/i18n/index.svelte';

export type SortOrder = 'newest' | 'oldest' | 'name';

export const sortLabels: Record<SortOrder, string> = {
	get newest() {
		return t('home.sortNewest');
	},
	get oldest() {
		return t('home.sortOldest');
	},
	get name() {
		return t('home.sortName');
	}
};

export const sortShortLabels: Record<SortOrder, string> = {
	get newest() {
		return t('home.sortNewestShort');
	},
	get oldest() {
		return t('home.sortOldestShort');
	},
	get name() {
		return t('home.sortNameShort');
	}
};

/** Discarded is not offered: discarding deletes the scan, so nothing listed carries it. */
export const filterableStatuses = [
	'draft',
	'exported',
	'saved',
	'downloaded'
] as const satisfies readonly SessionStatus[];

export type StatusFilter = 'all' | (typeof filterableStatuses)[number];

/** Matches the displayed title, so a template-named scan is found by e.g. its year. */
export function matchesQuery(
	session: SessionDto,
	query: string,
	template = '',
	now?: Date
): boolean {
	const q = query.trim().toLowerCase();
	if (q === '') return true;
	return sessionTitle(session, template, now).toLowerCase().includes(q);
}

export function browse(
	sessions: readonly SessionDto[],
	options: {
		query?: string;
		status?: StatusFilter;
		order?: SortOrder;
		template?: string;
		now?: Date;
	} = {}
): SessionDto[] {
	const { query = '', status = 'all', order = 'newest', template = '', now } = options;

	const kept = sessions.filter(
		(s) =>
			matchesQuery(s, query, template, now) && (status === 'all' || sessionStatus(s) === status)
	);

	return [...kept].sort((a, b) => {
		if (order === 'name') {
			return sessionTitle(a, template, now).localeCompare(sessionTitle(b, template, now));
		}
		const diff = Date.parse(a.CreatedAt) - Date.parse(b.CreatedAt);
		return order === 'oldest' ? diff : -diff;
	});
}

export function statusCounts(sessions: readonly SessionDto[]): Record<string, number> {
	const counts: Record<string, number> = {};
	for (const session of sessions) {
		const status = sessionStatus(session);
		counts[status] = (counts[status] ?? 0) + 1;
	}
	return counts;
}
