<script lang="ts">
	import { untrack } from 'svelte';
	import type { PageAdjust } from '$lib/hub/generated/Yass.Core.Domain';
	import { NEUTRAL_ADJUST, isNeutral, toneFactor } from '$lib/review/edits';
	import { Icon } from '$lib/icons';
	import Modal, { dialogCancel, dialogPrimary } from './Modal.svelte';
	import Slider from './Slider.svelte';
	import { t } from '$lib/i18n/index.svelte';

	let {
		src,
		alt,
		adjust,
		count = 1,
		onapply,
		oncancel
	}: {
		src: string;
		alt: string;
		adjust: PageAdjust | null;
		count?: number;
		onapply: (adjust: PageAdjust | undefined) => void;
		oncancel: () => void;
	} = $props();

	// Seeded once and then owned by the dialog, as in CropModal: the sliders are a draft.
	const current = untrack(() => adjust ?? NEUTRAL_ADJUST);
	let draft = $state<PageAdjust>({ ...current });

	// The preview already has the current adjust baked in, so only the difference is applied.
	// CSS brightness and contrast use the same formulas the server renders with.
	const filter = $derived(
		[
			`brightness(${(toneFactor(draft.Brightness) / toneFactor(current.Brightness)).toFixed(3)})`,
			`contrast(${(toneFactor(draft.Contrast) / toneFactor(current.Contrast)).toFixed(3)})`,
			draft.Sharpen > current.Sharpen ? 'url(#adjust-sharpen)' : ''
		].join(' ')
	);

	/** An unsharp kernel for the preview: a sketch of the server's Gaussian sharpen, not a copy. */
	const kernel = $derived.by(() => {
		const a = ((draft.Sharpen - current.Sharpen) / 100) * 0.6;
		return `0 ${-a} 0 ${-a} ${1 + 4 * a} ${-a} 0 ${-a} 0`;
	});

	const unchanged = $derived(
		draft.Brightness === current.Brightness &&
			draft.Contrast === current.Contrast &&
			draft.Sharpen === current.Sharpen
	);
</script>

<Modal
	title={t('dialogs.adjustTitle', { n: count })}
	cancelLabel={t('dialogs.cancelAdjust')}
	{oncancel}
	class="w-[min(640px,100%)] gap-[18px]"
	testid="adjust-modal"
>
	<div class="flex flex-wrap items-start gap-[18px]">
		<div class="relative mx-auto w-full max-w-[250px] flex-none">
			<svg class="absolute h-0 w-0" aria-hidden="true">
				<filter id="adjust-sharpen">
					<feConvolveMatrix order="3" preserveAlpha="true" kernelMatrix={kernel} />
				</filter>
			</svg>
			<img
				{src}
				{alt}
				class="block w-full rounded-[4px] shadow-page"
				style:filter
				data-testid="adjust-preview"
			/>
		</div>

		<div class="flex min-w-[220px] flex-1 flex-col gap-2">
			<Slider
				name="adjust-brightness"
				label={t('dialogs.brightness')}
				value={draft.Brightness}
				onchange={(v) => (draft = { ...draft, Brightness: v })}
			/>
			<Slider
				name="adjust-contrast"
				label={t('dialogs.contrast')}
				value={draft.Contrast}
				onchange={(v) => (draft = { ...draft, Contrast: v })}
			/>
			<Slider
				name="adjust-sharpen"
				label={t('dialogs.sharpen')}
				min={0}
				max={100}
				step={10}
				value={draft.Sharpen}
				onchange={(v) => (draft = { ...draft, Sharpen: v })}
			/>
			{#if count > 1}
				<p class="text-[11px] text-ink-faint">
					{t('dialogs.adjustMany')}
				</p>
			{/if}
		</div>
	</div>

	{#snippet actions()}
		<button
			type="button"
			class="mr-auto h-[46px] rounded-[14px] px-3 text-sm font-bold text-accent transition-colors hover:bg-accent-wash disabled:pointer-events-none disabled:text-ink-faint"
			disabled={isNeutral(draft)}
			onclick={() => (draft = { ...NEUTRAL_ADJUST })}
			data-testid="adjust-reset"
		>
			{t('dialogs.resetAll')}
		</button>
		<button type="button" class={dialogCancel} onclick={oncancel}>
			{t('dialogs.cancel')}
		</button>
		<button
			type="button"
			class={dialogPrimary}
			disabled={unchanged && count === 1}
			onclick={() => onapply(isNeutral(draft) ? undefined : draft)}
			data-testid="adjust-apply"
		>
			<Icon name="sliders" />
			{t('dialogs.apply')}
		</button>
	{/snippet}
</Modal>
