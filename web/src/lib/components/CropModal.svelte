<script lang="ts">
	import { useLayer } from '$lib/keys.svelte';
	import { backdropOut, cardOut } from '$lib/motion';
	import { untrack } from 'svelte';
	import { Rotation, type CropBox } from '$lib/hub/generated/Yass.Core.Domain';
	import { cropFromView, cropToView } from '$lib/review/edits';
	import { Icon } from '$lib/icons';
	import { t } from '$lib/i18n/index.svelte';

	let {
		src,
		alt,
		crop,
		rotation = Rotation.R0,
		onapply,
		oncancel
	}: {
		/**
		 * The page's frame image: upright, with every edit but its crop and skew. Drawing over the
		 * page's preview instead put the box in the wrong place on a page already cropped.
		 */
		src: string;
		alt: string;
		/** The page's current crop, if any, in the scan's own coordinates. */
		crop: CropBox | null;
		/** The page's quarter turns, which the frame image shows and the crop is taken before. */
		rotation?: Rotation;
		onapply: (crop: CropBox | undefined) => void;
		oncancel: () => void;
	} = $props();

	// Escape closes this, and only this, while it is open.
	useLayer(() => oncancel());

	const EDGES = [
		{ key: 'Top', label: 'dialogs.edgeTop' },
		{ key: 'Right', label: 'dialogs.edgeRight' },
		{ key: 'Bottom', label: 'dialogs.edgeBottom' },
		{ key: 'Left', label: 'dialogs.edgeLeft' }
	] as const;

	// Seeded once and then owned by the dialog: the sliders are a draft, not a mirror.
	// The dialog is mounted fresh each time it opens, so the seed is always current. The
	// draft is the box as it looks on the upright page, which is what the sliders' Top and
	// Left mean to whoever is moving them.
	const turn = untrack(() => rotation);
	let box = $state<CropBox>(
		untrack(() => cropToView(crop ?? { Top: 0, Right: 0, Bottom: 0, Left: 0 }, turn))
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
	class="fixed inset-0 z-50 flex animate-fade-in items-center justify-center bg-[rgb(0_0_0_/_0.38)] p-4 backdrop-blur-[3px]"
	out:backdropOut|global
>
	<button
		type="button"
		class="absolute inset-0 cursor-default"
		aria-label={t('dialogs.cancelCrop')}
		onclick={oncancel}
	></button>

	<div
		class="relative flex max-h-full w-[min(600px,100%)] animate-card-in flex-col gap-[18px] overflow-y-auto rounded-[26px] bg-surface p-6 shadow-modal"
		out:cardOut|global
		role="dialog"
		aria-modal="true"
		aria-label={t('dialogs.cropTitle')}
		data-testid="crop-modal"
	>
		<h2 class="text-[20px] font-extrabold">{t('dialogs.cropTitle')}</h2>

		<div class="flex flex-wrap items-start gap-[18px]">
			<div class="relative mx-auto w-full max-w-[250px] flex-none">
				<img {src} {alt} class="block w-full rounded-[4px] shadow-page" />
				<!--
					The kept region is outlined and everything outside it is dimmed by a very
					large spread shadow, which needs no second element to cover the page.
				-->
				<div
					class="pointer-events-none absolute rounded-[4px] outline-2 outline-accent"
					style="top: {box.Top}%; right: {box.Right}%; bottom: {box.Bottom}%; left: {box.Left}%; box-shadow: 0 0 0 999px rgb(0 0 0 / 0.4)"
				></div>
			</div>

			<div class="flex min-w-[220px] flex-1 flex-col gap-3">
				{#each EDGES as edge (edge.key)}
					<label class="flex flex-col gap-1">
						<span class="flex justify-between text-[13px] font-bold text-ink-2">
							{t(edge.label)}
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
					{t('dialogs.reset')}
				</button>
			</div>
		</div>

		{#if impossible}
			<p class="rounded-[12px] bg-danger-bg px-3 py-2 text-xs font-bold text-danger" role="alert">
				{t('dialogs.cropImpossible')}
			</p>
		{/if}

		<div class="flex justify-end gap-2">
			<button
				type="button"
				class="h-[46px] rounded-[14px] bg-ctl px-4 text-sm font-bold text-ink-3 transition-colors hover:bg-ctl-hover"
				onclick={oncancel}
			>
				{t('dialogs.cancel')}
			</button>
			<button
				type="button"
				class="flex h-[46px] items-center gap-2 rounded-[14px] bg-linear-135 from-accent-light to-accent-grad2 px-5 text-[15px] font-extrabold text-white shadow-export transition-shadow disabled:pointer-events-none disabled:opacity-40"
				disabled={impossible}
				onclick={() => onapply(empty ? undefined : cropFromView(box, turn))}
				data-testid="crop-apply"
			>
				<Icon name="crop" />
				{empty ? t('dialogs.removeCrop') : t('dialogs.applyCrop')}
			</button>
		</div>
	</div>
</div>
