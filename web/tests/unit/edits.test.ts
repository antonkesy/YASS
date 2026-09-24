import { describe, expect, it } from 'vitest';
import {
	ColorMode,
	PageEditOp,
	Rotation,
	SessionState
} from '$lib/hub/generated/Yawss.Core.Domain';
import type { PageDto, SessionDto } from '$lib/hub/generated/Yawss.Core.Hub';
import {
	applyOptimistic,
	deleteEdit,
	reorderEdit,
	rotate,
	rotateEdit,
	setColorEdit,
	targetsOf
} from '$lib/review/edits';

const page = (Index: number): PageDto => ({
	Index,
	Rotation: Rotation.R0,
	Deleted: false,
	IsBlank: false,
	Hash: `h${Index}`,
	ThumbnailReady: true
});
const session = (n: number): SessionDto => ({
	Id: 's',
	CreatedAt: '',
	State: SessionState.Review,
	Pages: Array.from({ length: n }, (_, i) => page(i)),
	OptionsUsed: {}
});

describe('edit builders', () => {
	it('empty selection targets all pages (undefined), otherwise sorted indices', () => {
		expect(targetsOf(new Set())).toBeUndefined();
		expect(targetsOf(new Set([3, 1]))).toEqual([1, 3]);
		expect(rotateEdit(90, new Set([2]))).toEqual({
			Op: PageEditOp.Rotate,
			Degrees: 90,
			Targets: [2]
		});
		expect(deleteEdit(new Set()).Targets).toBeUndefined();
		expect(setColorEdit(undefined, new Set([0])).Color).toBeUndefined();
		expect(reorderEdit([1, 0])).toMatchObject({ Op: PageEditOp.Reorder, Order: [1, 0] });
	});

	it('rotation wraps in both directions', () => {
		expect(rotate(Rotation.R270, 90)).toBe(Rotation.R0);
		expect(rotate(Rotation.R0, -90)).toBe(Rotation.R270);
		expect(rotate(Rotation.R90, 180)).toBe(Rotation.R270);
	});
});

describe('applyOptimistic', () => {
	it('mirrors server semantics for rotate, delete, restore and colour', () => {
		let s = applyOptimistic(session(3), rotateEdit(90, new Set([1])));
		expect(s.Pages.map((p) => p.Rotation)).toEqual([0, 90, 0]);
		s = applyOptimistic(s, deleteEdit(new Set()));
		expect(s.Pages.every((p) => p.Deleted)).toBe(true);
		s = applyOptimistic(s, { Op: PageEditOp.Restore, Degrees: 0, Targets: [2] });
		expect(s.Pages.map((p) => p.Deleted)).toEqual([true, true, false]);
		s = applyOptimistic(s, setColorEdit(ColorMode.Color, new Set([0])));
		expect(s.Pages[0].ColorOverride).toBe(ColorMode.Color);
		expect(s.Pages[1].ColorOverride).toBeUndefined();
	});

	it('keeps hashes untouched (only the server knows the new ones)', () => {
		const s = applyOptimistic(session(2), rotateEdit(90, new Set()));
		expect(s.Pages.map((p) => p.Hash)).toEqual(['h0', 'h1']);
	});

	it('reorders and reindexes, ignoring malformed orders', () => {
		const s = applyOptimistic(session(3), reorderEdit([2, 0, 1]));
		expect(s.Pages.map((p) => p.Hash)).toEqual(['h2', 'h0', 'h1']);
		expect(s.Pages.map((p) => p.Index)).toEqual([0, 1, 2]);
		expect(applyOptimistic(session(3), reorderEdit([0, 1]))).toEqual(session(3));
	});
});
