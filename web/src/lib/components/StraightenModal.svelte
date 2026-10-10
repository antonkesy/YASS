<script lang="ts">
	import { untrack } from 'svelte';
	import { MAX_SKEW, innerScale, roundSkew } from '$lib/review/edits';
	import { Icon } from '$lib/icons';
	import Slider from './Slider.svelte';
	import { t } from '$lib/i18n/index.svelte';
	import Modal, { dialogCancel, dialogPrimary } from './Modal.svelte';

	let {
		src,
		alt,
		skew,
		onapply,
		oncancel
	}: {
		/** The page's preview, which already has its current skew applied and cropped. */
		src: string;
		alt: string;
		skew: number;
		onapply: (degrees: number) => void;
		oncancel: () => void;
	} = $props();

	const current = untrack(() => skew);
	let draft = $state(current);

	let size = $state({ width: 1, height: 1.414 });

	// Turned by the change only and scaled to still fill the frame, as the server crops,
	// so the grid matches what Apply gives back.
	const delta = $derived(Math.round((draft - current) * 10) / 10);
	const scale = $derived(1 / innerScale(size.width, size.height, delta));

	function nudge(by: number) {
		draft = roundSkew(draft + by);
	}

	const nudgeBtn =
		'flex h-11 w-11 flex-none items-center justify-center rounded-full bg-ctl text-ink transition-colors hover:bg-ctl-hover disabled:pointer-events-none disabled:opacity-40';
</script>

<Modal
	title={t('dialogs.straightenTitle')}
	cancelLabel={t('dialogs.cancelStraighten')}
	{oncancel}
	class="w-[min(640px,100%)] gap-[18px]"
	testid="straighten-modal"
>
	<div class="flex flex-wrap items-start gap-[18px]">
		<div
			class="relative mx-auto w-full max-w-[250px] flex-none overflow-hidden rounded-[4px] shadow-page"
		>
			<img
				{src}
				{alt}
				class="block w-full transition-transform duration-100"
				style:transform="rotate({delta}deg) scale({scale})"
				onload={(e) => {
					const img = e.currentTarget as HTMLImageElement;
					size = { width: img.naturalWidth, height: img.naturalHeight };
				}}
				data-testid="straighten-preview"
			/>
			<div
				class="pointer-events-none absolute inset-0"
				style="background-image: linear-gradient(to right, rgb(0 122 255 / 0.35) 1px, transparent 1px), linear-gradient(to bottom, rgb(0 122 255 / 0.35) 1px, transparent 1px); background-size: 10% 10%"
				aria-hidden="true"
			></div>
		</div>

		<div class="flex min-w-[220px] flex-1 flex-col gap-3">
			<Slider
				name="straighten"
				label={t('dialogs.angle')}
				min={-MAX_SKEW}
				max={MAX_SKEW}
				step={0.1}
				value={draft}
				onchange={(v) => (draft = roundSkew(v))}
			/>
			<div class="flex items-center gap-2">
				<button
					type="button"
					class={nudgeBtn}
					onclick={() => nudge(-0.1)}
					disabled={draft <= -MAX_SKEW}
					aria-label={t('dialogs.turnAnticlockwise')}
					data-testid="straighten-left"
				>
					<Icon name="rotateLeft" />
				</button>
				<span class="flex-1 text-center text-[12px] text-ink-muted">
					{t('dialogs.lineUp')}
				</span>
				<button
					type="button"
					class={nudgeBtn}
					onclick={() => nudge(0.1)}
					disabled={draft >= MAX_SKEW}
					aria-label={t('dialogs.turnClockwise')}
					data-testid="straighten-right"
				>
					<Icon name="rotateRight" />
				</button>
			</div>
		</div>
	</div>

	{#snippet actions()}
		<button type="button" class={dialogCancel} onclick={oncancel}>
			{t('dialogs.cancel')}
		</button>
		<button
			type="button"
			class={dialogPrimary}
			disabled={draft === current}
			onclick={() => onapply(draft)}
			data-testid="straighten-apply"
		>
			{t('dialogs.apply')}
		</button>
	{/snippet}
</Modal>
