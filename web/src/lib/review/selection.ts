/** Multi-select over page indices. Immutable updates so Svelte sees every change. */
export function toggle(selection: ReadonlySet<number>, index: number): Set<number> {
	const next = new Set(selection);
	if (next.has(index)) next.delete(index);
	else next.add(index);
	return next;
}

export function clear(): Set<number> {
	return new Set();
}

/** Drops indices that no longer exist, e.g. after a delete-session or reorder. */
export function prune(selection: ReadonlySet<number>, count: number): Set<number> {
	return new Set([...selection].filter((i) => i >= 0 && i < count));
}
