<script lang="ts">
	import { untrack } from 'svelte';
	import { Rotation, type CropBox } from '$lib/hub/generated/Yass.Core.Domain';
	import { cropFromView, cropToView } from '$lib/review/edits';
	import { Icon } from '$lib/icons';
	import { t } from '$lib/i18n/index.svelte';
	import Modal, { dialogCancel, dialogPrimary } from './Modal.svelte';

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

<Modal
	title={t('dialogs.cropTitle')}
	cancelLabel={t('dialogs.cancelCrop')}
	{oncancel}
	class="w-[min(600px,100%)] gap-[18px]"
	testid="crop-modal"
>
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

	{#snippet actions()}
		<button type="button" class={dialogCancel} onclick={oncancel}>
			{t('dialogs.cancel')}
		</button>
		<button
			type="button"
			class={dialogPrimary}
			disabled={impossible}
			onclick={() => onapply(empty ? undefined : cropFromView(box, turn))}
			data-testid="crop-apply"
		>
			<Icon name="crop" />
			{empty ? t('dialogs.removeCrop') : t('dialogs.applyCrop')}
		</button>
	{/snippet}
</Modal>
