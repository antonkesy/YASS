<script lang="ts">
	import { useLayer } from '$lib/keys.svelte';
	import { backdropOut, cardOut } from '$lib/motion';
	import { untrack } from 'svelte';
	import type { PageAdjust } from '$lib/hub/generated/Yass.Core.Domain';
	import { NEUTRAL_ADJUST, isNeutral, toneFactor } from '$lib/review/edits';
	import { Icon } from '$lib/icons';
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
		/** The preview of the page in view, which already carries its current adjust. */
		src: string;
		alt: string;
		/** The page in view's current adjust, if any; the sliders start from it. */
		adjust: PageAdjust | null;
		/** How many pages Apply sets, for the title. */
		count?: number;
		onapply: (adjust: PageAdjust | undefined) => void;
		oncancel: () => void;
	} = $props();

	// Seeded once and then owned by the dialog, as in CropModal: the sliders are a draft.
	const current = untrack(() => adjust ?? NEUTRAL_ADJUST);
	let draft = $state<PageAdjust>({ ...current });

	/**
	 * The preview image already has the current adjust baked in, so the live preview applies
	 * only the difference. Brightness and contrast are CSS's own formulas, the same ones the
	 * server renders with, so this is what Apply will give back.
	 */
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

	let dialog = $state<HTMLDivElement | null>(null);
	$effect(() => {
		dialog?.focus();
	});

	// Escape cancels, through the shared layer stack: only this closes, not the preview under it.
	useLayer(() => oncancel());
</script>

<div
	class="fixed inset-0 z-50 flex animate-fade-in items-center justify-center bg-[rgb(0_0_0_/_0.38)] p-4 backdrop-blur-[3px]"
	out:backdropOut|global
>
	<button
		type="button"
		class="absolute inset-0 cursor-default"
		aria-label={t('dialogs.cancelAdjust')}
		onclick={oncancel}
	></button>

	<div
		class="relative flex max-h-full w-[min(640px,100%)] animate-card-in flex-col gap-[18px] overflow-y-auto rounded-[26px] bg-surface p-6 text-ink shadow-modal outline-none"
		out:cardOut|global
		role="dialog"
		aria-modal="true"
		aria-label={t('dialogs.adjustTitle', { n: count })}
		tabindex="-1"
		bind:this={dialog}
		data-testid="adjust-modal"
	>
		<h2 class="text-[20px] font-extrabold">
			{t('dialogs.adjustTitle', { n: count })}
		</h2>

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

		<div class="flex flex-wrap items-center justify-end gap-2">
			<button
				type="button"
				class="mr-auto h-[46px] rounded-[14px] px-3 text-sm font-bold text-accent transition-colors hover:bg-accent-wash disabled:pointer-events-none disabled:text-ink-faint"
				disabled={isNeutral(draft)}
				onclick={() => (draft = { ...NEUTRAL_ADJUST })}
				data-testid="adjust-reset"
			>
				{t('dialogs.resetAll')}
			</button>
			<button
				type="button"
				class="h-[46px] rounded-[14px] bg-ctl px-4 text-sm font-bold text-ink-3 transition-colors hover:bg-ctl-hover"
				onclick={oncancel}
			>
				{t('dialogs.cancel')}
			</button>
			<button
				type="button"
				class="flex h-[46px] items-center gap-2 rounded-[14px] bg-accent px-5 text-[15px] font-extrabold text-white shadow-export transition-[background-color,box-shadow] hover:bg-accent-hover disabled:pointer-events-none disabled:opacity-40"
				disabled={unchanged && count === 1}
				onclick={() => onapply(isNeutral(draft) ? undefined : draft)}
				data-testid="adjust-apply"
			>
				<Icon name="sliders" />
				{t('dialogs.apply')}
			</button>
		</div>
	</div>
</div>
