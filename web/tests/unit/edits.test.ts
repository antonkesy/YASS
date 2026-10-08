import { describe, expect, it } from 'vitest';
import { PageCleanup, PageEditOp, PageFilter, Rotation } from '$lib/hub/generated/Yass.Core.Domain';
import type { SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { page, sessionOf } from './fixtures';
import {
	addCleanupEdit,
	applyOptimistic,
	commonCleanup,
	cropFromView,
	cropToView,
	deleteEdit,
	innerScale,
	removeCleanupEdit,
	roundSkew,
	setAdjustEdit,
	setSkewEdit,
	toneFactor,
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
			Split: false,
			Cleanup: PageCleanup.None,
			Skew: 0,
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

describe('adjust, clean-up and skew', () => {
	it('sends a neutral adjust as none', () => {
		expect(
			setAdjustEdit({ Brightness: 0, Contrast: 0, Sharpen: 0 }, new Set([1])).Adjust
		).toBeUndefined();
		expect(setAdjustEdit({ Brightness: 10, Contrast: 0, Sharpen: 0 }, new Set()).Adjust).toEqual({
			Brightness: 10,
			Contrast: 0,
			Sharpen: 0
		});
	});

	it('applies each one optimistically, the way the server will', () => {
		let s = session(2);
		s = applyOptimistic(
			s,
			setAdjustEdit({ Brightness: 20, Contrast: -10, Sharpen: 0 }, new Set([0]))
		);
		s = applyOptimistic(s, addCleanupEdit(PageCleanup.Despeckle, new Set()));
		s = applyOptimistic(s, addCleanupEdit(PageCleanup.AutoLevels, new Set([1])));
		s = applyOptimistic(s, removeCleanupEdit(PageCleanup.Despeckle, new Set([1])));
		s = applyOptimistic(s, setSkewEdit(2.34, new Set([1])));

		expect(s.Pages[0].Adjust).toEqual({ Brightness: 20, Contrast: -10, Sharpen: 0 });
		expect(s.Pages[1].Adjust).toBeUndefined();
		expect(s.Pages[0].Cleanup).toBe(PageCleanup.Despeckle);
		expect(s.Pages[1].Cleanup).toBe(PageCleanup.AutoLevels);
		expect(s.Pages[1].Skew).toBe(2.3);
		expect(s.Pages.map((p) => p.Hash)).toEqual(session(2).Pages.map((p) => p.Hash));
	});

	it('rounds and bounds a skew like the server', () => {
		expect(roundSkew(2.36)).toBe(2.4);
		expect(roundSkew(-0.04)).toBe(0);
		expect(roundSkew(40)).toBe(15);
	});

	it('ticks a clean-up only when every page has it', () => {
		const both = PageCleanup.Despeckle | PageCleanup.AutoLevels;
		expect(
			commonCleanup([page(0, { Cleanup: both }), page(1, { Cleanup: PageCleanup.AutoLevels })])
		).toBe(PageCleanup.AutoLevels);
		expect(commonCleanup([])).toBe(PageCleanup.None);
	});

	// Shared values with PageHashTests on the server, so the preview and the render agree.
	it('maps tone and straighten the same way the server does', () => {
		expect(toneFactor(0)).toBe(1);
		expect(toneFactor(-100)).toBe(0.5);
		expect(toneFactor(100)).toBe(1.5);
		expect(innerScale(100, 100, 45)).toBeCloseTo(1 / Math.SQRT2, 9);
		expect(innerScale(200, 300, 0)).toBe(1);
		expect(innerScale(200, 300, 5)).toBeCloseTo(innerScale(200, 300, -5), 12);
	});
});

describe('crop coordinates on a turned page', () => {
	const crop = { Top: 1, Right: 2, Bottom: 3, Left: 4 };

	it("shows the scan's left edge at the top of a page turned right", () => {
		expect(cropToView(crop, Rotation.R90)).toEqual({ Top: 4, Right: 1, Bottom: 2, Left: 3 });
		expect(cropToView(crop, Rotation.R180)).toEqual({ Top: 3, Right: 4, Bottom: 1, Left: 2 });
		expect(cropToView(crop, Rotation.R270)).toEqual({ Top: 2, Right: 3, Bottom: 4, Left: 1 });
		expect(cropToView(crop, Rotation.R0)).toEqual(crop);
	});

	it.each([Rotation.R0, Rotation.R90, Rotation.R180, Rotation.R270])(
		'converts back to the same crop at %s degrees',
		(rotation) => {
			expect(cropFromView(cropToView(crop, rotation), rotation)).toEqual(crop);
		}
	);
});
