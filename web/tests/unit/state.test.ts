import { describe, expect, it } from 'vitest';
import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import type { PageDto, SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import {
	applyPageAdded,
	applyPageUpdated,
	applySessionChanged,
	isRemoval,
	replaceAll,
	sortedSessions
} from '$lib/hub/state';

const page = (Index: number, extra: Partial<PageDto> = {}): PageDto => ({
	Index,
	Rotation: 0,
	Deleted: false,
	IsBlank: false,
	Hash: `h${Index}`,
	ThumbnailReady: false,
	...extra
});

const session = (
	Id: string,
	pages: PageDto[] = [],
	extra: Partial<SessionDto> = {}
): SessionDto => ({
	Id,
	CreatedAt: `2026-09-24T10:00:0${Id.length}Z`,
	State: SessionState.Review,
	Pages: pages,
	OptionsUsed: {},
	...extra
});

describe('session reducers', () => {
	it('adds and replaces sessions immutably', () => {
		const a = replaceAll([session('a')]);
		const b = applySessionChanged(a, session('b'));
		expect(a.size).toBe(1);
		expect(b.size).toBe(2);
		expect(applySessionChanged(b, session('a', [page(0)])).get('a')?.Pages).toHaveLength(1);
	});

	it('removes sessions the server marks deleted or expired', () => {
		const m = replaceAll([session('a', [page(0)])]);
		expect(isRemoval(session('a', [], { State: SessionState.Done, Error: 'deleted' }))).toBe(true);
		expect(isRemoval(session('a', [], { State: SessionState.Done }))).toBe(false);
		expect(
			applySessionChanged(m, session('a', [], { State: SessionState.Done, Error: 'expired' })).has(
				'a'
			)
		).toBe(false);
	});

	it('appends pages in index order and ignores unknown sessions', () => {
		const m = replaceAll([session('a', [page(0)])]);
		const withTwo = applyPageAdded(m, 'a', page(2));
		const withThree = applyPageAdded(withTwo, 'a', page(1));
		expect(withThree.get('a')?.Pages.map((p) => p.Index)).toEqual([0, 1, 2]);
		expect(applyPageAdded(m, 'nope', page(0))).toBe(m);
	});

	it('replaces an existing page on update, keeping others', () => {
		const m = replaceAll([session('a', [page(0), page(1)])]);
		const updated = applyPageUpdated(m, 'a', page(1, { Hash: 'new', Rotation: 90 }));
		expect(updated.get('a')?.Pages[1]).toMatchObject({ Hash: 'new', Rotation: 90 });
		expect(updated.get('a')?.Pages[0].Hash).toBe('h0');
	});

	it('sorts newest first', () => {
		const m = replaceAll([session('a'), session('bb'), session('ccc')]);
		expect(sortedSessions(m).map((s) => s.Id)).toEqual(['ccc', 'bb', 'a']);
	});
});
