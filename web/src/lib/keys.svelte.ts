/**
 * Escape closes only the topmost open layer (menu, dialog, preview), via the layout's single
 * window listener; per-component listeners all heard the same Escape.
 */

type Layer = { close: () => void };

const layers: Layer[] = [];

export type LayerHandle = {
	pop: () => void;
	readonly top: boolean;
};

export function layer(close: () => void): LayerHandle {
	const entry: Layer = { close };
	layers.push(entry);
	return {
		pop: () => {
			const at = layers.indexOf(entry);
			if (at >= 0) layers.splice(at, 1);
		},
		get top() {
			return layers.at(-1) === entry;
		}
	};
}

export function useLayer(close: () => void): () => LayerHandle | null {
	let handle: LayerHandle | null = null;
	$effect(() => {
		handle = layer(close);
		return () => {
			handle?.pop();
			handle = null;
		};
	});
	return () => handle;
}

export function typing(e: KeyboardEvent): boolean {
	const target = e.target as HTMLElement | null;
	if (!target) return false;
	if (target.isContentEditable) return true;
	const tag = target.tagName;
	if (tag === 'TEXTAREA' || tag === 'SELECT') return true;
	if (tag !== 'INPUT') return false;
	const type = (target as HTMLInputElement).type;
	return !['checkbox', 'radio', 'button', 'submit', 'reset'].includes(type);
}

export function modified(e: KeyboardEvent): boolean {
	return e.ctrlKey || e.metaKey || e.altKey;
}

/** Returns whether it did something, so the caller can preventDefault. */
export function escape(e: KeyboardEvent): boolean {
	if (e.key !== 'Escape' || e.defaultPrevented) return false;
	if (typing(e)) {
		(e.target as HTMLElement).blur();
		return true;
	}
	const top = layers.at(-1);
	if (!top) return false;
	top.close();
	return true;
}

export function openLayers(): number {
	return layers.length;
}

export function escapeCloses(open: () => boolean, close: () => void): void {
	$effect(() => {
		if (!open()) return;
		const handle = layer(close);
		return handle.pop;
	});
}
