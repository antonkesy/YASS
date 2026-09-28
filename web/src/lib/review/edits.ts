import { ColorMode, PageEditOp, Rotation } from '$lib/hub/generated/Yass.Core.Domain';
import type { PageEdit } from '$lib/hub/generated/Yass.Core.Domain';
import type { PageDto, SessionDto } from '$lib/hub/generated/Yass.Core.Hub';

/** Empty selection means "all pages", mirroring PageEdit.Targets = null on the server. */
export function targetsOf(selection: ReadonlySet<number>): number[] | undefined {
	return selection.size === 0 ? undefined : [...selection].sort((a, b) => a - b);
}

export function rotateEdit(degrees: 90 | -90 | 180, selection: ReadonlySet<number>): PageEdit {
	return { Op: PageEditOp.Rotate, Degrees: degrees, Targets: targetsOf(selection) };
}

export function deleteEdit(selection: ReadonlySet<number>): PageEdit {
	return { Op: PageEditOp.Delete, Degrees: 0, Targets: targetsOf(selection) };
}

export function restoreEdit(selection: ReadonlySet<number>): PageEdit {
	return { Op: PageEditOp.Restore, Degrees: 0, Targets: targetsOf(selection) };
}

export function setColorEdit(
	color: ColorMode | undefined,
	selection: ReadonlySet<number>
): PageEdit {
	return { Op: PageEditOp.SetColor, Degrees: 0, Color: color, Targets: targetsOf(selection) };
}

export function reorderEdit(order: number[]): PageEdit {
	return { Op: PageEditOp.Reorder, Degrees: 0, Order: order };
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
			case PageEditOp.SetColor:
				return { ...page, ColorOverride: edit.Color };
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
