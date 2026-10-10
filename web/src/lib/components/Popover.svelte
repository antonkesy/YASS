<script lang="ts">
	import type { Snippet } from 'svelte';
	import { useLayer } from '$lib/keys.svelte';

	let {
		label,
		onclose,
		class: panel,
		role = 'menu',
		name,
		catcher = true,
		raised = false,
		testid,
		children
	}: {
		/** What pressing outside does, for a screen reader: "Close the … menu". */
		label: string;
		onclose: () => void;
		/** Where the panel sits and how it looks, which every caller decides for itself. */
		class: string;
		/** A menu takes the keyboard; a dialog is something to read and stays out of its way. */
		role?: 'menu' | 'dialog';
		/** The panel's own accessible name, for a dialog. */
		name?: string;
		/** Whether a press outside closes it. One opened by hovering has nothing to catch. */
		catcher?: boolean;
		/** Above a settings section's sticky header, for a popover inside one. */
		raised?: boolean;
		testid?: string;
		children: Snippet;
	} = $props();

	// Escape closes this, and only this, while it is open.
	useLayer(() => onclose());

	let el = $state<HTMLDivElement | null>(null);

	const items = () =>
		el ? [...el.querySelectorAll<HTMLElement>('[role^="menuitem"]:not([disabled])')] : [];

	// A menu is used from the keyboard like the system's: it opens on its first item, the
	// arrows move through it, and closing it puts focus back on the button that opened it.
	const before = document.activeElement;
	$effect(() => {
		if (role === 'menu') items()[0]?.focus();
	});
	$effect(() => () => {
		if (role === 'menu' && before instanceof HTMLElement && before.isConnected) before.focus();
	});

	function rove(e: KeyboardEvent) {
		const all = items();
		if (all.length === 0) return;
		const at = all.indexOf(document.activeElement as HTMLElement);
		const next =
			e.key === 'ArrowDown'
				? (at + 1) % all.length
				: e.key === 'ArrowUp'
					? (at - 1 + all.length) % all.length
					: e.key === 'Home'
						? 0
						: e.key === 'End'
							? all.length - 1
							: null;
		if (next === null) return;
		e.preventDefault();
		all[next].focus();
	}
</script>

{#if catcher}
	<!-- Click-catcher first, so anywhere outside the panel closes it, a tap included. -->
	<button
		type="button"
		class="fixed inset-0 cursor-default {raised ? 'z-34' : 'z-30'}"
		aria-label={label}
		onclick={onclose}
	></button>
{/if}
{#if role === 'menu'}
	<div
		class="{panel} outline-none"
		role="menu"
		tabindex="-1"
		bind:this={el}
		onkeydown={rove}
		data-testid={testid}
	>
		{@render children()}
	</div>
{:else}
	<div class={panel} role="dialog" aria-label={name} data-testid={testid}>
		{@render children()}
	</div>
{/if}
