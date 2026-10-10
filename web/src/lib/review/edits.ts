import { PageCleanup, PageEditOp, PageFilter, Rotation } from '$lib/hub/generated/Yass.Core.Domain';
import type { CropBox, PageAdjust, PageEdit } from '$lib/hub/generated/Yass.Core.Domain';
import type { PageDto, SessionDto } from '$lib/hub/generated/Yass.Core.Hub';
import { t } from '$lib/i18n/index.svelte';

/** Empty selection means "all pages", mirroring PageEdit.Targets = null on the server. */
export function targetsOf(selection: ReadonlySet<number>): number[] | undefined {
	return selection.size === 0 ? undefined : [...selection].sort((a, b) => a - b);
}

/**
 * Every edit carries the full payload shape, so one builder fills in the fields an op
 * does not use rather than each of them repeating the defaults.
 */
function edit(Op: PageEditOp, extra: Partial<PageEdit> = {}): PageEdit {
	return {
		Op,
		Degrees: 0,
		Filter: PageFilter.Original,
		Split: false,
		Cleanup: PageCleanup.None,
		Skew: 0,
		...extra
	};
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

/** Tone and sharpness; a neutral adjust is sent as none, which the server stores as none anyway. */
export function setAdjustEdit(
	adjust: PageAdjust | undefined,
	selection: ReadonlySet<number>
): PageEdit {
	return edit(PageEditOp.SetAdjust, {
		Adjust: adjust && !isNeutral(adjust) ? adjust : undefined,
		Targets: targetsOf(selection)
	});
}

export function addCleanupEdit(cleanup: PageCleanup, selection: ReadonlySet<number>): PageEdit {
	return edit(PageEditOp.AddCleanup, { Cleanup: cleanup, Targets: targetsOf(selection) });
}

export function removeCleanupEdit(cleanup: PageCleanup, selection: ReadonlySet<number>): PageEdit {
	return edit(PageEditOp.RemoveCleanup, { Cleanup: cleanup, Targets: targetsOf(selection) });
}

/** Rounded to the tenth of a degree the server keeps, so the optimistic page matches it. */
export function setSkewEdit(degrees: number, selection: ReadonlySet<number>): PageEdit {
	return edit(PageEditOp.SetSkew, { Skew: roundSkew(degrees), Targets: targetsOf(selection) });
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
			case PageEditOp.SetAdjust:
				return { ...page, Adjust: edit.Adjust };
			case PageEditOp.AddCleanup:
				return { ...page, Cleanup: page.Cleanup | edit.Cleanup };
			case PageEditOp.RemoveCleanup:
				return { ...page, Cleanup: page.Cleanup & ~edit.Cleanup };
			case PageEditOp.SetSkew:
				return { ...page, Skew: edit.Skew };
			default:
				return page;
		}
	});
	return { ...session, Pages: pages };
}

/**
 * A crop as it looks on the page turned upright. A crop is kept in the scan's own coordinates
 * and taken before the quarter turns, so on a page turned 90° clockwise its left edge is what
 * shows at the top. The Crop dialog draws in these coordinates and converts back on Apply.
 */
export function cropToView(crop: CropBox, rotation: Rotation): CropBox {
	const { Top, Right, Bottom, Left } = crop;
	switch (rotation) {
		case Rotation.R90:
			return { Top: Left, Right: Top, Bottom: Right, Left: Bottom };
		case Rotation.R180:
			return { Top: Bottom, Right: Left, Bottom: Top, Left: Right };
		case Rotation.R270:
			return { Top: Right, Right: Bottom, Bottom: Left, Left: Top };
		default:
			return { ...crop };
	}
}

/** The inverse of {@link cropToView}: a box drawn on the upright page, back in scan coordinates. */
export function cropFromView(box: CropBox, rotation: Rotation): CropBox {
	// Undoing a turn is the turn the rest of the way round.
	return cropToView(box, ((360 - rotation) % 360) as Rotation);
}

export const NEUTRAL_ADJUST: PageAdjust = { Brightness: 0, Contrast: 0, Sharpen: 0 };

export function isNeutral(adjust: PageAdjust | undefined): boolean {
	return !adjust || (adjust.Brightness === 0 && adjust.Contrast === 0 && adjust.Sharpen === 0);
}

/** The furthest Straighten turns a page either way, as the server allows. */
export const MAX_SKEW = 15;

export function roundSkew(degrees: number): number {
	const clamped = Math.max(-MAX_SKEW, Math.min(MAX_SKEW, degrees));
	const rounded = Math.round(clamped * 10) / 10;
	return rounded === 0 ? 0 : rounded;
}

/**
 * A tone slider as a CSS factor: 1 unchanged, -100 halves, 100 is one and a half. The same
 * mapping as PageAdjusts.Tone on the server, which is what lets the dialog preview it with CSS.
 */
export function toneFactor(value: number): number {
	return 1 + value / 200;
}

/**
 * How much a page turned by `degrees` is scaled for its own proportions to fit inside the
 * turned page, with no empty corners. The twin of Straighten.InnerScale on the server.
 */
export function innerScale(width: number, height: number, degrees: number): number {
	const theta = (Math.abs(degrees) * Math.PI) / 180;
	const cos = Math.cos(theta);
	const sin = Math.sin(theta);
	return Math.min(width / (width * cos + height * sin), height / (width * sin + height * cos));
}

/** The clean-ups every one of `pages` has, which is what the filter menu shows as ticked. */
export function commonCleanup(pages: readonly PageDto[]): PageCleanup {
	if (pages.length === 0) return PageCleanup.None;
	return pages.reduce((all, p) => all & p.Cleanup, ~0) as PageCleanup;
}

// Labels are getters so a menu built from these follows a change of language.
export const cleanupOptions = [
	{
		value: PageCleanup.Despeckle,
		get label() {
			return t('review.despeckle');
		},
		get hint() {
			return t('review.despeckleHint');
		}
	},
	{
		value: PageCleanup.AutoLevels,
		get label() {
			return t('review.autoLevels');
		},
		get hint() {
			return t('review.autoLevelsHint');
		}
	}
] as const;

/**
 * The four treatments the filter menu offers, with the swatch each one shows. The
 * gradients are the mockup's: half blank page, half what the filter does to it.
 */
export const pageFilters = [
	{
		value: PageFilter.Original,
		get label() {
			return t('review.original');
		},
		swatch: '#fff 50%, #60a5fa 50%'
	},
	{
		value: PageFilter.Enhance,
		get label() {
			return t('review.enhance');
		},
		swatch: '#fff 50%, #2b5cf0 50%'
	},
	{
		value: PageFilter.Grayscale,
		get label() {
			return t('review.grayscale');
		},
		swatch: '#f5f5f5 50%, #98989b 50%'
	},
	{
		value: PageFilter.Bw,
		get label() {
			return t('review.blackWhite');
		},
		swatch: '#f5f5f5 50%, #111 50%'
	}
] as const;

export const pageFilterLabels: Record<PageFilter, string> = {
	get [PageFilter.Original]() {
		return t('review.original');
	},
	get [PageFilter.Enhance]() {
		return t('review.enhance');
	},
	get [PageFilter.Grayscale]() {
		return t('review.grayscale');
	},
	get [PageFilter.Bw]() {
		return t('review.blackWhite');
	}
};
