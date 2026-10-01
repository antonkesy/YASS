import { describe, expect, it } from 'vitest';
import { SessionState } from '$lib/hub/generated/Yass.Core.Domain';
import type { PageDto, ScannerStatusDto, SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { ScannerState } from '$lib/hub/generated/Yass.Core.Scanning';
import {
	applyPageAdded,
	applyPageUpdated,
	applySessionChanged,
	devicesStale,
	isRemoval,
	replaceAll,
	sortedSessions
} from '$lib/hub/state';
import { page as makePage, session as build } from './fixtures';

const page = (Index: number, extra: Partial<PageDto> = {}): PageDto =>
	makePage(Index, { ThumbnailReady: false, ...extra });

const session = (Id: string, pages: PageDto[] = [], extra: Partial<SessionDto> = {}): SessionDto =>
	build(Id, { CreatedAt: `2026-09-24T10:00:0${Id.length}Z`, Pages: pages, ...extra });

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

describe('devicesStale', () => {
	const status = (
		State: ScannerState,
		DeviceName: string | undefined,
		CheckedAt = '2026-10-01T10:00:00Z'
	): ScannerStatusDto => ({ State, DeviceName, CheckedAt });

	it('asks once the first status arrives, and not before', () => {
		expect(devicesStale(null, null)).toBe(false);
		expect(devicesStale(null, status(ScannerState.Ready, 'ADF'))).toBe(true);
	});

	it('asks again when a scanner appears, goes away or changes name', () => {
		const none = status(ScannerState.NoDevice, undefined);
		const ready = status(ScannerState.Ready, 'ADF');
		expect(devicesStale(none, ready)).toBe(true);
		expect(devicesStale(ready, none)).toBe(true);
		expect(devicesStale(ready, status(ScannerState.Ready, 'Flatbed'))).toBe(true);
	});

	it('leaves the list alone when only the timestamp moved', () => {
		const ready = status(ScannerState.Ready, 'ADF');
		expect(devicesStale(ready, status(ScannerState.Ready, 'ADF', '2026-10-01T10:00:05Z'))).toBe(
			false
		);
	});
});
