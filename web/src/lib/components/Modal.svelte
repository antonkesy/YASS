<script lang="ts" module>
	/** The grey Cancel button every dialog ends with. */
	export const dialogCancel =
		'h-[46px] rounded-[14px] bg-ctl px-4 text-sm font-bold text-ink-3 transition-colors hover:bg-ctl-hover';

	/** The blue button that does what the dialog is for. */
	export const dialogPrimary =
		'flex h-[46px] items-center gap-2 rounded-[14px] bg-accent px-5 text-[15px] font-extrabold text-white shadow-export transition-[background-color,box-shadow] hover:bg-accent-hover disabled:pointer-events-none disabled:opacity-40';

	const FOCUSABLE =
		'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])';
</script>

<script lang="ts">
	import type { Snippet } from 'svelte';
	import { useLayer } from '$lib/keys.svelte';
	import { backdropOut, cardOut } from '$lib/motion';

	let {
		title,
		hint,
		cancelLabel,
		oncancel,
		class: size,
		top = false,
		testid,
		children,
		actions
	}: {
		/** The heading, and what a screen reader announces the dialog as. */
		title: string;
		/** A line under the heading, when the title needs one. */
		hint?: string;
		/** What the backdrop does when tapped, for a screen reader. */
		cancelLabel: string;
		oncancel: () => void;
		/** The card's width and gap: the only things that differ between dialogs. */
		class: string;
		/** Above the other dialogs, for one that can open over them. */
		top?: boolean;
		testid: string;
		children: Snippet;
		/** The row of buttons at the bottom, right-aligned. */
		actions?: Snippet;
	} = $props();

	// Escape cancels, through the shared layer stack: only this closes, not what is under it.
	useLayer(() => oncancel());

	let card = $state<HTMLDivElement | null>(null);

	// Focus moves into the dialog as it opens and goes back where it came from as it closes, so
	// a keyboard user is not left on the page under it, or at the top of it afterwards. What
	// had focus is read as the dialog is created, before anything in it can take focus.
	const before = document.activeElement;
	$effect(() => {
		card?.focus();
	});
	$effect(() => () => {
		if (before instanceof HTMLElement && before.isConnected) before.focus();
	});

	/** Tab and Shift+Tab go round the dialog's own controls, never out to the page under it. */
	function trap(e: KeyboardEvent) {
		if (e.key !== 'Tab' || !card) return;
		const all = [...card.querySelectorAll<HTMLElement>(FOCUSABLE)];
		if (all.length === 0) return e.preventDefault();
		const first = all[0];
		const last = all[all.length - 1];
		const at = document.activeElement;
		if (e.shiftKey && (at === first || at === card)) {
			e.preventDefault();
			last.focus();
		} else if (!e.shiftKey && at === last) {
			e.preventDefault();
			first.focus();
		}
	}
</script>

<div
	class="fixed inset-0 flex animate-fade-in items-center justify-center bg-[rgb(0_0_0_/_0.38)] p-4 backdrop-blur-[3px] {top
		? 'z-60'
		: 'z-50'}"
	out:backdropOut|global
>
	<button
		type="button"
		class="absolute inset-0 cursor-default"
		aria-label={cancelLabel}
		onclick={oncancel}
	></button>

	<div
		class="relative flex max-h-full animate-card-in flex-col overflow-y-auto rounded-[26px] bg-surface p-6 text-ink shadow-modal outline-none {size}"
		out:cardOut|global
		role="dialog"
		aria-modal="true"
		aria-label={title}
		tabindex="-1"
		bind:this={card}
		onkeydown={trap}
		data-testid={testid}
	>
		{#if hint}
			<div class="flex flex-col gap-1">
				<h2 class="text-[20px] font-extrabold">{title}</h2>
				<p class="text-sm text-ink-muted">{hint}</p>
			</div>
		{:else}
			<h2 class="text-[20px] font-extrabold">{title}</h2>
		{/if}

		{@render children()}

		{#if actions}
			<div class="flex flex-wrap items-center justify-end gap-2">
				{@render actions()}
			</div>
		{/if}
	</div>
</div>
