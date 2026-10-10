import { writable } from 'svelte/store';

export type Toast = {
	id: number;
	message: string;
	undo?: () => void;
};

export const TOAST_MS = 4200;

function create() {
	const { subscribe, set } = writable<Toast | null>(null);
	let seq = 0;
	let timer: ReturnType<typeof setTimeout> | undefined;

	function show(message: string, undo?: () => void) {
		clearTimeout(timer);
		const toast = { id: ++seq, message, undo };
		set(toast);
		timer = setTimeout(() => set(null), TOAST_MS);
		return toast.id;
	}

	return {
		subscribe,
		show,
		dismiss() {
			clearTimeout(timer);
			set(null);
		}
	};
}

export const toast = create();
