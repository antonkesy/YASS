import { ColorMode, PageEditOp, PageFilter, Rotation } from '$lib/hub/generated/Yass.Core.Domain';
import type { CropBox, PageEdit } from '$lib/hub/generated/Yass.Core.Domain';
import type { PageDto, SessionDto } from '$lib/hub/generated/Yass.Core.Hub';

/** Empty selection means "all pages", mirroring PageEdit.Targets = null on the server. */
export function targetsOf(selection: ReadonlySet<number>): number[] | undefined {
	return selection.size === 0 ? undefined : [...selection].sort((a, b) => a - b);
}

/**
 * Every edit carries the full payload shape, so one builder fills in the fields an op
 * does not use rather than each of them repeating the defaults.
 */
function edit(Op: PageEditOp, extra: Partial<PageEdit> = {}): PageEdit {
	return { Op, Degrees: 0, Filter: PageFilter.Original, Split: false, ...extra };
}

export function rotateEdit(degrees: 90 | -90 | 180, selection: ReadonlySet<number>): PageEdit {
	return edit(PageEditOp.Rotate, { Degrees: degrees, Targets: targetsOf(selection) });
}

export function deleteEdit(selection: ReadonlySet<number>): PageEdit {
	return edit(PageEditOp.Delete, { Targets: targetsOf(selection) });
}

export function restoreEdit(selection: ReadonlySet<number>): PageEdit {
	return edit(PageEditOp.Restore, { Targets: targetsOf(selection) });
}

export function setFilterEdit(filter: PageFilter, selection: ReadonlySet<number>): PageEdit {
	return edit(PageEditOp.SetFilter, { Filter: filter, Targets: targetsOf(selection) });
}

export function setCropEdit(crop: CropBox | undefined, selection: ReadonlySet<number>): PageEdit {
	return edit(PageEditOp.SetCrop, { Crop: crop, Targets: targetsOf(selection) });
}

export function setSplitEdit(split: boolean, selection: ReadonlySet<number>): PageEdit {
	return edit(PageEditOp.SetSplit, { Split: split, Targets: targetsOf(selection) });
}

export function reorderEdit(order: number[]): PageEdit {
	return edit(PageEditOp.Reorder, { Order: order });
}

export function rotate(rotation: Rotation, degrees: number): Rotation {
	return (((((rotation as number) + degrees) % 360) + 360) % 360) as Rotation;
}

/**
 * Applies an edit to a session the same way the server will, so the UI can update before the round trip.
 * Hashes are left untouched: only the server knows the new ones, and PageUpdated brings them.
 */
export function applyOptimistic(session: SessionDto, edit: PageEdit): SessionDto {
	if (edit.Op === PageEditOp.Reorder) {
		const order = edit.Order ?? [];
		if (order.length !== session.Pages.length) return session;
		const pages = order.map((old, index) => ({ ...session.Pages[old], Index: index }));
		return { ...session, Pages: pages };
	}
	const targets = new Set(edit.Targets ?? session.Pages.map((p) => p.Index));
	const pages = session.Pages.map((page): PageDto => {
		if (!targets.has(page.Index)) return page;
		switch (edit.Op) {
			case PageEditOp.Rotate:
				return { ...page, Rotation: rotate(page.Rotation, edit.Degrees) };
			case PageEditOp.Delete:
				return { ...page, Deleted: true };
			case PageEditOp.Restore:
				return { ...page, Deleted: false };
			case PageEditOp.SetFilter:
				return { ...page, Filter: edit.Filter };
			case PageEditOp.SetCrop:
				return { ...page, Crop: edit.Crop };
			case PageEditOp.SetSplit:
				return { ...page, StartsDocument: edit.Split };
			default:
				return page;
		}
	});
	return { ...session, Pages: pages };
}

export const colorModeLabels: Record<ColorMode, string> = {
	[ColorMode.Bw]: 'Black & white',
	[ColorMode.Grayscale]: 'Grayscale',
	[ColorMode.Color]: 'Color'
};

/**
 * The four treatments the filter menu offers, with the swatch each one shows. The
 * gradients are the mockup's: half blank page, half what the filter does to it.
 */
export const pageFilters = [
	{ value: PageFilter.Original, label: 'Original', swatch: '#fff 50%, #60a5fa 50%' },
	{ value: PageFilter.Enhance, label: 'Enhance', swatch: '#fff 50%, #2b5cf0 50%' },
	{ value: PageFilter.Grayscale, label: 'Grayscale', swatch: '#f5f5f5 50%, #98989b 50%' },
	{ value: PageFilter.Bw, label: 'Black & white', swatch: '#f5f5f5 50%, #111 50%' }
] as const;

export const pageFilterLabels: Record<PageFilter, string> = {
	[PageFilter.Original]: 'Original',
	[PageFilter.Enhance]: 'Enhance',
	[PageFilter.Grayscale]: 'Grayscale',
	[PageFilter.Bw]: 'Black & white'
};

/**
 * Which document each page belongs to, one-based, mirroring Session.Documents on the
 * server: deleted pages belong to none, and the first active page always opens one.
 */
export function documentNumbers(pages: readonly PageDto[]): Map<number, number> {
	const numbers = new Map<number, number>();
	let current = 0;
	for (const page of pages) {
		if (page.Deleted) continue;
		if (current === 0 || page.StartsDocument) current += 1;
		numbers.set(page.Index, current);
	}
	return numbers;
}
