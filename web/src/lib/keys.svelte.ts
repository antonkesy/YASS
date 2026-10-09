/**
 * Keyboard handling shared by every screen. Escape closes the topmost thing that is open —
 * a menu, a dialog, the full-screen preview — and only that; with nothing open it goes back.
 * Each of those registers a layer while it is open, and the layout's one window listener
 * hands Escape to the newest. Separate listeners per component used to all hear the same
 * Escape, and closing a dialog also closed the preview under it.
 */

type Layer = { close: () => void };

const layers: Layer[] = [];

export type LayerHandle = {
	/** Takes this layer off the stack; what a component's $effect cleanup calls. */
	pop: () => void;
	/** Whether nothing has opened on top of this layer: the one its keys belong to. */
	readonly top: boolean;
};

/** Registers something that Escape should close, while it is open. */
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

/**
 * Svelte-friendly form of {@link layer}: register in an $effect, and the cleanup pops it.
 * Returns the handle so a component can ask whether it is on top.
 */
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

/** Whether a key press is someone typing, which no shortcut may take from them. */
export function typing(e: KeyboardEvent): boolean {
	const target = e.target as HTMLElement | null;
	if (!target) return false;
	if (target.isContentEditable) return true;
	const tag = target.tagName;
	if (tag === 'TEXTAREA' || tag === 'SELECT') return true;
	if (tag !== 'INPUT') return false;
	// A checkbox, radio or range is not typing; arrows on a range are its own business though.
	const type = (target as HTMLInputElement).type;
	return !['checkbox', 'radio', 'button', 'submit', 'reset'].includes(type);
}

/** Any modifier but Shift: those keys belong to the browser and the system. */
export function modified(e: KeyboardEvent): boolean {
	return e.ctrlKey || e.metaKey || e.altKey;
}

/**
 * The layout's Escape: leaves a text field first, then closes the newest layer. Returns
 * whether it did something, so the caller can stop the browser doing anything else with it.
 */
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

/** For tests: how many layers are open. */
export function openLayers(): number {
	return layers.length;
}

/**
 * For a menu that stays mounted and tracks whether it is open: while `open()` is true, Escape
 * closes it rather than whatever is underneath.
 */
export function escapeCloses(open: () => boolean, close: () => void): void {
	$effect(() => {
		if (!open()) return;
		const handle = layer(close);
		return handle.pop;
	});
}
