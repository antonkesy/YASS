<script lang="ts">
	import { untrack } from 'svelte';
	import type { CropBox } from '$lib/hub/generated/Yass.Core.Domain';
	import { Icon } from '$lib/icons';

	let {
		src,
		alt,
		crop,
		onapply,
		oncancel
	}: {
		src: string;
		alt: string;
		/** The page's current crop, if any. */
		crop: CropBox | null;
		onapply: (crop: CropBox | undefined) => void;
		oncancel: () => void;
	} = $props();

	const EDGES = [
		{ key: 'Top', label: 'Top' },
		{ key: 'Right', label: 'Right' },
		{ key: 'Bottom', label: 'Bottom' },
		{ key: 'Left', label: 'Left' }
	] as const;

	// Seeded once and then owned by the dialog: the sliders are a draft, not a mirror.
	// The dialog is mounted fresh each time it opens, so the seed is always current.
	let box = $state<CropBox>(
		untrack(() => ({
			Top: crop?.Top ?? 0,
			Right: crop?.Right ?? 0,
			Bottom: crop?.Bottom ?? 0,
			Left: crop?.Left ?? 0
		}))
	);

	const empty = $derived(box.Top === 0 && box.Right === 0 && box.Bottom === 0 && box.Left === 0);

	// Mirrors the server's rule, so the Apply button cannot offer an edit that would
	// be rejected on arrival.
	const impossible = $derived(box.Top + box.Bottom > 95 || box.Left + box.Right > 95);

	function set(key: (typeof EDGES)[number]['key'], value: number) {
		box = { ...box, [key]: value };
	}
</script>

<div
	class="fixed inset-0 z-50 flex animate-fade-in items-center justify-center bg-[rgb(19_26_43_/_0.38)] p-4 backdrop-blur-[3px]"
>
	<button
		type="button"
		class="absolute inset-0 cursor-default"
		aria-label="Cancel crop"
		onclick={oncancel}
	></button>

	<div
		class="relative flex max-h-full w-[min(600px,100%)] animate-pop-in flex-col gap-[18px] overflow-y-auto rounded-[26px] bg-surface p-6 shadow-modal"
		role="dialog"
		aria-modal="true"
		aria-label="Crop page"
		data-testid="crop-modal"
	>
		<h2 class="text-[20px] font-extrabold">Crop page</h2>

		<div class="flex flex-wrap items-start gap-[18px]">
			<div class="relative mx-auto w-full max-w-[250px] flex-none">
				<img {src} {alt} class="block w-full rounded-[4px] shadow-page" />
				<!--
					The kept region is outlined and everything outside it is dimmed by a very
					large spread shadow, which needs no second element to cover the page.
				-->
				<div
					class="pointer-events-none absolute rounded-[4px] outline-2 outline-accent"
					style="top: {box.Top}%; right: {box.Right}%; bottom: {box.Bottom}%; left: {box.Left}%; box-shadow: 0 0 0 999px rgb(19 26 43 / 0.4)"
				></div>
			</div>

			<div class="flex min-w-[220px] flex-1 flex-col gap-3">
				{#each EDGES as edge (edge.key)}
					<label class="flex flex-col gap-1">
						<span class="flex justify-between text-[13px] font-bold text-ink-2">
							{edge.label}
							<span class="text-ink-muted tabular-nums">{box[edge.key]}%</span>
						</span>
						<input
							class="h-9 accent-accent"
							type="range"
							min="0"
							max="30"
							step="1"
							value={box[edge.key]}
							oninput={(e) => set(edge.key, Number(e.currentTarget.value))}
							data-testid="crop-{edge.key.toLowerCase()}"
						/>
					</label>
				{/each}

				<button
					type="button"
					class="h-8 self-start rounded-full px-3 text-[13px] font-bold text-accent transition-colors hover:bg-accent-wash"
					onclick={() => (box = { Top: 0, Right: 0, Bottom: 0, Left: 0 })}
				>
					Reset
				</button>
			</div>
		</div>

		{#if impossible}
			<p class="rounded-[12px] bg-danger-bg px-3 py-2 text-xs font-bold text-danger" role="alert">
				A crop has to leave at least 5% of the page on each axis.
			</p>
		{/if}

		<div class="flex justify-end gap-2">
			<button
				type="button"
				class="h-[46px] rounded-[14px] bg-ctl px-4 text-sm font-bold text-ink-3 transition-colors hover:bg-ctl-hover"
				onclick={oncancel}
			>
				Cancel
			</button>
			<button
				type="button"
				class="flex h-[46px] items-center gap-2 rounded-[14px] bg-linear-135 from-accent-light to-accent-grad2 px-5 text-[15px] font-extrabold text-white shadow-export transition-shadow disabled:pointer-events-none disabled:opacity-40"
				disabled={impossible}
				onclick={() => onapply(empty ? undefined : box)}
				data-testid="crop-apply"
			>
				<Icon name="crop" />
				{empty ? 'Remove crop' : 'Apply crop'}
			</button>
		</div>
	</div>
</div>
