/** Immutable so Svelte sees every change. */
export function toggle<T>(selection: ReadonlySet<T>, item: T): Set<T> {
	const next = new Set(selection);
	if (next.has(item)) next.delete(item);
	else next.add(item);
	return next;
}

export function clear<T>(): Set<T> {
	return new Set<T>();
}

export function prune(selection: ReadonlySet<number>, count: number): Set<number> {
	return new Set([...selection].filter((i) => i >= 0 && i < count));
}

export function retain<T>(selection: ReadonlySet<T>, visible: ReadonlySet<T>): Set<T> {
	return new Set([...selection].filter((item) => visible.has(item)));
}
