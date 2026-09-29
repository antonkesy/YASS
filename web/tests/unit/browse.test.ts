import { describe, expect, it } from 'vitest';
import { SessionEventKind, SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import { browse, matchesQuery, statusCounts } from '$lib/sessions/browse';
import { formatBytes, formatTime, sessionStatus, sessionTitle } from '$lib/format';
import { session } from './fixtures';

const at = '2026-09-24T10:00:00Z';
const event = (Kind: SessionEventKind, Path?: string) => ({ Kind, At: at, Path });

describe('session status', () => {
	it('is a draft until something happens to it', () => {
		expect(sessionStatus(session('a'))).toBe('draft');
	});

	it('comes from the newest event', () => {
		const s = session('a', {
			Events: [event(SessionEventKind.Exported, '/out/a.pdf'), event(SessionEventKind.Downloaded)]
		});
		expect(sessionStatus(s)).toBe('downloaded');
	});

	it('lets a busy or failed state outrank the history', () => {
		const events = [event(SessionEventKind.Exported, '/out/a.pdf')];
		expect(sessionStatus(session('a', { Events: events, State: SessionState.Scanning }))).toBe(
			'scanning'
		);
		expect(sessionStatus(session('a', { Events: events, State: SessionState.Exporting }))).toBe(
			'exporting'
		);
		expect(sessionStatus(session('a', { Events: events, State: SessionState.Failed }))).toBe(
			'failed'
		);
	});
});

describe('session title', () => {
	it('prefers the name, then the template, then the timestamp', () => {
		expect(sessionTitle(session('a', { Name: 'Tax return' }), '{YEAR}_scan')).toBe('Tax return');

		// Nobody has named it, so it is called what it will be saved as — the same name the
		// review screen shows, not the time it happened to be scanned.
		const unnamed = session('a', { CreatedAt: '2026-09-01T10:00:00Z' });
		expect(sessionTitle(unnamed, '{YEAR}-{MONTH}-{DAY}_scan')).toBe('2026-09-01_scan');

		// No name and no template: the timestamp is all there is.
		expect(sessionTitle(session('a', { Name: '   ' }), '')).not.toBe('   ');
		expect(sessionTitle(unnamed, '   ')).toBe(formatTime('2026-09-01T10:00:00Z'));
	});
});

describe('browse', () => {
	const sessions = [
		session('a', { CreatedAt: '2026-09-01T10:00:00Z', Name: 'Invoice' }),
		session('b', {
			CreatedAt: '2026-09-03T10:00:00Z',
			Name: 'Contract',
			Events: [event(SessionEventKind.Exported, '/out/b.pdf')]
		}),
		session('c', {
			CreatedAt: '2026-09-02T10:00:00Z',
			Name: 'Receipt',
			Events: [event(SessionEventKind.Discarded)]
		})
	];

	it('sorts newest first by default and can reverse or sort by name', () => {
		expect(browse(sessions).map((s) => s.Id)).toEqual(['b', 'c', 'a']);
		expect(browse(sessions, { order: 'oldest' }).map((s) => s.Id)).toEqual(['a', 'c', 'b']);
		expect(browse(sessions, { order: 'name' }).map((s) => s.Id)).toEqual(['b', 'a', 'c']);
	});

	it('filters by name, case-insensitively and ignoring surrounding space', () => {
		expect(browse(sessions, { query: '  contr ' }).map((s) => s.Id)).toEqual(['b']);
		expect(browse(sessions, { query: 'nothing' })).toEqual([]);
		expect(matchesQuery(sessions[0], '')).toBe(true);
	});

	it('searches and sorts on the name a scan is shown under, template and all', () => {
		// Neither is named, so both are called what their template makes of them — and that
		// is what the search box has to match, or typing what you can see finds nothing.
		const unnamed = [
			session('x', { CreatedAt: '2026-09-01T10:00:00Z' }),
			session('y', { CreatedAt: '2026-10-01T10:00:00Z' })
		];
		const template = '{YEAR}-{MONTH}-{DAY}_scan';

		expect(browse(unnamed, { query: '2026-10', template }).map((s) => s.Id)).toEqual(['y']);
		expect(browse(unnamed, { order: 'name', template }).map((s) => s.Id)).toEqual(['x', 'y']);
		// Without the template there is no name to match, only the timestamp.
		expect(browse(unnamed, { query: '2026-10' })).toEqual([]);
	});

	it('filters by status', () => {
		expect(browse(sessions, { status: 'exported' }).map((s) => s.Id)).toEqual(['b']);
		expect(browse(sessions, { status: 'draft' }).map((s) => s.Id)).toEqual(['a']);
		expect(browse(sessions, { status: 'all' })).toHaveLength(3);
	});

	it('does not reorder the array it is given', () => {
		const input = [...sessions];
		browse(input, { order: 'name' });
		expect(input.map((s) => s.Id)).toEqual(['a', 'b', 'c']);
	});

	it('counts each status for the filter chips', () => {
		expect(statusCounts(sessions)).toEqual({ draft: 1, exported: 1, discarded: 1 });
	});
});

describe('formatBytes', () => {
	it('reports whole kilobytes and one decimal above', () => {
		expect(formatBytes(0)).toBe('0 kB');
		expect(formatBytes(400)).toBe('1 kB');
		expect(formatBytes(12_000)).toBe('12 kB');
		expect(formatBytes(2_400_000)).toBe('2.4 MB');
		expect(formatBytes(15_000_000)).toBe('15 MB');
		expect(formatBytes(3_200_000_000)).toBe('3.2 GB');
	});
});
