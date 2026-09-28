import { describe, expect, it } from 'vitest';
import { PageEditOp, PageFilter, Rotation } from '$lib/hub/generated/Yass.Core.Domain';
import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { sessionOf } from './fixtures';
import {
	applyOptimistic,
	deleteEdit,
	reorderEdit,
	restoreEdit,
	rotate,
	rotateEdit,
	setCropEdit,
	setFilterEdit,
	targetsOf
} from '$lib/review/edits';

const session = (n: number): SessionDto => sessionOf('s', n);

describe('edit builders', () => {
	it('empty selection targets all pages (undefined), otherwise sorted indices', () => {
		expect(targetsOf(new Set())).toBeUndefined();
		expect(targetsOf(new Set([3, 1]))).toEqual([1, 3]);
		expect(rotateEdit(90, new Set([2]))).toEqual({
			Op: PageEditOp.Rotate,
			Degrees: 90,
			Filter: PageFilter.Original,
			Targets: [2]
		});
		expect(deleteEdit(new Set()).Targets).toBeUndefined();
		expect(setFilterEdit(PageFilter.Bw, new Set([0])).Filter).toBe(PageFilter.Bw);
		expect(setCropEdit({ Top: 5, Right: 0, Bottom: 5, Left: 0 }, new Set([1]))).toMatchObject({
			Op: PageEditOp.SetCrop,
			Crop: { Top: 5, Bottom: 5 },
			Targets: [1]
		});
		expect(setCropEdit(undefined, new Set([1])).Crop).toBeUndefined();
		expect(reorderEdit([1, 0])).toMatchObject({ Op: PageEditOp.Reorder, Order: [1, 0] });
	});

	it('rotation wraps in both directions', () => {
		expect(rotate(Rotation.R270, 90)).toBe(Rotation.R0);
		expect(rotate(Rotation.R0, -90)).toBe(Rotation.R270);
		expect(rotate(Rotation.R90, 180)).toBe(Rotation.R270);
	});
});

describe('applyOptimistic', () => {
	it('mirrors server semantics for rotate, delete, restore, filter and crop', () => {
		let s = applyOptimistic(session(3), rotateEdit(90, new Set([1])));
		expect(s.Pages.map((p) => p.Rotation)).toEqual([0, 90, 0]);
		s = applyOptimistic(s, deleteEdit(new Set()));
		expect(s.Pages.every((p) => p.Deleted)).toBe(true);
		s = applyOptimistic(s, restoreEdit(new Set([2])));
		expect(s.Pages.map((p) => p.Deleted)).toEqual([true, true, false]);
		s = applyOptimistic(s, setFilterEdit(PageFilter.Bw, new Set([0])));
		expect(s.Pages[0].Filter).toBe(PageFilter.Bw);
		expect(s.Pages[1].Filter).toBe(PageFilter.Original);
		s = applyOptimistic(s, setCropEdit({ Top: 10, Right: 0, Bottom: 0, Left: 0 }, new Set([0])));
		expect(s.Pages[0].Crop).toEqual({ Top: 10, Right: 0, Bottom: 0, Left: 0 });
		expect(s.Pages[1].Crop).toBeUndefined();
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
