<script lang="ts">
	import type { PageDto } from '$lib/hub/generated/Yawss.Core.Hub';
	import { imageUrl } from '$lib/hub/hub';
	import { detectSwipe, step } from '$lib/review/swipe';

	let {
		sessionId,
		pages,
		index,
		onnavigate,
		onclose,
		onrotate
	}: {
		sessionId: string;
		pages: PageDto[];
		index: number;
		onnavigate: (index: number) => void;
		onclose: () => void;
		onrotate: (index: number, degrees: 90 | -90) => void;
	} = $props();

	const page = $derived(pages[index]);
	let shown = $state<{ hash: string; rotation: number } | null>(null);
	const delta = $derived(shown && page ? page.Rotation - shown.rotation : 0);

	let start: { x: number; y: number } | null = null;
	let dialog = $state<HTMLDivElement | null>(null);

	$effect(() => {
		dialog?.focus();
	});

	function pointerdown(e: PointerEvent) {
		start = { x: e.clientX, y: e.clientY };
	}

	function pointerup(e: PointerEvent) {
		if (!start) return;
		const direction = detectSwipe(e.clientX - start.x, e.clientY - start.y);
		start = null;
		if (direction) onnavigate(step(index, pages.length, direction));
	}

	function keydown(e: KeyboardEvent) {
		if (e.key === 'Escape') onclose();
		else if (e.key === 'ArrowRight') onnavigate(step(index, pages.length, 'left'));
		else if (e.key === 'ArrowLeft') onnavigate(step(index, pages.length, 'right'));
		else return;
		e.preventDefault();
	}

	const navBtn =
		'flex h-11 w-11 items-center justify-center rounded-full bg-black/50 text-white hover:bg-black/70 focus-visible:ring-4 focus-visible:ring-teal-300 focus-visible:outline-none disabled:opacity-30';
</script>

{#if page}
	<div
		class="fixed inset-0 z-30 flex flex-col bg-black text-white select-none"
		role="dialog"
		aria-modal="true"
		aria-label="Page {index + 1} of {pages.length}"
		tabindex="-1"
		bind:this={dialog}
		onkeydown={keydown}
		data-testid="preview"
	>
		<div class="flex items-center gap-2 p-2">
			<button
				type="button"
				class={navBtn}
				aria-label="Close preview"
				onclick={onclose}
				data-testid="preview-close"
			>
				<svg
					class="h-6 w-6"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					aria-hidden="true"><path d="M6 6l12 12M18 6L6 18" /></svg
				>
			</button>
			<span class="text-sm" data-testid="preview-counter"
				>{index + 1} / {pages.length}{page.Deleted ? ' · deleted' : ''}</span
			>
			<span class="ml-auto flex gap-1">
				<button
					type="button"
					class={navBtn}
					aria-label="Rotate left"
					onclick={() => onrotate(index, -90)}
				>
					<svg
						class="h-5 w-5"
						viewBox="0 0 24 24"
						fill="none"
						stroke="currentColor"
						stroke-width="2"
						aria-hidden="true"><path d="M3 12a9 9 0 1 0 3-6.7M3 4v5h5" /></svg
					>
				</button>
				<button
					type="button"
					class={navBtn}
					aria-label="Rotate right"
					onclick={() => onrotate(index, 90)}
				>
					<svg
						class="h-5 w-5"
						viewBox="0 0 24 24"
						fill="none"
						stroke="currentColor"
						stroke-width="2"
						aria-hidden="true"><path d="M21 12a9 9 0 1 1-3-6.7M21 4v5h-5" /></svg
					>
				</button>
			</span>
		</div>

		<div
			class="relative flex min-h-0 flex-1 touch-pan-y items-center justify-center"
			onpointerdown={pointerdown}
			onpointerup={pointerup}
			onpointercancel={() => (start = null)}
		>
			{#key page.Hash}
				<img
					src={imageUrl(sessionId, page.Index, page.Hash, 'preview')}
					alt="Page {index + 1}"
					class="max-h-full max-w-full object-contain transition-transform duration-200 {page.Deleted
						? 'opacity-50'
						: ''}"
					style:transform={delta ? `rotate(${delta}deg)` : undefined}
					draggable="false"
					onload={() => (shown = { hash: page.Hash, rotation: page.Rotation })}
				/>
			{/key}
			<button
				type="button"
				class="{navBtn} absolute left-2"
				aria-label="Previous page"
				disabled={index === 0}
				onclick={() => onnavigate(index - 1)}
				data-testid="preview-prev"
			>
				<svg
					class="h-6 w-6"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					aria-hidden="true"><path d="M15 6l-6 6 6 6" /></svg
				>
			</button>
			<button
				type="button"
				class="{navBtn} absolute right-2"
				aria-label="Next page"
				disabled={index >= pages.length - 1}
				onclick={() => onnavigate(index + 1)}
				data-testid="preview-next"
			>
				<svg
					class="h-6 w-6"
					viewBox="0 0 24 24"
					fill="none"
					stroke="currentColor"
					stroke-width="2"
					aria-hidden="true"><path d="M9 6l6 6-6 6" /></svg
				>
			</button>
		</div>
	</div>
{/if}
