/**
 * Multi-select, immutable so Svelte sees every change. Generic because the same
 * behaviour serves page indices in the review screen and session ids in past scans.
 */
export function toggle<T>(selection: ReadonlySet<T>, item: T): Set<T> {
	const next = new Set(selection);
	if (next.has(item)) next.delete(item);
	else next.add(item);
	return next;
}

export function clear<T>(): Set<T> {
	return new Set<T>();
}

/** Drops indices that no longer exist, e.g. after a delete-session or reorder. */
export function prune(selection: ReadonlySet<number>, count: number): Set<number> {
	return new Set([...selection].filter((i) => i >= 0 && i < count));
}

/** Keeps only what is still on screen, so bulk actions cannot touch hidden rows. */
export function retain<T>(selection: ReadonlySet<T>, visible: ReadonlySet<T>): Set<T> {
	return new Set([...selection].filter((item) => visible.has(item)));
}
